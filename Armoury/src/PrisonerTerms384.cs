using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Armoury
{
    /// <summary>
    /// 384 (Jeff 09.11: "nie sprzedam ich, lub wymienie ich za odstapienie od oblezenia i nie atakowanie moich ziem, albo inne warunki rozejmu";
    /// "daj warunki w parly ze moge ziemie lub moge rozmawiac z dowodca oblezenia zeby odstapil od oblezenia za jencow, lub przepuscil mnie na slowo honoru
    /// lub inne elementy ktore mozna bylo dogadac paktujac z lordem przeciwnej strony i majac np. jencow zloto etc."). PERTRAKTACJE Z WODZEM OBLEZENIA.
    /// Klucz PrisonerExchangeForTerms, dlugosc rozejmu / paktu PrisonerExchangePactDays (60 dob = 1/6 roku kalendarza Armoury 364 - ta sama miara co
    /// SiegeRansomTruceDays okupu, jedna miara rozejmu).
    ///
    /// Skad rozmowa: ta sama co 374 (gracz w oblezonej twierdzy, "Request a parley" gry -> rozmowa z wodzem obozu; warunki jak SiegeSurrender374.AskCond).
    /// Linia w hero_main_options (priorytet 104, obok 374): ZADANIE (co gracz chce) -> OFERTA (co daje: jency-lordowie frakcji wodza z lochow rodu
    /// gracza i z jego druzyny - 8 najcenniejszych osobno i "wszyscy", zloto 5 000 / 20 000, zamek albo miasto rodu gracza - do 6, najcenniejsze pierwsze, bez oblezonych; 384b)
    /// -> ocena wodza -> zgoda / kontroferta (najwyzej 2 rundy: dodaj zloto albo jeszcze jednego jenca) / odmowa (3 doby przerwy). Bez okien - same linie
    /// rozmowy (zadnego okna nad misja rozmowy, zadnego SwitchToMenu).
    /// ZADANIA:
    ///  L - odstapienie: oboz zwiniety droga gry (SiegeRansom.Lift384 = RemoveAllSiegeParties w takcie kampanii, jak okup), rozejm tej osady N dob dla frakcji
    ///      i rodow obozu (SiegeRansom.Truce384 - ta sama tablica, latka celu i nasluch zlamania co okup);
    ///  A - pakt: L + frakcja i rody obozu nie celuja w osady i wsie rodu gracza N dob (ScorePact w SiegeRansom.ScorePost: oblezenie i najazd -> wynik 0)
    ///      i ich druzyny nie atakuja druzyny gracza (SetDoNotAttackMainParty 25 h co dobe);
    ///  P - swobodne przejscie: gracz z druzyna wychodzi (menu arm384_pass po rozmowie - SetNextMenu jak 374/gra przy SafePassageBarterable, potem jak gry
    ///      "Leave": FinalizePlayerSiege, LeaveSettlement, Finish), twierdza zostaje z zaloga, oboz nie atakuje gracza 48 h.
    ///  Rozejm miedzy krolestwami - linia widoczna, nieczynna z powodem (gracz nie jest wladca; wladca - pokoj zawiera sie na dworze, nie pod murami).
    /// OCENA (denary): V = jency (Ransom178.PriceFor; rod wladcy/nastepca x3, krewni i rod wodza x1.5; relacja wodza z jencem 0.5-1.5; jency w lochu OBLEZONEJ
    ///   twierdzy x (1 - 0.7 p) - wodz i tak ich uwolni, gdy zdobedzie mury; Mercy +10%/poziom) + zloto + ziemia (SettlementValueModel dla rodu wodza);
    ///   Honor > 0: +5%/poziom. C = L: p x wartosc osady x (1 + 0.15 Valor); P: p x (okup gracza + 0.3 wartosci wojskowej druzyny) + 2000; A: L + (N/60) x 0.1 x
    ///   wartosc pozostalych twierdz gracza; relacja wodza z graczem x (1 - rel/200). p = szansa zdobycia (sila obozu do murow R, odparte szturmy i straty
    ///   z 374, jedzenie, 0.05-0.95). Zgoda: V >= C x prog (Calculating > 0: 1.0, < 0: 0.9, inaczej 0.95); kontroferta od V >= 0.7 C (Calculating > 0: 0.8 C) - od C, nie od progu.
    /// ZDRADA (losowana przy zgodzie, SiegeSurrender374.BetrayChance - tylko Honor < 0): L/A - oboz odchodzi i bierze jencow, ale rozejmu / paktu nie
    ///   dotrzymuje (wpisy tylko pilnowane); powrot w czasie umowy = linia ZDRADA, klamca (lista 374), relacja -30, honorowe glowy rodow -5 x Honor.
    ///   P - jency i zloto oddane przy bramie, potem pojmanie gracza i towarzyszy, szeregowi do lochu (jak RunBetray 374, bez zmiany pana twierdzy).
    /// ZLAMANIE PRZEZ GRACZA (384b): tylko PAKT (A - przysiega wzajemna, "and I swear the same") - gracz zaczyna atak na druzyne / wies / oblezenie frakcji
    ///   z paktem w czasie paktu. NIE: samoobrona (wypad na oboz pod twierdza rodu gracza, atak na druzyne, ktora najezdza wies gracza albo ma za cel druzyne
    ///   gracza) i umowa L (gracz niczego nie przysiega). Skutek: wpisy tej umowy zdjete (takze rozejm osady), relacja z przysiegajacym -20 i z wladca -10,
    ///   Honor gracza -500 xp (TraitLevelingHelper.OnIncidentResolved). P: gracz wyszedl za brame, a nie oddal obiecanego (jency zniknely, zloto, ziemia) -
    ///   oddane, co sie da, i ta sama kara (bez terminu 3 dob - usluga juz wykonana).
    /// UCZCIWA UMOWA A ARMIE BK (384b): wybor celu armii BK nie pyta o wynik celu - oblezenie wbrew niezdradzieckiemu wpisowi (OnSiegeStarted albo przeglad
    ///   doby) idzie do kolejki zwiniecia (SiegeRansom.Lift384 w takcie kampanii, ta sama droga co okup), bez kary dla przysiegajacego.
    /// WYKONANIE tylko w takcie kampanii (bez misji i rozmowy; L/A przy braku bitwy przy obozie, najwyzej 1 doba czekania; P po wyjsciu gracza na mape):
    ///   najpierw sprawdzenie (jency nadal u rodu gracza, zloto, ziemia wolna od oblezenia), dla L/A najpierw zwiniecie obozu (nie wyszlo = bez zaplaty),
    ///   potem jency wolni (EndCaptivityAction.ApplyByReleasedByChoice - jak gra przy uwolnieniu z lochu; bez urazy 367d, bo uraza liczy okup i rokowania),
    ///   zloto GiveGoldAction gracz -> wodz (platnik i odbiorca), ziemia ChangeOwnerOfSettlementAction.ApplyByBarter (jak FiefBarterable; bez roszczenia 369 -
    ///   ApplyByBarter to nie BySiege; wladca gracza -10 relacji za oddanie ziemi korony).
    /// Zapis "arm_k384": kolejka, oferta w menu, wpisy umow (klucz, zasieg, do doby, przysiegajacy, zdrada), przerwy odmow. Log 'Wymiana jencow (384): ...'.
    /// Potkniecia liczone (log pierwszego na miejsce), nic sie nie gasi.
    ///
    /// 385 (Jeff 10.11 "rob caly punkt 2" - reszta pertraktacji), klucze PrisonerTermsPickWindow / ReleaseMyMen / Oath / FieldParley / RealmTruce:
    ///  OKNO WYBORU JENCOW: linia "Let me name the lords..." -> linia wodza "Name them, then." z konsekwencja otwierajaca MultiSelectionInquiry (wszyscy jency,
    ///   nie 8) - wzor gry (turn_companion_to_lord_consequence: okno z konsekwencji linii NPC, powrot ConversationManager.ContinueConversation). Klucz NIE = 8 linii.
    ///  MOI LUDZIE: bohaterowie rodu gracza i towarzysze w niewoli frakcji rozmowcy (wladca - cala frakcja; inny lord - jego rod, jego druzyna, armia, oboz) -
    ///   przelacznik w ofercie (koszt C + suma okupow Ransom178.PriceOf) albo samodzielne zadanie M w polu. Wykonanie po zaplacie:
    ///   EndCaptivityAction.ApplyByReleasedByCompensation (towarzysz wraca droga gry jako zbieg, czlonek rodu - Released). Zdrada: nie puszcza. Zolnierze - nie
    ///   (loch nie pamieta, czyi byli).
    ///  PRZYSIEGA w ofercie: gracz przysiega nie podnosic broni na frakcje N dob - mechanizm GRY (IFaction.NotAttackableByPlayerUntilTime, jak
    ///   NoAttackBarterable): gra sama wylacza atak / oblezenie / najazd / dolaczenie do armii przeciw tej frakcji i sama zwalnia, gdy tamci zaatakuja gracza.
    ///   Wartosc 0.1 x wartosc wojskowa druzyny x sqrt(N/5) (gra: 0.1 za 5 dob), x0.5 przy Honor gracza < 0. Nie przy A (przysiega wzajemna juz w zadaniu) i T.
    ///  ROZMOWA W POLU (St = ""): ta sama linia w hero_main_options z lordem-wodzem druzyny wrogiej frakcji na mapie (gracz podjechal; gdy lord sam atakuje, gra
    ///   nie dochodzi do hero_main_options). Zadania: A (pakt - klucz wpisu: frakcja, gdy rozmowca jest wladca, inaczej "c:"+rod), M, T. Wykonanie po wyjsciu
    ///   ze spotkania; walka gracza z ta frakcja przed wykonaniem = umowa przepada.
    ///  ROZEJM KROLESTW (T): gracz JEST wladca krolestwa K, rozmowca wladca krolestwa E w wojnie z K, bez blokady CanonPeaceLock i bez wojny wymuszanej przez ROT
    ///   (poza para 330 - tam pokoj gracza sam staje sie rozejmem WarTruce). Koszt = -PeaceBarterable.GetUnitValueForFaction(E) (miara barteru gry), oferta =
    ///   zloto + ziemia + wartosc WSZYSTKICH jencow E (pokoj i tak otwiera lochy). Wykonanie: MakePeaceAction.Apply w takcie kampanii, sprawdzenie, ze wojna
    ///   zgasla, dopiero potem zaplata; wpis zasiegu "K" (klucz = E) - bramki wojny AI: WarTruce.AddDecisionPrefix, CanonThroneWars.WarScorePostfix,
    ///   CaptiveGrudge; wojna mimo to (WarDeclared): gracz pierwszy - zlamane slowo, tamci - kara tylko dla zdrajcy.
    ///  Zapis v2 (rekord umowy 17 pol; v1 czytany). Zakladnik - 385c (PrisonerTerms384.Hostage385c.cs).
    ///
    /// 386 (Jeff 10.11 "rob caly punkt 2" - 2b: pakt chroni ziemie i druzyne gracza, ale nie karawany ani druzyny towarzyszy), klucz PrisonerExchangePactGuardsClan:
    ///  PARTIE RODU (Mine): partie rodu gracza poza druzyna gracza, zaloga i milicja (karawany, druzyny towarzyszy) oraz wiesniacy wsi rodu gracza; NIE partia
    ///   DOLACZONA do cudzej armii (walczy pod cudzym rozkazem w wojnie korony - pakt jest osobisty). 386c: partia wezwana do cudzej armii, ale jeszcze w drodze,
    ///   jest partia rodu - chyba ze wodz tej armii wystepuje przeciw frakcji paktu (Hunting patrzy tez na wodza armii).
    ///  LATKA: postfiks DefaultMobilePartyAIModel.ShouldConsiderAttacking (jedyna bramka ataku z inicjatywy - CalculateInitiativeScoresForEnemy; gra czyta tam
    ///   DoNotAttackMainPartyUntil tylko dla druzyny gracza): partia frakcji / rodu z uczciwym paktem nie zaczyna ataku na partie rodu i partia rodu nie zaczyna
    ///   ataku na partie tej frakcji. Wyjatki - ta sama regula co dla gracza (384b): samoobrona (cel oblega / najezdza ziemie rodu albo sciga druzyne gracza /
    ///   partie rodu - AggressorCore) i dolaczenie do TRWAJACEJ bitwy (gracz tez lamie pakt tylko zaczynajac atak). Druga latka: ShouldConsiderAvoiding - te same
    ///   pary nie uciekaja przed soba (inaczej karawana gracza ucieka przed lordem, ktory jej nie ruszy, a CartGuard / CartEscort297 wstrzymuja wozy).
    ///  Watki AI: postfiksy czytaja tylko niezmienna migawke kluczy (_pact - podmieniana w calosci po kazdej zmianie wpisow) i pola partii; wpisy zdrajcow do
    ///   migawki nie wchodza (zdrajca atakuje i jest karany jak dotad - OnMapEventStarted). Dlugie rozkazy EngageParty sprzed paktu zdejmowane co dobe (ClearOrders).
    ///  LordEscort.Guard335 (obroncy chronionego wodza wymuszaja EngageParty z pominieciem ShouldConsiderAttacking): PactBlocks - napastnikiem nie jest druzyna
    ///   gracza ani partia rodu, gdy frakcja chronionego ma pakt (dziura 384: obroncy mogli uderzyc na gracza mimo paktu).
    ///
    /// 386c (Jeff 11.11 "tak, rob wszystko 2-4"): cel podejscia GoAroundParty (AiEngagePartyBehavior.AiHourlyTick; takze dodany przez RescueHunt) wyciety U ZRODLA -
    ///  postfiks 140 CampaignEventDispatcher.AiHourlyTick (ThinkPact): partia z paktem na druzyne gracza (sam klucz 384) / partie rodu, partia rodu na partie z
    ///  paktem; wyjatki jak w AttackPact. Powod: MobilePartyAi.GetGoAroundPartyBehavior bez punktu obejscia daje EngageParty prosto na cel BEZ pytania modelu
    ///  (dziura w zalozeniu 386b), a samo podejscie to krazenie wokol partii, ktorej i tak nie wolno ruszyc. Zalogi i milicje: bez latki (gra nie daje im inicjatywy
    ///  w polu ani nie bierze ich za cel - DefaultMobilePartyAIModel.GetBestInitiativeBehavior / ShouldPartyCheckInitiativeBehavior; Aggressiveness 0).
    /// </summary>
    internal static partial class PrisonerTerms384
    {
        internal const string Tag = "Wymiana jencow (384)";
        private const string MenuId = "arm384_pass";
        private const int AskPriority = 104;
        private const int ShowPris = 8, ShowFiefs = 6, MaxRounds = 2;
        private const double CooldownDays = 3.0;
        private const int NoAttackHours = 48, PactNoAttackHours = 25;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static bool On { get { var s = Settings.Current; return s != null && s.PrisonerExchangeForTerms; } }
        private static int Days { get { var s = Settings.Current; return s != null ? Math.Max(10, Math.Min(364, s.PrisonerExchangePactDays)) : 60; } }
        // 385
        private static bool KPick { get { var s = Settings.Current; return s != null && s.PrisonerTermsPickWindow; } }
        private static bool KMen { get { var s = Settings.Current; return s != null && s.PrisonerTermsReleaseMyMen; } }
        private static bool KOath { get { var s = Settings.Current; return s != null && s.PrisonerTermsOath; } }
        private static bool KField { get { var s = Settings.Current; return s != null && s.PrisonerTermsFieldParley; } }
        private static bool KRealm { get { var s = Settings.Current; return s != null && s.PrisonerTermsRealmTruce; } }
        // 386
        private static bool KClan { get { var s = Settings.Current; return s != null && s.PrisonerExchangeForTerms && s.PrisonerExchangePactGuardsClan; } }
        private const string FieldCool = "@f";

        private sealed class Deal
        {
            public string St = "", Lord = ""; public double Start; public char Want; public List<string> Pris = new List<string>(); public int Gold;
            public List<string> Fiefs = new List<string>(); public bool Betray; public double P, Day, V, C; public int Days;
            public string Host = "";   // 385c: zakladnik (StringId bohatera rodu gracza) - pole 17 rekordu
            public List<string> Men = new List<string>(); public bool Oath;   // 385: moi ludzie do uwolnienia, przysiega gracza; St == "" - rozmowa w polu
            public bool Field { get { return string.IsNullOrEmpty(St); } }
        }
        /// <summary>Wpis umowy: Key - frakcja albo "c:"+rod; Scope - "*" (wszystkie ziemie rodu gracza), StringId osady (rozejm osady) albo "K" (385: rozejm
        /// krolestw - Key to krolestwo, ktore przysieglo krolestwu gracza); Betray - tylko pilnowany.</summary>
        private sealed class Entry { public string Key = "", Scope = "", Lord = ""; public int Until; public bool Betray; }
        private sealed class Cool { public double Start, End; }
        private sealed class Ev { public double V, Vp, Vg, Vf, Vo, Vh, C, PTake, Thr, Lo; public char Why = 'T'; public string Parts = "-"; }

        private static readonly List<Deal> _queue = new List<Deal>();
        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly Dictionary<string, Cool> _cool = new Dictionary<string, Cool>();
        /// <summary>384b: oblezenia wbrew uczciwej umowie do zwiniecia w takcie kampanii (StringId osady -> poczatek oblezenia w godzinach). Nie w zapisie -
        /// przeglad doby (Daily) znajduje je ponownie po wczytaniu.</summary>
        private static readonly Dictionary<string, double> _relift = new Dictionary<string, double>();
        private static int _dRelift, _dReliftFail;
        private static Deal _offer;          // P: zgoda w rozmowie, czeka na opcje menu arm384_pass
        private static bool _armed;
        private static int _stumbles, _dZero, _dNoAttack, _dZeroMine;   // _dZeroMine 386b: cele druzyn rodu gracza na osadach frakcji z paktem
        private static readonly HashSet<string> _err = new HashSet<string>();

        // stan rozmowy
        private static Hero _talk;
        private static Settlement _st;
        private static char _want;
        private static readonly List<Hero> _pris = new List<Hero>();
        private static readonly Dictionary<Hero, double> _worth = new Dictionary<Hero, double>();
        private static readonly Dictionary<Hero, bool> _inSt = new Dictionary<Hero, bool>();
        private static readonly HashSet<Hero> _sel = new HashSet<Hero>();
        private static readonly List<Settlement> _fiefs = new List<Settlement>();
        private static readonly HashSet<Settlement> _selF = new HashSet<Settlement>();
        private static int _gold, _round, _ask;
        private static Hero _askHero;
        private static Ev _ev;
        private static char _verdict;
        // 385: rozmowa w polu, moi ludzie w niewoli rozmowcy, przysiega gracza w ofercie
        private static bool _field, _menOn, _oath;
        private static readonly List<Hero> _men = new List<Hero>();
        private static double _menCost;
        private static int _dRealmBlock, _dPick;
        private static readonly string[] NoKeys = new string[0];
        /// <summary>385: krolestwa z czynnym, uczciwym rozejmem krolestw (migawka dla bramek wojny - czytana z dowolnego watku).</summary>
        private static volatile string[] _realmKeys = NoKeys;

        internal static void Reset()
        {
            _realmKeys = NoKeys; _dRealmBlock = 0; _dPick = 0;
            _pact = null; PactZero(); _pThreadStumbles = 0;   // 386
            HostReset();   // 385c
            _queue.Clear(); _entries.Clear(); _cool.Clear(); _relift.Clear(); _dRelift = 0; _dReliftFail = 0; _offer = null; _armed = false; _stumbles = 0; _dZero = 0; _dNoAttack = 0; _dZeroMine = 0; _err.Clear();
            ClearTalk();
        }

        private static void ClearTalk()
        {
            _talk = null; _st = null; _want = '\0'; _pris.Clear(); _worth.Clear(); _inSt.Clear(); _sel.Clear(); _fiefs.Clear(); _selF.Clear();
            _gold = 0; _round = 0; _ask = 0; _askHero = null; _ev = null; _verdict = '\0';
            _field = false; _menOn = false; _oath = false; _men.Clear(); _menCost = 0;
            HostTalkClear();   // 385c
        }

        private static void Stumble(string where, Exception e)
        {
            _stumbles++;
            try { if (_err.Add(where)) Log.Error("PrisonerTerms384." + where, e); } catch { }
        }

        private static string Name(Hero h) { try { return h != null && h.Name != null ? h.Name.ToString() : "-"; } catch { return "?"; } }
        private static string Name(Settlement s) { try { return s != null && s.Name != null ? s.Name.ToString() : "-"; } catch { return "?"; } }
        private static string F(double v) { return v.ToString("0.#", Inv); }
        private static string N(double v) { return Math.Round(v).ToString("N0", Inv); }
        private static double Today { get { return CampaignTime.Now.ToDays; } }
        private static int Trait(Hero h, TraitObject t) { return SiegeSurrender374.Trait(h, t); }
        private static string WantName(char w) { return w == 'L' ? "odstapienie" : w == 'P' ? "swobodne przejscie" : w == 'A' ? "odstapienie + pakt" : w == 'M' ? "uwolnienie moich ludzi" : w == 'T' ? "rozejm krolestw" : "-"; }
        private static string WantName(Deal d) { return d.Field && d.Want == 'A' ? "pakt (pole)" : WantName(d.Want); }
        private static string Place(Settlement st) { return st != null ? "at " + Name(st) : "in the field"; }
        private static string Realm(IFaction f) { try { return f != null && f.Name != null ? f.Name.ToString() : "-"; } catch { return "?"; } }
        private static bool Ruler(Hero h) { try { return h != null && h.MapFaction != null && h.MapFaction.Leader == h; } catch { return false; } }

        /// <summary>374: czy w toku jest umowa 384 o te osade (oferta w menu albo kolejka) - 374 wtedy nie proponuje poddania.</summary>
        internal static bool Pending(Settlement st)
        {
            if (st == null) return false;
            if (_offer != null && _offer.St == st.StringId) return true;
            foreach (var d in _queue) if (d.St == st.StringId) return true;
            return false;
        }

        // ------------------------------------------------------------ rozmowa
        internal static void AddDialogs(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddPlayerLine("arm384_ask", "hero_main_options", "arm384_open", "{=!}{ARM384_ASK}", AskCond, Begin, AskPriority, AskClick);
                starter.AddDialogLine("arm384_open", "arm384_open", "arm384_want", "{=!}{ARM384_OPEN}", OpenCond, null, 200);
                starter.AddPlayerLine("arm384_w_lift", "arm384_want", "arm384_wfor", "{=!}{ARM384_WL}", WantLCond, () => SetWant('L'), 112);
                starter.AddPlayerLine("arm384_w_pact", "arm384_want", "arm384_wfor", "{=!}{ARM384_WA}", WantACond, () => SetWant('A'), 111);
                starter.AddPlayerLine("arm384_w_pass", "arm384_want", "arm384_wfor", "{=!}{ARM384_WP}", WantPCond, () => SetWant('P'), 110);
                starter.AddPlayerLine("arm384_w_men", "arm384_want", "arm384_wfor", "{=!}{ARM384_WM}", WantMCond, () => { SetWant('M'); _menOn = true; }, 109);   // 385: tylko w polu
                starter.AddPlayerLine("arm384_w_peace", "arm384_want", "arm384_wfor", "{=!}A truce between our realms.", null, () => SetWant('T'), 108, PeaceClick);   // 385: czynna dla gracza-wladcy
                starter.AddPlayerLine("arm384_w_back", "arm384_want", "lord_pretalk", "{=!}Never mind.", null, ClearTalk, 100);
                starter.AddDialogLine("arm384_wfor", "arm384_wfor", "arm384_offer", "{=!}{ARM384_WFOR}", WforCond, null, 200);
                for (int k = 0; k < ShowPris; k++)
                {
                    int i = k;
                    starter.AddPlayerLine("arm384_p" + i, "arm384_offer", "arm384_ack", "{=!}{ARM384_P" + i + "}", () => PCond(i), () => PToggle(i), 130 - i);
                }
                // 385: okno wyboru - linia gracza -> linia wodza z konsekwencja (okno) -> arm384_ack po ContinueConversation z przycisku okna
                starter.AddPlayerLine("arm384_pick", "arm384_offer", "arm384_pickw", "{=!}{ARM384_PICK}", PickCond, null, 131);
                starter.AddDialogLine("arm384_pickw", "arm384_pickw", "arm384_ack", "{=!}Name them, then.", null, OpenPick, 200);
                starter.AddPlayerLine("arm384_men", "arm384_offer", "arm384_ack", "{=!}{ARM384_MEN}", MenCond, () => { _menOn = !_menOn; }, 111);
                starter.AddPlayerLine("arm384_oath", "arm384_offer", "arm384_ack", "{=!}{ARM384_OATH}", OathCond, () => { _oath = !_oath; if (!HostWantOk()) _host = null; }, 110);
                // 385c: zakladnik - do 4 linii (109..106), jeden wybor; nieczynna z powodem (brak lochu / latki)
                for (int k = 0; k < ShowHost; k++)
                {
                    int i = k;
                    starter.AddPlayerLine("arm384_h" + i, "arm384_offer", "arm384_ack", "{=!}{ARM384_H" + i + "}", () => HCond(i), () => HToggle(i), 109 - i, (out TextObject why) => HClick(i, out why));
                }
                starter.AddPlayerLine("arm384_pall", "arm384_offer", "arm384_ack", "{=!}{ARM384_PALL}", PAllCond, PAllToggle, 121);
                starter.AddPlayerLine("arm384_g5", "arm384_offer", "arm384_ack", "{=!}Add 5,000 denars.", () => GoldCond(5000), () => { _gold += 5000; }, 120);
                starter.AddPlayerLine("arm384_g20", "arm384_offer", "arm384_ack", "{=!}Add 20,000 denars.", () => GoldCond(20000), () => { _gold += 20000; }, 119);
                starter.AddPlayerLine("arm384_g0", "arm384_offer", "arm384_ack", "{=!}{ARM384_G0}", G0Cond, () => { _gold = 0; }, 118);
                for (int k = 0; k < ShowFiefs; k++)
                {
                    int i = k;
                    starter.AddPlayerLine("arm384_f" + i, "arm384_offer", "arm384_ack", "{=!}{ARM384_F" + i + "}", () => FCond(i), () => FToggle(i), 117 - i);
                }
                starter.AddPlayerLine("arm384_done", "arm384_offer", "arm384_answer", "{=!}That is my offer. What say you?", DoneCond, Decide, 105);
                starter.AddPlayerLine("arm384_o_back", "arm384_offer", "lord_pretalk", "{=!}Never mind.", null, ClearTalk, 100);
                starter.AddDialogLine("arm384_ack", "arm384_ack", "arm384_offer", "{=!}{ARM384_SUM}", SumCond, null, 200);
                starter.AddDialogLine("arm384_yes", "arm384_answer", "close_window", "{=!}{ARM384_YES}", () => _verdict == 'Y', YesCons, 200);
                starter.AddDialogLine("arm384_ctr", "arm384_answer", "arm384_counter", "{=!}{ARM384_CTR}", () => _verdict == 'K', null, 199);
                starter.AddDialogLine("arm384_no", "arm384_answer", "lord_pretalk", "{=!}{ARM384_NO}", () => _verdict != 'Y' && _verdict != 'K', NoCons, 198);
                starter.AddPlayerLine("arm384_c_gold", "arm384_counter", "arm384_answer", "{=!}{ARM384_CG}", CGoldCond, () => { _gold += _ask; Decide(); }, 112);
                starter.AddPlayerLine("arm384_c_hero", "arm384_counter", "arm384_answer", "{=!}{ARM384_CH}", CHeroCond, () => { if (_askHero != null) _sel.Add(_askHero); Decide(); }, 111);
                starter.AddPlayerLine("arm384_c_more", "arm384_counter", "arm384_offer", "{=!}Let me weigh my offer again.", null, null, 110);
                starter.AddPlayerLine("arm384_c_no", "arm384_counter", "lord_pretalk", "{=!}No. That is my last word.", null, LastWord, 100);
                var s = Settings.Current;
                Log.Info(Tag + ": rozmowa wpieta (hero_main_options, priorytet " + AskPriority + ") | klucz " + (On ? "TAK" : "NIE") + ", rozejm / pakt " + Days + " dob"
                         + " | 385: okno wyboru " + (KPick ? "TAK" : "NIE") + ", moi ludzie " + (KMen ? "TAK" : "NIE") + ", przysiega " + (KOath ? "TAK" : "NIE") + ", rozmowa w polu " + (KField ? "TAK" : "NIE") + ", rozejm krolestw " + (KRealm ? "TAK" : "NIE") + HostStartLine()
                         + " | kolejka " + _queue.Count + (_offer != null ? ", oferta w menu" : "") + ", wpisy umow " + _entries.Count + ", przerwy " + _cool.Count + ".");
            }
            catch (Exception e) { Stumble("AddDialogs", e); }
        }

        internal static void AddMenus(CampaignGameStarter starter)
        {
            try
            {
                if (starter == null) return;
                starter.AddGameMenu(MenuId, "{=!}{ARM384_MENU}", MenuInit, GameMenu.MenuOverlayType.None);
                starter.AddGameMenuOption(MenuId, "arm384_ride", "{=!}Ride out under his word.", RideCond, RideCons, true);
            }
            catch (Exception e) { Stumble("AddMenus", e); }
        }

        private static string CoolKey(Settlement st, Hero h) { return (st != null ? st.StringId : FieldCool) + "|" + h.StringId; }

        /// <summary>Przerwa po odmowie; st == null - rozmowa w polu (385: klucz "@f|bohater", bez poczatku oblezenia).</summary>
        private static double CoolLeft(Settlement st, Hero h)
        {
            Cool c;
            if (h == null || h.StringId == null || (st != null && st.SiegeEvent == null) || !_cool.TryGetValue(CoolKey(st, h), out c)) return 0;
            if (st != null && Math.Abs(c.Start - st.SiegeEvent.SiegeStartTime.ToHours) > 0.05) return 0;
            double left = c.End - Today;
            return left > 0 ? left : 0;
        }

        /// <summary>Warunki rozmowy jak 374 (SiegeSurrender374.AskCond): gracz w oblezonej twierdzy (swojej albo jako wodz obrony), rozmowca to wodz obozu, wojna, bez bitwy.</summary>
        private static bool Parley(out Hero h, out Settlement st)
        {
            h = Hero.OneToOneConversationHero; st = null;
            var me = Hero.MainHero;
            var mp = MobileParty.MainParty;
            if (h == null || me == null || mp == null || h == me || !h.IsAlive || !h.IsLord || h.IsPrisoner || h.StringId == null) return false;
            st = mp.CurrentSettlement;
            if (st == null || !st.IsFortification || st.Town == null || st.SiegeEvent == null || mp.BesiegerCamp != null) return false;
            if (Settlement.CurrentSettlement != st) return false;
            var ev = st.SiegeEvent;
            if (!SiegeSurrender374.IsCampLeader(ev, h) || SiegeSurrender374.Excluded(h)) return false;
            if (me.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(h.MapFaction, me.MapFaction)) return false;
            if ((st.Party != null && st.Party.MapEvent != null) || mp.MapEvent != null) return false;
            if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
            Hero def = null;
            try { def = Campaign.Current.Models.EncounterModel.GetLeaderOfSiegeEvent(ev, BattleSideEnum.Defender); } catch { }
            if (def != me && st.OwnerClan != Clan.PlayerClan) return false;
            return true;
        }

        /// <summary>385: rozmowa w polu - gracz podjechal do druzyny lorda wrogiej frakcji (spotkanie na mapie, lord jest jej wodzem), bez osady, oblezenia i bitwy.</summary>
        private static bool Field(out Hero h)
        {
            h = Hero.OneToOneConversationHero;
            var me = Hero.MainHero;
            var mp = MobileParty.MainParty;
            if (h == null || me == null || mp == null || h == me || !h.IsAlive || !h.IsLord || h.IsPrisoner || h.StringId == null) return false;
            if (Campaign.Current == null || Campaign.Current.CurrentConversationContext != ConversationContext.PartyEncounter) return false;
            if (PlayerEncounter.Current == null || mp.CurrentSettlement != null || Settlement.CurrentSettlement != null || mp.BesiegerCamp != null || mp.MapEvent != null) return false;
            var ep = PlayerEncounter.EncounteredMobileParty;
            if (ep == null || ep.LeaderHero != h || ep.MapEvent != null || ep.BesiegerCamp != null || ep.CurrentSettlement != null) return false;
            if (SiegeSurrender374.Excluded(h)) return false;
            if (me.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(h.MapFaction, me.MapFaction)) return false;
            if (mp.Army != null && mp.Army.LeaderParty != mp) return false;
            return true;
        }

        /// <summary>Rozmowa 384: parley oblezenia (field = false, st = oblezona osada) albo 385 - rozmowa w polu (field = true, st = null).</summary>
        private static bool Talk(out Hero h, out Settlement st, out bool field)
        {
            field = false;
            if (Parley(out h, out st)) return true;
            st = null;
            if (KField && Field(out h)) { field = true; return true; }
            return false;
        }

        private static bool FieldPending(Hero h)
        {
            foreach (var d in _queue) if (d.Field && d.Lord == h.StringId) return true;
            return false;
        }

        private static bool AskCond()
        {
            try
            {
                if (!On) return false;
                ExpireOffer();
                if (_offer != null) return false;
                Hero h; Settlement st; bool field;
                if (!Talk(out h, out st, out field)) return false;
                if (field ? FieldPending(h) : (Pending(st) || SiegeSurrender374.Pending(st))) return false;
                int n = CountPrisoners(h);
                MBTextManager.SetTextVariable("ARM384_ASK", n > 0 ? "I hold lords of your realm in my dungeons. Let us talk terms." : "Let us talk terms - what would it take?");
                return true;
            }
            catch (Exception e) { Stumble("AskCond", e); return false; }
        }

        private static bool AskClick(out TextObject why)
        {
            why = null;
            try
            {
                Hero h; Settlement st; bool field;
                if (!Talk(out h, out st, out field)) return true;
                double cl = CoolLeft(st, h);
                if (cl > 0) { int n = (int)Math.Ceiling(cl); why = new TextObject("{=!}He has turned your terms down already. You may try again in " + (n <= 1 ? "1 day." : n.ToString(Inv) + " days.")); return false; }
                return true;
            }
            catch (Exception e) { Stumble("AskClick", e); return true; }
        }

        /// <summary>385: rozejm krolestw czynny, gdy gracz jest wladca K, rozmowca wladca krolestwa E w wojnie z K i pokoju nie blokuje kanon (CanonPeaceLock)
        /// ani wojna wymuszana przez ROT (poza para 330, gdzie pokoj gracza staje sie rozejmem WarTruce). Inaczej nieczynna z powodem.</summary>
        private static bool PeaceClick(out TextObject why)
        {
            why = null;
            try
            {
                string no = PeaceBar(_talk);
                if (no == null) return true;
                why = new TextObject("{=!}" + no);
            }
            catch (Exception e) { Stumble("PeaceClick", e); why = new TextObject("{=!}Not here."); }
            return false;
        }

        /// <summary>Powod (po angielsku), dla ktorego rozejmu krolestw nie da sie zawrzec z tym rozmowca; null - wolno.</summary>
        private static string PeaceBar(Hero lord)
        {
            var me = Hero.MainHero;
            var k = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
            if (k == null) return "You rule no realm. You can bargain only for yourself, your men and your lands.";
            if (k.Leader != me) return "Only " + (k.Leader != null ? Name(k.Leader) : "its ruler") + " can make peace for " + k.Name + ". You can bargain only for yourself, your men and your lands.";
            if (!KRealm) return "Peace between realms is sworn at court, not here. Here you can bargain only for yourself, your men and your lands.";
            var e = lord != null ? lord.MapFaction as Kingdom : null;
            if (e == null) return "He speaks for no realm.";
            if (e.Leader != lord) return "Only " + (e.Leader != null ? Name(e.Leader) : "its ruler") + " can swear peace for " + e.Name + ".";
            if (!k.IsAtWarWith(e)) return "There is no war between your realms.";
            if (CanonPeaceLock.Blocks(k, e)) return "This war cannot be ended yet.";
            if (!WarTruce.IsPair(k, e) && Prologue.RotWarForced(k, e)) return "This war cannot be ended yet.";
            // 385b: para 330 w fazie 1 / 2 (wesele ROT w toku, Polnoc walczy po Krwawych Godach) - WarTruce.OnPeace dalby rozejm 1 doby za pelna cene
            if (WarTruce.IsPair(k, e)) { string pw; int ph = CanonPeaceLock.Phase330(k, e, out pw); if (ph == 1 || ph == 2) return "This war cannot be ended yet."; }
            return null;
        }

        /// <summary>385b: dlugosc rozejmu krolestw - para 330 przy czynnej regule 330 wedlug WarTruce (WarTruceDays), inaczej zwykly wpis "K" na Days.</summary>
        private static int TruceDaysFor(IFaction a, IFaction b) { try { return WarTruce.IsPair(a, b) && WarTruce.On ? WarTruce.TruceDays : Days; } catch { return Days; } }

        /// <summary>385: bohaterowie rodu gracza i towarzysze w niewoli u rozmowcy: wladca frakcji - cala frakcja; inny lord - lochy i druzyny jego rodu, jego
        /// druzyna, jego armia i oboz oblezniczy.</summary>
        private static List<Hero> MenHeld(Hero lord, Settlement besieged)
        {
            var list = new List<Hero>();
            try
            {
                var pc = Clan.PlayerClan;
                var f = lord != null ? lord.MapFaction : null;
                if (pc == null || f == null) return list;
                bool all = Ruler(lord);
                var lp = lord.PartyBelongedTo;
                var camp = besieged != null && besieged.SiegeEvent != null ? besieged.SiegeEvent.BesiegerCamp : null;
                var seen = new HashSet<Hero>();
                var src = new List<Hero>(pc.Heroes);
                foreach (var c in pc.Companions) src.Add(c);
                foreach (var h in src)
                {
                    if (h == null || h == Hero.MainHero || !h.IsAlive || !h.IsPrisoner || h.StringId == null || !seen.Add(h)) continue;
                    var pb = h.PartyBelongedToAsPrisoner;
                    if (pb == null || pb.MapFaction != f) continue;
                    try { if (Prologue.HeldByScene(h) != null) continue; } catch { }   // jeniec sceny i tak nie wyjdzie (Prologue.CaptivityPrefix) - nie bierzemy za niego zaplaty
                    { var hr = HostFind(h); if (hr != null && hr.State == 'H') continue; }   // 385c: zakladnik ze straza odsiaduje termin - nie do wykupienia jako "moj czlowiek"
                    if (!all)
                    {
                        var mp = pb.MobileParty;
                        Clan owner = pb.IsSettlement && pb.Settlement != null ? pb.Settlement.OwnerClan : (mp != null ? mp.ActualClan : null);
                        bool mine = owner != null && owner == lord.Clan;
                        if (!mine && mp != null && lp != null) mine = mp == lp || (mp.Army != null && mp.Army == lp.Army);
                        if (!mine && mp != null && camp != null) mine = camp.HasInvolvedPartyForEventType(pb);
                        if (!mine) continue;
                    }
                    list.Add(h);
                }
            }
            catch (Exception e) { Stumble("MenHeld", e); }
            return list;
        }

        /// <summary>Okup za mojego czlowieka wedlug tej samej reguly co kurier okupu (Ransom178.PriceOf, bez warunku Lord); zapas - cena gry.</summary>
        private static double ManPrice(Hero h)
        {
            double a = 0;
            try { bool bc; double dd; a = Ransom178.PriceOf(h, out bc, out dd); } catch (Exception e) { Stumble("ManPrice.Own", e); }
            if (a <= 0) { try { a = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(h.CharacterObject, null); } catch (Exception e) { Stumble("ManPrice.Game", e); a = 1000; } }
            return Math.Max(0, a);
        }

        private static string MenNames()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _men.Count && i < 2; i++) { if (i > 0) sb.Append(", "); sb.Append(Name(_men[i])); }
            if (_men.Count > 2) sb.Append(" (+" + (_men.Count - 2) + ")");
            return sb.ToString();
        }

        private static bool OathAllowed(char want) { return want == 'L' || want == 'P' || want == 'M'; }

        /// <summary>Wartosc przysiegi gracza dla wodza: miara gry (NoAttackBarterable: 0.1 x wartosc wojskowa druzyny za 5 dob) x sqrt(N/5); Honor gracza < 0 - pol.</summary>
        private static double OathValue(bool surety)
        {
            double mil = 0;
            try { mil = Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(MobileParty.MainParty); } catch (Exception e) { Stumble("OathValue", e); }
            // 385b: gracz juz zwiazany (r dob zostalo) sprzedaje tylko przedluzenie: sqrt((r+N)/5) - sqrt(r/5), nie druga pelna wartosc
            double r = 0;
            try { var f = _talk != null ? _talk.MapFaction : null; if (f != null && f.NotAttackableByPlayerUntilTime.IsFuture) r = Math.Max(0, f.NotAttackableByPlayerUntilTime.ToDays - CampaignTime.Now.ToDays); } catch (Exception e) { Stumble("OathValue.Left", e); }
            double v = 0.1 * mil * (Math.Sqrt((r + Days) / 5.0) - Math.Sqrt(r / 5.0));
            if (!surety && Trait(Hero.MainHero, DefaultTraits.Honor) < 0) v *= 0.5;   // 385c: z zakladnikiem slowo ma porecznie - bez kary
            return Math.Max(0, v);
        }

        /// <summary>Jency-bohaterowie frakcji wodza u rodu gracza: lochy twierdz rodu gracza i druzyna gracza. inSt - w lochu oblezonej osady.</summary>
        private static List<Hero> Prisoners(Hero lord, Settlement besieged, Dictionary<Hero, bool> inSt)
        {
            var list = new List<Hero>();
            var seen = new HashSet<Hero>();
            var f = lord != null ? lord.MapFaction : null;
            if (f == null) return list;
            Action<TaleWorlds.CampaignSystem.Roster.TroopRoster, bool> scan = (r, here) =>
            {
                if (r == null) return;
                for (int i = 0; i < r.Count; i++)
                {
                    var c = r.GetCharacterAtIndex(i);
                    var h = c != null && c.IsHero ? c.HeroObject : null;
                    if (h == null || !h.IsAlive || !h.IsPrisoner || h.MapFaction != f || h == Hero.MainHero || !seen.Add(h)) continue;
                    list.Add(h);
                    if (inSt != null) inSt[h] = here;
                }
            };
            // 384b: druzyna gracza w oblezonej twierdzy dzieli jej los (mury padna - jency z druzyny tez wolni), wiec ten sam rabat co loch tej twierdzy
            bool partyHere = besieged != null && MobileParty.MainParty != null && MobileParty.MainParty.CurrentSettlement == besieged;
            try { scan(PartyBase.MainParty != null ? PartyBase.MainParty.PrisonRoster : null, partyHere); } catch (Exception e) { Stumble("Prisoners.Party", e); }
            try
            {
                var pc = Clan.PlayerClan;
                if (pc != null)
                    foreach (var s in pc.Settlements)
                        if (s != null && s.IsFortification && s.Party != null) scan(s.Party.PrisonRoster, s == besieged);
            }
            catch (Exception e) { Stumble("Prisoners.Dungeons", e); }
            return list;
        }

        private static int CountPrisoners(Hero lord) { return Prisoners(lord, null, null).Count; }

        /// <summary>Jeniec nadal u rodu gracza (druzyna gracza albo loch twierdzy rodu gracza).</summary>
        private static bool Held(Hero h)
        {
            if (h == null || !h.IsAlive || !h.IsPrisoner) return false;
            var pb = h.PartyBelongedToAsPrisoner;
            if (pb == null) return false;
            if (pb == PartyBase.MainParty) return true;
            return pb.IsSettlement && pb.Settlement != null && pb.Settlement.OwnerClan == Clan.PlayerClan;
        }

        private static bool Kin(Hero a, Hero b)
        {
            try
            {
                if (a == null || b == null) return false;
                if (a.Clan != null && a.Clan == b.Clan) return true;
                return a.Father == b || a.Mother == b || b.Father == a || b.Mother == a || a.Spouse == b
                       || (a.Father != null && a.Father == b.Father) || (a.Mother != null && a.Mother == b.Mother);
            }
            catch { return false; }
        }

        /// <summary>Wartosc jenca dla wodza (denary, bez rabatu oblezonej twierdzy i Mercy - te w Evaluate).</summary>
        private static double WorthOf(Hero lord, Hero h)
        {
            double w = Ransom178.PriceFor(h);
            if (w <= 0) { try { w = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(h.CharacterObject, null); } catch { w = 1000; } }
            if (Ransom178.IsRulerOrHeir(h)) w *= 3.0;
            else if (Kin(lord, h)) w *= 1.5;
            int rel = 0; try { rel = lord.GetRelation(h); } catch { }
            w *= Math.Max(0.5, Math.Min(1.5, 1.0 + rel / 100.0));
            return Math.Max(0, w);
        }

        private static double FiefValue(Settlement s, Hero lord)
        {
            try { return Math.Max(0, Campaign.Current.Models.SettlementValueModel.CalculateSettlementValueForFaction(s, lord.Clan)); }
            catch { return 0; }
        }

        private static void Begin()
        {
            try
            {
                ClearTalk();
                Hero h; Settlement st; bool field;
                if (!Talk(out h, out st, out field)) return;
                _talk = h; _st = st; _field = field;
                if (KMen) { foreach (var m in MenHeld(h, st)) { _men.Add(m); _menCost += ManPrice(m); } }
                HostBegin(h);   // 385c
                foreach (var p in Prisoners(h, st, _inSt)) { _pris.Add(p); _worth[p] = WorthOf(h, p); }
                _pris.Sort((a, b) => _worth[b].CompareTo(_worth[a]));
                var pc = Clan.PlayerClan;
                if (pc != null)
                    foreach (var s in pc.Settlements)
                        if (s != null && s.IsFortification && s != st && s.SiegeEvent == null && s.Party != null && s.Party.MapEvent == null) _fiefs.Add(s);
                _fiefs.Sort((a, b) => FiefValue(b, h).CompareTo(FiefValue(a, h)));   // 384b: najcenniejsze pierwsze (dotad 3 najtansze)
                if (_fiefs.Count > ShowFiefs) _fiefs.RemoveRange(ShowFiefs, _fiefs.Count - ShowFiefs);
            }
            catch (Exception e) { Stumble("Begin", e); }
        }

        private static bool OpenCond()
        {
            try
            {
                var sb = new StringBuilder("Speak, then. What would you have of me");
                sb.Append(_pris.Count > 0 ? ", and what are my lords worth to you?" : "?");
                int honor = _talk != null ? Trait(_talk, DefaultTraits.Honor) : 0;
                if (_talk != null && (SiegeSurrender374.IsLiar(_talk) || honor < 0)) sb.Append(" (Men say his word is worth little.)");
                else if (honor > 0) sb.Append(" (Men say he keeps his word.)");
                MBTextManager.SetTextVariable("ARM384_OPEN", sb.ToString());
            }
            catch (Exception e) { Stumble("OpenCond", e); MBTextManager.SetTextVariable("ARM384_OPEN", "Speak."); }
            return true;
        }

        private static bool WantLCond()
        {
            if (_talk == null || _st == null) return false;
            MBTextManager.SetTextVariable("ARM384_WL", "Lift the siege and march away, and swear not to come back to " + Name(_st) + " for " + Days + " days.");
            return true;
        }

        private static bool WantACond()
        {
            if (_talk == null) return false;
            if (_field)
            {
                MBTextManager.SetTextVariable("ARM384_WA", "Swear that your " + (Ruler(_talk) ? "realm" : "house") + " will not raise a hand against my lands or my men for " + Days + " days. I swear the same for mine.");
                return true;
            }
            if (_st == null) return false;
            MBTextManager.SetTextVariable("ARM384_WA", "Lift the siege, and swear that your realm will not raise a hand against my lands or my men for " + Days + " days. I swear the same for mine.");
            return true;
        }

        private static bool WantPCond()
        {
            if (_talk == null || _st == null) return false;
            MBTextManager.SetTextVariable("ARM384_WP", "Let my men and me ride out of " + Name(_st) + " unharmed, on your word of honour. The keep and its garrison stay.");
            return true;
        }

        private static bool WantMCond()
        {
            if (_talk == null || !_field || !KMen || _men.Count == 0) return false;
            MBTextManager.SetTextVariable("ARM384_WM", "Free my people you hold: " + MenNames() + ".");
            return true;
        }

        private static void SetWant(char w) { _want = w; _round = 0; _verdict = '\0'; if (w == 'T' || w == 'A') _oath = false; if (w == 'T') _menOn = false; if (!HostWantOk()) _host = null; }

        private static bool WforCond()
        {
            string t = _want == 'T' ? "Peace between our realms is no small thing. What would you give for it?"
                     : _want == 'M' ? "Your people are worth silver to me. What would you give for them?"
                     : _want == 'P' ? "You ask me to let the prize walk out of the trap. What would you give for it?"
                     : _want == 'A' ? "A long oath for a whole realm. What would you give for it?"
                     : _field ? "What would you give for it?" : "You ask me to give up a siege half won. What would you give for it?";
            MBTextManager.SetTextVariable("ARM384_WFOR", t);
            return true;
        }

        private static string Rank(Hero h)
        {
            try
            {
                if (h.MapFaction is Kingdom k && k.Leader == h) return "the king";
                if (Ransom178.IsRulerOrHeir(h)) return "of the royal house";
                if (_talk != null && Kin(_talk, h)) return "your kin";
                if (h.Clan != null && h.Clan.Leader == h) return "head of " + h.Clan.Name;
                return h.Clan != null ? "of " + h.Clan.Name : "lord";
            }
            catch { return "lord"; }
        }

        private static bool PCond(int i)
        {
            try
            {
                if (i >= _pris.Count || KPick || _want == 'T') return false;   // 385: z kluczem okna - jedna linia i okno
                var h = _pris[i];
                bool on = _sel.Contains(h);
                MBTextManager.SetTextVariable("ARM384_P" + i, (on ? "Take back " : "Offer ") + Name(h) + " (" + Rank(h) + ", worth some " + N(_worth[h]) + " denars to you" + (_inSt.ContainsKey(h) && _inSt[h] ? ", held here" : "") + ").");
                return true;
            }
            catch (Exception e) { Stumble("PCond", e); return false; }
        }

        private static void PToggle(int i)
        {
            if (i >= _pris.Count) return;
            var h = _pris[i];
            if (!_sel.Remove(h)) _sel.Add(h);
        }

        // ------------------------------------------------------------ 385: okno wyboru jencow, moi ludzie, przysiega
        private static bool PickCond()
        {
            if (!KPick || _pris.Count == 0 || _want == 'T' || _sel.Count >= _pris.Count) return false;   // 385b: okno doklada - wszyscy nazwani = nie ma kogo
            MBTextManager.SetTextVariable("ARM384_PICK", (_sel.Count > 0 ? "Let me name more lords I would give up (" : "Let me name the lords I would give up (") + _sel.Count + " of " + _pris.Count + " named).");
            return true;
        }

        /// <summary>Konsekwencja linii wodza "Name them, then.": okno z lista WSZYSTKICH jencow (wzor gry: okno z konsekwencji linii NPC, powrot
        /// ContinueConversation z przycisku). Blad okna - rozmowa idzie dalej zwyklym kliknieciem (linia wodza stoi, nastepna to arm384_ack).</summary>
        private static void OpenPick()
        {
            try
            {
                var talk = _talk;
                var rows = new List<InquiryElement>();
                foreach (var h in _pris)
                {
                    if (h.StringId == null || _sel.Contains(h)) continue;   // 385b: tylko jeszcze nie nazwani - zaznaczeni sa DOKLADANI do wyboru
                    bool here = _inSt.ContainsKey(h) && _inSt[h];
                    rows.Add(new InquiryElement(h.StringId, Name(h) + " - " + Rank(h) + ", worth some " + N(_worth[h]) + (here ? ", held here" : ""), null, true,
                                                Name(h) + (h.Clan != null ? " of " + h.Clan.Name : "") + ". Worth some " + N(_worth[h]) + " denars to " + Name(talk) + (here ? "; held within these walls, so worth less to a man who means to take them." : ".")));
                }
                if (rows.Count == 0) return;
                int shown = rows.Count;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Lords to offer",
                    "Tick the lords you would give up to " + Name(talk) + " (dearest first). The lords you tick are added to those you have already named; Back changes nothing.",
                    rows, false, 0, shown, "Offer these", "Back",
                    delegate (List<InquiryElement> picked) { PickDone(picked, talk, shown); },
                    delegate (List<InquiryElement> _) { PickDone(null, talk, shown); }), false);   // 385b: bez pauzy stanu gry - wzor gry dla okna z rozmowy
            }
            catch (Exception e) { Stumble("OpenPick", e); }
        }

        private static void PickDone(List<InquiryElement> picked, Hero talk, int shown)
        {
            try
            {
                if (talk == null || !ReferenceEquals(talk, _talk)) return;   // rozmowa juz inna albo zamknieta
                int added = 0;
                if (picked != null)
                {
                    var ids = new HashSet<string>();
                    foreach (var p in picked) { var id = p != null ? p.Identifier as string : null; if (id != null) ids.Add(id); }
                    foreach (var h in _pris) if (h.StringId != null && ids.Contains(h.StringId) && !_sel.Contains(h)) { _sel.Add(h); added++; }   // 385b: doklada
                }
                _dPick++;
                Log.Info(Tag + ": okno wyboru: pokazano " + shown + ", " + (picked != null ? "dolozono " + added + ", wybranych " + _sel.Count : "Back - bez zmian (wybranych " + _sel.Count + ")") + ".");
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (cm != null && cm.IsConversationFlowActive) cm.ContinueConversation();
            }
            catch (Exception e) { Stumble("PickDone", e); }
        }

        private static bool MenCond()
        {
            if (!KMen || _talk == null || _men.Count == 0 || _want == 'M' || _want == 'T' || _want == '\0') return false;
            MBTextManager.SetTextVariable("ARM384_MEN", _menOn ? "Leave my people out of it."
                : "And free my people you hold: " + MenNames() + " (a ransom of some " + N(_menCost) + " denars by his reckoning).");
            return true;
        }

        private static bool OathCond()
        {
            if (!KOath || _talk == null || _talk.MapFaction == null || !OathAllowed(_want)) return false;
            MBTextManager.SetTextVariable("ARM384_OATH", _oath ? "Take back my oath."
                : "I swear not to raise arms against " + Realm(_talk.MapFaction) + " for " + Days + " days. (Until then you cannot attack, besiege or raid them, and you cannot join any army of your own realm while it is at war with them - unless they strike at you first.)");
            return true;
        }

        private static bool PAllCond()
        {
            if (_pris.Count == 0 || _want == 'T') return false;
            bool all = _sel.Count == _pris.Count;
            double sum = 0; foreach (var h in _pris) sum += _worth[h];
            MBTextManager.SetTextVariable("ARM384_PALL", all ? "Take back all the lords." : "Offer all the lords of your realm I hold (" + _pris.Count + ", worth some " + N(sum) + " denars).");
            return true;
        }

        private static void PAllToggle()
        {
            if (_sel.Count == _pris.Count) _sel.Clear();
            else foreach (var h in _pris) _sel.Add(h);
        }

        private static bool GoldCond(int add) { var me = Hero.MainHero; return me != null && me.Gold >= _gold + add; }

        private static bool G0Cond()
        {
            if (_gold <= 0) return false;
            MBTextManager.SetTextVariable("ARM384_G0", "Take back the gold (" + N(_gold) + " denars).");
            return true;
        }

        private static bool FCond(int i)
        {
            if (i >= _fiefs.Count || _talk == null) return false;
            var s = _fiefs[i];
            bool on = _selF.Contains(s);
            MBTextManager.SetTextVariable("ARM384_F" + i, (on ? "Take back " : "Offer ") + Name(s) + (s.IsTown ? " (town" : " (castle") + ", worth some " + N(FiefValue(s, _talk)) + " to you" + (on ? ")." : "; its dungeon goes with it)."));
            return true;
        }

        private static void FToggle(int i)
        {
            if (i >= _fiefs.Count) return;
            var s = _fiefs[i];
            if (!_selF.Remove(s)) _selF.Add(s);
        }

        private static bool DoneCond() { return _talk != null && (_st != null || _field) && _want != '\0' && (_want == 'T' || _sel.Count > 0 || _gold > 0 || _selF.Count > 0 || (_oath && OathAllowed(_want)) || HostOn); }

        private static string OfferText()
        {
            var parts = new List<string>();
            if (_want == 'T') { if (_pris.Count > 0) parts.Add("all " + _pris.Count + " lords of your realm I hold (peace opens the dungeons on both sides)"); }
            else if (_sel.Count > 0) parts.Add(_sel.Count == 1 ? Name(First(_sel)) : _sel.Count + " lords");
            if (_gold > 0) parts.Add(N(_gold) + " denars");
            foreach (var s in _selF) parts.Add(Name(s));
            if (_oath && OathAllowed(_want)) parts.Add("my oath not to raise arms against you for " + Days + " days");
            if (HostOn) parts.Add(Name(_host) + " as a hostage for " + Days + " days");   // 385c
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "nothing";
        }

        private static Hero First(HashSet<Hero> s) { foreach (var h in s) return h; return null; }

        private static bool SumCond()
        {
            try
            {
                var e = _talk != null && (_field || (_st != null && _st.SiegeEvent != null)) ? Evaluate(_talk, _st, _want) : null;
                string lean = e == null ? "" : e.V >= e.C * e.Thr ? " (He looks at it long. It may be enough.)" : e.V >= e.C * e.Lo ? " (He frowns. It is close.)" : " (He shakes his head. It is far from enough.)";
                string men = _menOn && _men.Count > 0 && _want != 'M' && _want != 'T' ? " You ask for your people as well (" + _men.Count + ")." : "";
                MBTextManager.SetTextVariable("ARM384_SUM", "On the table: " + OfferText() + "." + men + lean);
            }
            catch (Exception ex) { Stumble("SumCond", ex); MBTextManager.SetTextVariable("ARM384_SUM", "Go on."); }
            return true;
        }

        // ------------------------------------------------------------ ocena
        private static double PTake(Settlement st, out string parts)
        {
            double camp, walls; SiegeSurrender374.Strength(st, out camp, out walls);
            int held, lost; SiegeSurrender374.Assaults(st, out held, out lost);
            int menCamp = 0;
            try
            {
                var ev = st.SiegeEvent;
                if (ev != null && ev.BesiegerCamp != null)
                    foreach (var pb in ev.BesiegerCamp.GetInvolvedPartiesForEventType()) if (pb != null && pb.MemberRoster != null) menCamp += pb.MemberRoster.TotalManCount;
            }
            catch { }
            double food = 999;
            try { float ch = st.Town.FoodChange; if (ch < 0) food = st.Town.FoodStocks / -ch; } catch { }
            double R = walls > 1 ? camp / walls : 99;
            double lostFrac = Math.Min(1.0, lost / Math.Max(1.0, menCamp));
            double p = 0.5 + 0.15 * Math.Log(Math.Max(0.01, R / 4.0), 2) - 0.10 * held - 0.5 * lostFrac + (food < 14 ? 0.15 : food > 60 ? -0.10 : 0);
            p = Math.Max(0.05, Math.Min(0.95, p));
            parts = "p " + p.ToString("0.00", Inv) + " (R " + R.ToString("0.0", Inv) + ", odparte " + held + ", straty " + lost + "/" + menCamp + ", jedzenie " + (food >= 999 ? "bez ubytku" : F(food) + " d") + ")";
            return p;
        }

        private static Ev Evaluate(Hero lord, Settlement st, char want)
        {
            var e = new Ev();
            var me = Hero.MainHero;
            bool siege = st != null && st.SiegeEvent != null;   // 385: w polu bez oblezenia - PTake 0, bez rabatu "held here"
            string pp = "pole (bez oblezenia)";
            if (siege) e.PTake = PTake(st, out pp);
            int honor = Trait(lord, DefaultTraits.Honor), calc = Trait(lord, DefaultTraits.Calculating), mercy = Trait(lord, DefaultTraits.Mercy), valor = Trait(lord, DefaultTraits.Valor);
            // oferta
            foreach (var h in (want == 'T' ? (IEnumerable<Hero>)_pris : _sel))   // 385: pokoj otwiera lochy - licza sie wszyscy jency krolestwa
            {
                double w; if (!_worth.TryGetValue(h, out w)) continue;
                bool here; if (_inSt.TryGetValue(h, out here) && here) w *= 1.0 - 0.7 * e.PTake;   // i tak ich uwolni, gdy zdobedzie mury
                e.Vp += w;
            }
            e.Vp *= Math.Max(0.7, 1.0 + 0.1 * mercy);
            e.Vg = _gold;
            foreach (var s in _selF) e.Vf += FiefValue(s, lord);
            bool surety = HostOn;   // 385c
            if (_oath && OathAllowed(want)) e.Vo = OathValue(surety);
            if (surety) e.Vh = HostValue();
            e.V = (e.Vp + e.Vg + e.Vf + e.Vo + e.Vh) * (honor > 0 ? 1.0 + 0.05 * honor : 1.0);
            // koszt zadania
            double stVal = siege ? FiefValue(st, lord) : 0;
            double cLift = e.PTake * stVal * Math.Max(0.5, 1.0 + 0.15 * valor);
            double c;
            string cp;
            int rel = 0; try { rel = lord.GetRelation(me); } catch { }
            if (want == 'T')
            {
                // 385: ta sama miara co barter pokoju gry (PeaceBarterable -> DiplomacyModel.GetScoreOfDeclaringPeaceForClan z BK i naszymi postfiksami)
                double val = 0;
                var k = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                var ek = lord.MapFaction as Kingdom;
                try { if (k != null && ek != null) val = new TaleWorlds.CampaignSystem.BarterSystem.Barterables.PeaceBarterable(me, k, ek, CampaignTime.Days(TruceDaysFor(k, ek))).GetUnitValueForFaction(ek); }
                catch (Exception ex) { Stumble("Evaluate.Peace", ex); val = -1000000; }
                c = -val;
                cp = "pokoj: koszt z barteru gry " + N(-val) + " (wartosc pokoju dla " + (ek != null ? ek.StringId : "-") + " " + N(val) + ")";
            }
            else if (want == 'M') { c = 0; cp = "samo uwolnienie moich ludzi"; }
            else if (!siege)
            {
                // 385: pakt w polu - bez odstapienia; udzial: wladca cala frakcja, inny lord - sila rodu do sily frakcji (0.15..1)
                double fiefs = 0, mil = 0, share = 1.0;
                var pc = Clan.PlayerClan;
                if (pc != null) foreach (var s in pc.Settlements) if (s != null && s.IsFortification) fiefs += FiefValue(s, lord);
                try { mil = Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(MobileParty.MainParty); } catch { }
                if (!Ruler(lord))
                {
                    try { float fs = lord.MapFaction.CurrentTotalStrength, cs = lord.Clan.CurrentTotalStrength; share = fs > 1f ? Math.Max(0.15, Math.Min(1.0, cs / fs)) : 1.0; } catch { share = 0.15; }
                }
                c = (Days / 60.0) * 0.1 * (fiefs * share + mil);
                cp = "pakt w polu: " + Days + " d x 0.1 x (twierdze gracza " + N(fiefs) + " x udzial " + share.ToString("0.00", Inv) + " + wojsko " + N(mil) + ")";
            }
            else if (want == 'P')
            {
                double ransom = PlayerPrice(me, lord), mil = 0;   // 384b: Ransom178.PriceFor(MainHero) = 0 (Lord() wyklucza gracza)
                try { mil = Campaign.Current.Models.ValuationModel.GetMilitaryValueOfParty(MobileParty.MainParty); } catch { }
                c = e.PTake * (ransom + 0.3 * mil) + 2000;
                cp = "przejscie: p x (okup gracza " + N(ransom) + " + 0.3 x wojsko " + N(mil) + ") + 2000";
            }
            else
            {
                c = cLift;
                cp = "odstapienie: p x osada " + N(stVal) + " x Valor";
                if (want == 'A')
                {
                    double other = 0;
                    var pc = Clan.PlayerClan;
                    if (pc != null) foreach (var s in pc.Settlements) if (s != null && s.IsFortification && s != st) other += FiefValue(s, lord);
                    double add = (Days / 60.0) * 0.1 * other;
                    c += add;
                    cp += " + pakt " + N(add) + " (pozostale twierdze gracza " + N(other) + ", " + Days + " d)";
                }
            }
            if (want != 'T') c *= 1.0 - Math.Max(-50, Math.Min(50, rel)) / 200.0;
            if (_menOn && _men.Count > 0 && want != 'T') { c += _menCost; cp += " + moi ludzie " + _men.Count + " koszt " + N(_menCost); }   // 385: okup bez rabatu relacji
            e.C = Math.Max(1000, c);
            e.Thr = calc > 0 ? 1.0 : calc < 0 ? 0.9 : 0.95;
            e.Lo = calc > 0 ? 0.8 : 0.7;
            e.Why = !siege ? 'T' : e.PTake >= 0.8 ? 'S' : (mercy < 0 || valor > 0) ? 'C' : 'T';
            e.Parts = pp + " | oferta: jency " + N(e.Vp) + " (" + (want == 'T' ? _pris.Count : _sel.Count) + ", Mercy " + mercy + "), zloto " + N(e.Vg) + ", ziemia " + N(e.Vf) + " (" + _selF.Count + ")" + (e.Vo > 0 ? ", przysiega " + N(e.Vo) + (surety ? " (z poreczeniem)" : "") : "") + (surety ? ", zakladnik " + N(e.Vh) + " (" + Name(_host) + ", k " + F(_hostK.ContainsKey(_host) ? _hostK[_host] : 1.0) + ")" : "") + ", Honor " + honor
                      + " -> V " + N(e.V) + " | koszt: " + cp + ", relacja " + rel + " -> C " + N(e.C) + " | prog " + e.Thr.ToString("0.00", Inv) + " (Calc " + calc + "), kontroferta od " + e.Lo.ToString("0.00", Inv) + ", Valor " + valor;
            return e;
        }

        /// <summary>384b: okup gracza dla wodza - wieksza z: cena wedlug majatku (Ransom178.PriceOf, bez warunku Lord) i cena gry (RansomValueCalculationModel,
        /// jak SafePassageBarterable).</summary>
        private static double PlayerPrice(Hero me, Hero lord)
        {
            double a = 0, b = 0;
            try { bool bc; double dd; a = Ransom178.PriceOf(me, out bc, out dd); } catch (Exception e) { Stumble("PlayerPrice.Own", e); }
            try { b = Campaign.Current.Models.RansomValueCalculationModel.PrisonerRansomValue(me.CharacterObject, lord); } catch (Exception e) { Stumble("PlayerPrice.Game", e); }
            return Math.Max(0, Math.Max(a, b));
        }

        private static void Decide()
        {
            try
            {
                var h = _talk; var st = _st;
                _askHero = null; _ask = 0;
                if (h == null || _want == '\0' || (!_field && (st == null || st.SiegeEvent == null)) || (_want == 'T' && PeaceBar(h) != null)) { _verdict = 'N'; MBTextManager.SetTextVariable("ARM384_NO", "Not now."); return; }
                _round++;
                _ev = Evaluate(h, st, _want);
                double need = _ev.C * _ev.Thr;
                if (_ev.V >= need)
                {
                    _verdict = 'Y';
                    string who = Ruler(h) ? "realm" : "house";
                    string yes = _want == 'T' ? "So be it. Let there be peace between our realms - for " + TruceDaysFor(Hero.MainHero.MapFaction, h.MapFaction) + " days no banner of mine marches on yours. You have my word."
                               : _want == 'M' ? "So be it. Your people will be on the road to you before nightfall. You have my word."
                               : _field ? "So be it. For " + Days + " days my " + who + " will leave your lands and your men in peace. You have my word."
                               : _want == 'P' ? "Go, then. Leave my lords at the gate, and no man of mine will lay a hand on you. You have my word."
                               : _want == 'A' ? "So be it. Free my lords, and we march at dawn - and for " + Days + " days my realm will leave your lands and your men in peace. You have my word."
                               : "So be it. Free my lords, and we march at dawn. For " + Days + " days no banner of mine comes back to " + Name(st) + ". You have my word.";
                    if (_menOn && _men.Count > 0 && _want != 'M' && _want != 'T') yes += " Your people go free with it.";
                    if (HostOn) yes += " " + Name(_host) + " will be kept" + (_hostHall != null ? " at " + Name(_hostHall) : "") + " and come to no harm while you keep faith.";   // 385c
                    MBTextManager.SetTextVariable("ARM384_YES", yes);
                    return;
                }
                if (_ev.V >= _ev.C * _ev.Lo && _round < MaxRounds)
                {
                    double gap = need - _ev.V;
                    int honor = Trait(h, DefaultTraits.Honor);
                    double mul = honor > 0 ? 1.0 + 0.05 * honor : 1.0;
                    _ask = (int)(Math.Ceiling(gap / mul / 500.0) * 500);
                    // jeszcze jeden jeniec: najtanszy, ktory sam domyka roznice
                    double best = double.MaxValue;
                    foreach (var p in _pris)
                    {
                        if (_want == 'T' || _sel.Contains(p)) continue;   // 385: przy pokoju wszyscy jency juz policzeni
                        double w = _worth[p];
                        bool here; if (_inSt.TryGetValue(p, out here) && here) w *= 1.0 - 0.7 * _ev.PTake;
                        w *= Math.Max(0.7, 1.0 + 0.1 * Trait(h, DefaultTraits.Mercy)) * mul;
                        if (w >= gap && w < best) { best = w; _askHero = p; }
                    }
                    _verdict = 'K';
                    var sb = new StringBuilder("Not enough. ");
                    sb.Append("Add " + N(_ask) + " denars and we have a deal.");
                    if (_askHero != null) sb.Append(" Or give me " + Name(_askHero) + " as well.");
                    MBTextManager.SetTextVariable("ARM384_CTR", sb.ToString());
                    LogDecision(h, st, _ev, "KONTROFERTA runda " + _round + " (+" + _ask + " zlota" + (_askHero != null ? " albo jeniec " + Name(_askHero) : "") + ")", 0);
                    return;
                }
                _verdict = 'N';
                string no = _ev.Why == 'S' ? "Why bargain for what I will take? When your walls fall, your dungeon opens to me."
                          : _ev.Why == 'C' ? "You insult me. I will have my lords back and your banner in the mud besides."
                          : "Not for so little. Think on it, and we may speak again.";
                MBTextManager.SetTextVariable("ARM384_NO", no);
                Refuse(h, st, "ODMOWA (runda " + _round + ")");
            }
            catch (Exception e) { Stumble("Decide", e); _verdict = 'N'; MBTextManager.SetTextVariable("ARM384_NO", "Not now."); }
        }

        private static void Refuse(Hero h, Settlement st, string verdict)
        {
            try
            {
                if (h != null && st != null && st.SiegeEvent != null) _cool[CoolKey(st, h)] = new Cool { Start = st.SiegeEvent.SiegeStartTime.ToHours, End = Today + CooldownDays };
                else if (h != null && st == null && _field && h.StringId != null) _cool[CoolKey(null, h)] = new Cool { Start = 0, End = Today + CooldownDays };
                if (_ev != null && h != null && (st != null || _field)) LogDecision(h, st, _ev, verdict + ", przerwa " + F(CooldownDays) + " d", 0);
            }
            catch (Exception e) { Stumble("Refuse", e); }
        }

        private static bool CGoldCond()
        {
            var me = Hero.MainHero;
            if (_ask <= 0 || me == null || me.Gold < _gold + _ask) return false;
            MBTextManager.SetTextVariable("ARM384_CG", "Agreed - " + N(_ask) + " denars more.");
            return true;
        }

        private static bool CHeroCond()
        {
            if (_askHero == null) return false;
            MBTextManager.SetTextVariable("ARM384_CH", "Then take " + Name(_askHero) + " as well.");
            return true;
        }

        private static void LastWord()
        {
            Refuse(_talk, _st, "ODMOWA (gracz nie dolozyl)");
            try { Log.Player(Name(_talk) + " will not take your terms. You may try again in " + CooldownDays.ToString(Inv) + " days.", true); } catch { }
            ClearTalk();
        }

        private static void NoCons()
        {
            try { Log.Player(Name(_talk) + " refuses your terms. You may try again in " + CooldownDays.ToString(Inv) + " days.", true); } catch { }
            ClearTalk();
        }

        private static void YesCons()
        {
            try
            {
                var h = _talk; var st = _st;
                bool siege = st != null && st.SiegeEvent != null;
                if (h != null && (siege || _field) && _ev != null)
                {
                    double p = SiegeSurrender374.BetrayChance(h);
                    bool betray = p > 0 && MBRandom.RandomFloat < p;
                    var d = new Deal { St = siege ? st.StringId : "", Lord = h.StringId, Start = siege ? st.SiegeEvent.SiegeStartTime.ToHours : 0, Want = _want, Gold = _gold, Betray = betray, P = p, Day = Today, V = _ev.V, C = _ev.C, Days = Days };
                    if (_want == 'T') d.Days = TruceDaysFor(Hero.MainHero.MapFaction, h.MapFaction);
                    else foreach (var x in _sel) if (x.StringId != null) d.Pris.Add(x.StringId);   // 385: przy pokoju jencow nie oddajemy sami - lochy otwiera pokoj (PeaceHold)
                    if (_menOn && _want != 'T') foreach (var x in _men) if (x.StringId != null) d.Men.Add(x.StringId);
                    d.Oath = _oath && OathAllowed(_want);
                    if (HostOn && _host.StringId != null) d.Host = _host.StringId;   // 385c
                    if (!siege)
                    {
                        // 385: w polu - druzyna rozmowcy nie rzuca sie na gracza, zanim umowa wykona sie po wyjsciu ze spotkania
                        try { var lp = h.PartyBelongedTo; if (lp != null && lp.Ai != null) lp.Ai.SetDoNotAttackMainParty(3); } catch (Exception e) { Stumble("YesCons.NoAttack", e); }
                    }
                    foreach (var s in _selF) if (s.StringId != null) d.Fiefs.Add(s.StringId);
                    if (_want == 'P')
                    {
                        _offer = d;
                        // ta sama droga co gra po udanym barterze wolnego przejscia (SafePassageBarterable.Apply) i 374: nastepne menu po rozmowie, bez SwitchToMenu
                        Campaign.Current.GameMenuManager.SetNextMenu(MenuId);
                    }
                    else { _queue.Add(d); _armed = false; }
                    LogDecision(h, st, _ev, "ZGODA (" + WantName(_want) + ") | zdrada " + F(p * 100) + "%" + (betray ? " -> ZLAMIE SLOWO" : " -> dotrzyma"), p);
                }
            }
            catch (Exception e) { Stumble("YesCons", e); _offer = null; }
            ClearTalk();
        }

        private static void LogDecision(Hero h, Settlement st, Ev e, string verdict, double p)
        {
            try
            {
                int rel = 0; try { rel = h.GetRelation(Hero.MainHero); } catch { }
                var names = new List<string>();
                foreach (var x in _sel) { if (names.Count >= 12) { names.Add("..."); break; } names.Add(Name(x)); }
                Log.Info(Tag + ": dzien " + F(Today) + " | " + (st != null ? "oblezenie " + Name(st) + " [" + st.StringId + "]" : "pole (" + Realm(h.MapFaction) + ")") + " | wodz " + Name(h) + (Ruler(h) ? " - wladca" : "") + " (Honor " + Trait(h, DefaultTraits.Honor) + ", Calc " + Trait(h, DefaultTraits.Calculating)
                         + ", Mercy " + Trait(h, DefaultTraits.Mercy) + ", Valor " + Trait(h, DefaultTraits.Valor) + ", rel " + rel + (SiegeSurrender374.IsLiar(h) ? ", znany klamca" : "") + ")"
                         + " | zadanie " + (_field && _want == 'A' ? "pakt (pole)" : WantName(_want)) + " (" + (_want == 'T' ? TruceDaysFor(Hero.MainHero.MapFaction, h.MapFaction) : Days) + " d)" + (_menOn && _men.Count > 0 ? " + moi ludzie " + _men.Count + " [" + MenNames() + "]" : "")
                         + " | oferta: jency " + _sel.Count + " [" + string.Join(", ", names.ToArray()) + "], zloto " + _gold + ", ziemia " + _selF.Count + (_oath && OathAllowed(_want) ? ", przysiega gracza" : "") + (HostOn ? ", zakladnik " + Name(_host) : "")
                         + " | " + e.Parts + " | wynik V/C " + (e.C > 0 ? (e.V / e.C).ToString("0.00", Inv) : "-") + " -> " + verdict + ".");
            }
            catch (Exception ex) { Stumble("LogDecision", ex); }
        }

        // ------------------------------------------------------------ menu po rozmowie (P - swobodne przejscie)
        private static void ExpireOffer()
        {
            var o = _offer;
            if (o == null) return;
            try
            {
                if (Mission.Current != null) return;
                var cm = Campaign.Current != null ? Campaign.Current.ConversationManager : null;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                var ctx = Campaign.Current != null ? Campaign.Current.CurrentMenuContext : null;
                string menu = ctx != null && ctx.GameMenu != null ? ctx.GameMenu.StringId : null;
                if (menu == MenuId) return;
                var st = Settlement.Find(o.St);
                bool siegeGone = st == null || st.SiegeEvent == null || Math.Abs(st.SiegeEvent.SiegeStartTime.ToHours - o.Start) > 0.05;
                double age = Today - o.Day;
                if (!siegeGone && age <= 0.1) return;
                _offer = null;
                Log.Info(Tag + ": dzien " + F(Today) + " | " + (st != null ? Name(st) : o.St) + " - oferta przejscia PRZEPADLA (menu " + MenuId + " nie otwarte; czynne menu " + (menu ?? "-") + ").");
            }
            catch (Exception e) { Stumble("ExpireOffer", e); }
        }

        private static void MenuInit(MenuCallbackArgs args)
        {
            try
            {
                var st = _offer != null ? Settlement.Find(_offer.St) : MobileParty.MainParty.CurrentSettlement;
                var lord = _offer != null ? Hero.Find(_offer.Lord) : null;
                MBTextManager.SetTextVariable("ARM384_MENU", "A postern of " + Name(st) + " opens. " + (lord != null ? Name(lord) : "The besiegers' commander") + " waits beyond it for the lords you promised him.");
                string mesh = st != null && st.SettlementComponent != null ? st.SettlementComponent.WaitMeshName : null;
                args.MenuContext.SetBackgroundMeshName(string.IsNullOrEmpty(mesh) ? "wait_fallback" : mesh);
            }
            catch (Exception e) { Stumble("MenuInit", e); }
        }

        private static bool RideCond(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private static void RideCons(MenuCallbackArgs args)
        {
            var o = _offer;
            _offer = null;
            try
            {
                var st = o != null ? Settlement.Find(o.St) : null;
                var ev = st != null ? st.SiegeEvent : null;
                int n = 0;
                if (ev != null && ev.BesiegerCamp != null)
                    foreach (var pb in ev.BesiegerCamp.GetInvolvedPartiesForEventType())
                    {
                        var mp = pb != null ? pb.MobileParty : null;
                        if (mp == null || mp.Ai == null) continue;
                        mp.Ai.SetDoNotAttackMainParty(NoAttackHours); n++;
                    }
                if (o != null) { _queue.Add(o); _armed = false; }
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " - gracz wychodzi na slowo honoru | oboz nie atakuje " + n + " partii przez " + NoAttackHours + " h"
                         + (o == null ? " | BRAK umowy (wczytanie bez oferty?) - samo wyjscie" : " | wymiana w kolejce (wykonanie w takcie mapy)") + ".");
            }
            catch (Exception e) { Stumble("RideCons", e); }
            try
            {
                var main = MobileParty.MainParty;
                if (main.BesiegerCamp == null && PlayerEncounter.Current != null && main.CurrentSettlement != null)
                {
                    if (main.Army != null) main.Army = null;
                    PlayerSiege.FinalizePlayerSiege();
                    PlayerEncounter.LeaveSettlement();
                    PlayerEncounter.Finish();
                }
                else GameMenu.ExitToLast();
            }
            catch (Exception e) { Stumble("RideCons.Leave", e); }
        }

        // ------------------------------------------------------------ wykonanie (takt kampanii)
        internal static void Tick()
        {
            if (_offer != null) ExpireOffer();
            if (_relift.Count > 0) Relift();
            if (_queue.Count == 0) return;
            try
            {
                if (Campaign.Current == null || Mission.Current != null) return;
                var cm = Campaign.Current.ConversationManager;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MapEvent != null || Hero.MainHero.IsPrisoner) return;
                var d = _queue[0];
                if (d.Want == 'P')
                {
                    if (PlayerEncounter.Current != null || mp.CurrentSettlement != null) return;   // dopiero za brama
                }
                else if (d.Field)
                {
                    if (PlayerEncounter.Current != null) return;   // 385: dopiero po wyjsciu ze spotkania z druzyna rozmowcy
                }
                else
                {
                    var st0 = Settlement.Find(d.St);
                    if (SiegeSurrender374.CampBusy(st0) && Today - d.Day < 1.0) return;   // bitwa przy obozie - czekamy (najwyzej doba)
                }
                if (!_armed) { _armed = true; return; }
                _queue.RemoveAt(0);   // najpierw z kolejki - wyjatek nie powtorzy umowy
                _armed = false;
                Run(d);
            }
            catch (Exception e) { Stumble("Tick", e); }
        }

        private static void Drop(Deal d, Settlement st, string en, string pl)
        {
            Log.Info(Tag + ": dzien " + F(Today) + " | " + (st != null ? Name(st) : d.Field ? "pole" : d.St) + " - umowa PRZEPADLA (" + WantName(d) + "): " + pl + ".");
            if (!string.IsNullOrEmpty(en)) Log.Player(en, true);
        }

        /// <summary>Czy oferta nadal da sie oddac: jency u rodu gracza, zloto, ziemia rodu gracza bez oblezenia i bitwy. why - powod po polsku.</summary>
        private static bool Payable(Deal d, out string why)
        {
            why = null;
            var me = Hero.MainHero;
            foreach (var id in d.Pris) { var h = Hero.Find(id); if (!Held(h)) { why = "jeniec " + (h != null ? Name(h) : id) + " nie jest juz u rodu gracza"; return false; } }
            if (d.Gold > 0 && me.Gold < d.Gold) { why = "za malo zlota gracza (" + me.Gold + " < " + d.Gold + ")"; return false; }
            foreach (var id in d.Fiefs)
            {
                var s = Settlement.Find(id);
                if (s == null || s.OwnerClan != Clan.PlayerClan || s.SiegeEvent != null || (s.Party != null && s.Party.MapEvent != null)) { why = "ziemia " + (s != null ? Name(s) : id) + " nie do oddania"; return false; }
            }
            // 385c: zakladnik nadal w szeregu druzyny gracza (jak brakujacy jeniec - umowa przepada; przy P kara jak za niedotrzymanie)
            if (!string.IsNullOrEmpty(d.Host)) { var hh = Hero.Find(d.Host); if (!HostFit(hh)) { why = "zakladnik " + (hh != null ? Name(hh) : d.Host) + " nie jest juz w szeregu druzyny gracza"; return false; } }
            return true;
        }

        /// <summary>Zaplata: jency wolni (z wyboru gracza), zloto gracz -> wodz, ziemia przez barter gry. Zwraca opis do logu.</summary>
        private static string Pay(Deal d, Hero lord)
        {
            var me = Hero.MainHero;
            int freed = 0, fails = 0;
            var names = new List<string>();
            foreach (var id in d.Pris)
            {
                var h = Hero.Find(id);
                if (!Held(h)) { fails++; continue; }
                try { EndCaptivityAction.ApplyByReleasedByChoice(h, me); freed++; if (names.Count < 20) names.Add(Name(h)); }
                catch (Exception e) { fails++; Stumble("Pay.Free", e); }
            }
            int gold = 0;
            if (d.Gold > 0)
            {
                try { int g = Math.Min(d.Gold, me.Gold); GiveGoldAction.ApplyBetweenCharacters(me, lord, g, true); gold = g; }
                catch (Exception e) { Stumble("Pay.Gold", e); }
            }
            var ceded = new List<string>();
            foreach (var id in d.Fiefs)
            {
                var s = Settlement.Find(id);
                if (s == null || s.OwnerClan != Clan.PlayerClan || s.SiegeEvent != null || (s.Party != null && s.Party.MapEvent != null)) continue;   // 384b: jak Payable
                try
                {
                    bool crown = Clan.PlayerClan.Kingdom != null && s.MapFaction == Clan.PlayerClan.Kingdom;
                    ChangeOwnerOfSettlementAction.ApplyByBarter(lord, s);
                    ceded.Add(Name(s));
                    var ruler = crown ? Clan.PlayerClan.Kingdom.Leader : null;
                    if (ruler != null && ruler != me && ruler.IsAlive) ChangeRelationAction.ApplyPlayerRelation(ruler, -10);
                }
                catch (Exception e) { Stumble("Pay.Fief", e); }
            }
            return "jency wolni " + freed + (fails > 0 ? " (nieudane " + fails + ")" : "") + (names.Count > 0 ? " [" + string.Join(", ", names.ToArray()) + "]" : "")
                   + ", zloto " + gold + " (gracz -> " + Name(lord) + "), ziemia " + (ceded.Count > 0 ? string.Join(", ", ceded.ToArray()) + " (barter, wladca gracza -10)" : "-");
        }

        private static void Run(Deal d)
        {
            Settlement st = null;
            try
            {
                st = d.Field ? null : Settlement.Find(d.St);   // 385: umowa z pola nie ma osady
                var lord = Hero.Find(d.Lord);
                // 384b: P - usluga juz wykonana (gracz za brama, RideCons), wiec bez terminu 3 dob; przed wyjsciem P nie trafia do kolejki
                if (d.Want != 'P' && Today - d.Day > 3.0) { Drop(d, st, "The terms agreed " + Place(st) + " have lapsed.", "umowa przedawniona (ponad 3 doby)"); return; }
                if (lord == null || !lord.IsAlive || lord.IsPrisoner) { Drop(d, st, "The lord who gave you his word " + Place(st) + " can no longer keep it.", "przysiegajacy nie zyje albo w niewoli"); return; }
                string why;
                if (!Payable(d, out why))
                {
                    if (d.Want == 'P')
                    {
                        // 384b: gracz wyszedl na slowo i nie oddaje obiecanego - oddane, co sie da, i kara jak za zlamanie przysiegi
                        string part = Pay(d, lord);
                        string rel = BrokeWord(new HashSet<string> { lord.StringId }, lord.MapFaction);
                        Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " - GRACZ NIE ODDAL obiecanego za swobodne przejscie (" + why + ") | oddane: " + part + " | " + rel + ".");
                        Log.Player("You rode out of " + Name(st) + " on " + Name(lord) + "'s word, but did not hand over all you promised. Men will remember it.", true);
                        return;
                    }
                    Drop(d, st, "You can no longer pay what you promised " + Place(st) + ". The terms are void.", why); return;
                }
                if (!MenStill(d, st, lord)) return;   // 385b: moi ludzie juz nie u nich - bez zaplaty za nieobecnych
                if (d.Want == 'P') { RunPass(d, st, lord); return; }
                if (d.Want == 'T') { RunTruce(d, st, lord); return; }   // 385
                if (d.Field) { RunField(d, lord); return; }             // 385
                var ev = st != null ? st.SiegeEvent : null;
                if (st == null || ev == null || Math.Abs(ev.SiegeStartTime.ToHours - d.Start) > 0.05) { Drop(d, st, null, "oblezenie skonczylo sie przed wymiana - nic nie oddane"); return; }
                if (!FactionManager.IsAtWarAgainstFaction(lord.MapFaction, st.MapFaction)) { Drop(d, st, null, "pokoj miedzy stronami - nic nie oddane"); return; }
                if (SiegeSurrender374.CampBusy(st)) { Drop(d, st, "The terms agreed at " + Name(st) + " have lapsed amid the fighting.", "bitwa przy obozie dluzej niz doba"); return; }
                var keys = SiegeRansom.CampKeys384(ev.BesiegerCamp);   // przed zwinieciem
                string lift;
                if (!SiegeRansom.Lift384(st, out lift)) { Drop(d, st, "The besiegers of " + Name(st) + " could not break camp. Nothing was handed over.", "oboz nie zwiniety (" + lift + ") - bez zaplaty"); return; }
                string paid = Pay(d, lord);
                int until = (int)Today + d.Days;
                int turned = 0;
                if (!d.Betray) turned = SiegeRansom.Truce384(st, keys, until);
                foreach (var k in keys) _entries.Add(new Entry { Key = k, Scope = st.StringId, Until = until, Lord = lord.StringId, Betray = d.Betray });
                if (d.Want == 'A')
                {
                    foreach (var k in keys) _entries.Add(new Entry { Key = k, Scope = "*", Until = until, Lord = lord.StringId, Betray = d.Betray });
                    SyncPact();   // 386
                    if (!d.Betray) NoAttack();
                }
                if (!d.Betray) { try { ChangeRelationAction.ApplyPlayerRelation(lord, 5); } catch (Exception e) { Stumble("Run.Rel", e); } }
                paid += After(d, lord);   // 385: moi ludzie, przysiega gracza
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " [" + st.StringId + "] WYKONANE: " + WantName(d.Want) + " | wodz " + Name(lord) + " | " + lift + " | " + paid
                         + " | " + (d.Betray ? "ZDRADA wylosowana (p " + F(d.P * 100) + "%): rozejm NIE wpisany do latki - tylko pilnowany" : "rozejm osady do dnia " + until + " [" + string.Join(", ", keys.ToArray()) + "], zawrocone w drodze " + turned)
                         + (d.Want == 'A' ? " | pakt ziem rodu gracza do dnia " + until + (d.Betray ? " (tylko pilnowany)" : "") : "") + (d.Betray ? "" : " | relacja wodza +5") + ".");
                Log.Player(Name(lord) + " lifts the siege of " + Name(st) + " and takes back his lords. "
                           + (d.Want == 'A' ? "His realm swears to leave your lands and your men in peace for " + d.Days + " days." : "He swears not to return for " + d.Days + " days."));
            }
            catch (Exception e) { Stumble("Run", e); }
        }

        /// <summary>385b: zamknieta ekonomia "moich ludzi" - miedzy zgoda a wykonaniem (do 3 dob) ktos mogl uciec, zginac albo trafic w inne rece. Zadanie M bez
        /// ani jednego czlowieka u nich przepada bez zaplaty (false); inaczej zloto oferty maleje o okup nieobecnych (najwyzej do zera - jency i ziemia zostaja w ofercie).</summary>
        private static bool MenStill(Deal d, Settlement st, Hero lord)
        {
            try
            {
                if (d.Men.Count == 0 || d.Want == 'T') return true;
                var kept = new List<string>(); double miss = 0; int gone = 0;
                foreach (var id in d.Men)
                {
                    var h = Hero.Find(id);
                    var pb = h != null && h.IsAlive && h.IsPrisoner ? h.PartyBelongedToAsPrisoner : null;
                    if (pb != null && pb.MapFaction == lord.MapFaction) { kept.Add(id); continue; }
                    gone++;
                    if (h != null) miss += ManPrice(h);
                }
                if (gone == 0) return true;
                if (d.Want == 'M' && kept.Count == 0)
                {
                    Drop(d, st, Name(lord) + " no longer holds any of your people. Nothing was handed over.", "moi ludzie juz poza ich lochem (" + gone + ") - nic nie oddane");
                    return false;
                }
                int back = (int)Math.Min(d.Gold, Math.Round(miss));
                d.Gold -= back; d.Men = kept;
                Log.Info(Tag + ": dzien " + F(Today) + " | " + (st != null ? Name(st) : "pole") + " - moi ludzie juz poza ich lochem: " + gone + " (okup " + N(miss) + ") | zloto oferty mniejsze o " + back + " -> " + d.Gold + ", zostalo do uwolnienia " + kept.Count + ".");
                Log.Player(Name(lord) + " no longer holds " + gone + " of your people" + (back > 0 ? "; you keep " + N(back) + " denars of what you offered." : "."));
            }
            catch (Exception e) { Stumble("MenStill", e); }
            return true;
        }

        /// <summary>385: po zaplacie - moi ludzie wolni (nie przy zdradzie) i przysiega gracza na mechanizmie gry. Zwraca dopisek do linii WYKONANE.</summary>
        private static string After(Deal d, Hero lord)
        {
            var sb = new StringBuilder();
            try
            {
                if (d.Men.Count > 0)
                {
                    if (d.Betray)
                    {
                        sb.Append(" | moi ludzie: ZDRADA - wzial zaplate, nie uwolnil (" + d.Men.Count + "), klamca zapisany (374)");
                        SiegeSurrender374.MarkLiar(lord);
                        Log.Player(Name(lord) + " takes what you gave and keeps your people in chains. His word was worth nothing.", true);
                    }
                    else
                    {
                        int freed = 0, blocked = 0, gone = 0;
                        var names = new List<string>();
                        foreach (var id in d.Men)
                        {
                            var h = Hero.Find(id);
                            var pb = h != null && h.IsAlive && h.IsPrisoner ? h.PartyBelongedToAsPrisoner : null;
                            if (pb == null || pb.MapFaction != lord.MapFaction) { gone++; continue; }   // juz wolny, martwy albo w innych rekach
                            try { EndCaptivityAction.ApplyByReleasedByCompensation(h); } catch (Exception e) { Stumble("After.Free", e); }
                            if (h.IsPrisoner) blocked++; else { freed++; if (names.Count < 12) names.Add(Name(h)); }
                        }
                        sb.Append(" | moi uwolnieni " + freed + (names.Count > 0 ? " [" + string.Join(", ", names.ToArray()) + "]" : "") + " (zablokowani sceny " + blocked + ", juz poza ich lochem " + gone + ")");
                        if (freed > 0) Log.Player(Name(lord) + " sets your people free: " + string.Join(", ", names.ToArray()) + ".");
                    }
                }
                if (d.Oath && d.Betray) sb.Append(" | przysiega gracza nie wchodzi (zdrada wylosowana)");   // 385b: jedna regula z RunPass
                if (d.Oath && !d.Betray && lord.MapFaction != null)
                {
                    // jak NoAttackBarterable.Apply: gra sama pilnuje (atak, oblezenie, najazd, dolaczenie do armii) i sama zwalnia, gdy tamci uderza na gracza
                    var f = lord.MapFaction;
                    var t = f.NotAttackableByPlayerUntilTime;
                    if (t.IsPast) t = CampaignTime.Now;
                    f.NotAttackableByPlayerUntilTime = t + CampaignTime.Days(d.Days);
                    sb.Append(" | przysiega gracza wobec " + f.StringId + " do dnia " + F(f.NotAttackableByPlayerUntilTime.ToDays) + " (NotAttackableByPlayerUntilTime)");
                    Log.Player("You are sworn not to raise arms against " + Realm(f) + " for " + d.Days + " days.");
                }
            }
            catch (Exception e) { Stumble("After", e); }
            HostHand(d, lord, sb);   // 385c: zakladnik do lochu (wlasny try; po zaplacie, moich ludziach i przysiedze)
            return sb.ToString();
        }

        /// <summary>385: umowa z rozmowy w polu (bez oblezenia): zaplata, pakt (wpis "*": frakcja, gdy przysiega wladca, inaczej rod), moi ludzie, przysiega.</summary>
        private static void RunField(Deal d, Hero lord)
        {
            var me = Hero.MainHero;
            if (lord.MapFaction == null || me.MapFaction == null || !FactionManager.IsAtWarAgainstFaction(lord.MapFaction, me.MapFaction)) { Drop(d, null, null, "pokoj miedzy stronami - nic nie oddane"); return; }
            string paid = Pay(d, lord);
            int until = (int)Today + d.Days;
            string key = Ruler(lord) ? lord.MapFaction.StringId : (lord.Clan != null ? "c:" + lord.Clan.StringId : null);
            if (d.Want == 'A' && key != null)
            {
                _entries.Add(new Entry { Key = key, Scope = "*", Until = until, Lord = lord.StringId, Betray = d.Betray });
                SyncPact();   // 386
                if (!d.Betray) NoAttack();
            }
            if (!d.Betray) { try { ChangeRelationAction.ApplyPlayerRelation(lord, 5); } catch (Exception e) { Stumble("RunField.Rel", e); } }
            paid += After(d, lord);
            Log.Info(Tag + ": dzien " + F(Today) + " | pole WYKONANE: " + WantName(d) + " | " + Name(lord) + (Ruler(lord) ? " - wladca" : "") + " (" + lord.MapFaction.StringId + ") | " + paid
                     + (d.Want == 'A' ? " | pakt ziem rodu gracza do dnia " + until + " [" + key + "]" + (d.Betray ? " - ZDRADA wylosowana (p " + F(d.P * 100) + "%): tylko pilnowany" : "") : "")
                     + (d.Betray ? "" : " | relacja +5") + ".");
            if (d.Want == 'A') Log.Player(Name(lord) + " takes what you offered. His " + (Ruler(lord) ? "realm" : "house") + " swears to leave your lands and your men in peace for " + d.Days + " days.");
            else Log.Player(Name(lord) + " takes what you offered.");
        }

        /// <summary>385: rozejm krolestw gracza-wladcy. Najpierw pokoj droga gry (MakePeaceAction.Apply - nasze blokady i rozejm 330 dzialaja po swojemu), sprawdzenie,
        /// ze wojna zgasla, dopiero potem zaplata; wpis "K" poza para 330 (tam rozejmem rzadzi WarTruce).</summary>
        private static void RunTruce(Deal d, Settlement st, Hero lord)
        {
            var k = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
            var e = lord.MapFaction as Kingdom;
            string bar = PeaceBar(lord);
            if (bar != null || k == null || e == null) { Drop(d, st, "The peace you agreed " + Place(st) + " can no longer be made.", "pokoj juz niemozliwy (" + (bar ?? "brak krolestwa") + ") - nic nie oddane"); return; }
            try { MakePeaceAction.Apply(k, e); } catch (Exception ex) { Stumble("RunTruce.Peace", ex); }
            if (k.IsAtWarWith(e)) { Drop(d, st, "The peace you agreed " + Place(st) + " did not hold. Nothing was handed over.", "MakePeaceAction nie zgasil wojny (blokada) - nic nie oddane"); return; }
            // 385b: dlugosc PO FAKCIE - para 330 tylko wtedy, gdy WarTruce.OnPeace naprawde wpisal rozejm (klucz 330 NIE, koniec 330, krol kanonu, kampania
            // klasyczna = rozejmu WarTruce nie ma -> zwykly wpis "K", zeby pokoj mial bramke wojny AI)
            bool pair = false; int days = d.Days;
            try { if (WarTruce.IsPair(k, e) && WarTruce.TruceHolds) { pair = true; days = Math.Max(1, WarTruce.TruceEnd - (int)Today); } } catch (Exception ex) { Stumble("RunTruce.Pair", ex); }
            string paid = Pay(d, lord);
            int until = (int)Today + days;
            if (!pair)
            {
                _entries.Add(new Entry { Key = e.StringId, Scope = "K", Until = until, Lord = lord.StringId, Betray = d.Betray });
                SyncRealm();
            }
            if (!d.Betray) { try { ChangeRelationAction.ApplyPlayerRelation(lord, 5); } catch (Exception ex) { Stumble("RunTruce.Rel", ex); } }
            Log.Info(Tag + ": dzien " + F(Today) + " | " + (st != null ? Name(st) : "pole") + " WYKONANE: ROZEJM KROLESTW " + k.StringId + "-" + e.StringId + " do dnia " + until
                     + (pair ? " (para 330 - rozejmem rzadzi WarTruce, bez wpisu)" : d.Betray ? " | ZDRADA wylosowana (p " + F(d.P * 100) + "%): wpis tylko pilnowany, bramki wojny nie trzymaja" : " | bramki wojny AI czynne")
                     + " | " + paid + " | jency obu stron wychodza droga pokoju (PeaceHold)" + (d.Betray ? "" : " | relacja +5") + ".");
            Log.Player(Name(lord) + " swears peace between " + e.Name + " and " + k.Name + " for " + days + " days.", true);
        }

        /// <summary>385: przebudowa migawki rozejmow krolestw po kazdej zmianie wpisow.</summary>
        private static void SyncRealm()
        {
            try
            {
                var l = new List<string>();
                int today = (int)Today;
                foreach (var e in _entries) if (e.Scope == "K" && !e.Betray && e.Until > today && !l.Contains(e.Key)) l.Add(e.Key);
                _realmKeys = l.Count > 0 ? l.ToArray() : NoKeys;
            }
            catch (Exception e) { _realmKeys = NoKeys; Stumble("SyncRealm", e); }
            SyncPact();   // 386: te same miejsca zmieniaja wpisy paktu
        }

        /// <summary>385: bramki wojny AI (WarTruce.AddDecisionPrefix, CanonThroneWars.WarScorePostfix, CaptiveGrudge): krolestwo gracza i krolestwo z czynnym,
        /// uczciwym rozejmem krolestw. Tylko odczyt migawki - dowolny watek, nie rzuca.</summary>
        internal static bool RealmTruce(IFaction a, IFaction b)
        {
            var keys = _realmKeys;
            if (keys.Length == 0 || a == null || b == null) return false;
            try
            {
                var pk = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (pk == null || pk.Leader != Hero.MainHero) return false;   // 385b: slowo dal gracz-wladca; po utracie tronu / zmianie krolestwa nowe krolestwo nie jest zwiazane
                IFaction o = ReferenceEquals(a, pk) ? b : ReferenceEquals(b, pk) ? a : null;
                if (o == null) return false;
                string id = o.StringId;
                foreach (var k in keys) if (k == id) return true;
            }
            catch { }
            return false;
        }

        internal static void NoteRealmBlock() { System.Threading.Interlocked.Increment(ref _dRealmBlock); }

        /// <summary>385: nasluch WarDeclared - wojna krolestwa gracza z krolestwem z wpisem "K": gracz-wladca wypowiedzial pierwszy = zlamane slowo; tamci =
        /// kara tylko dla zdrajcy (uczciwy wpis: droga poza bramkami, np. wojna wymuszona przez ROT - bez kary).</summary>
        internal static void OnWarDeclared(IFaction f1, IFaction f2, DeclareWarAction.DeclareWarDetail detail)
        {
            if (_entries.Count == 0) return;
            try
            {
                var pk = Clan.PlayerClan != null ? Clan.PlayerClan.Kingdom : null;
                if (pk == null || f1 == null || f2 == null) return;
                IFaction o = ReferenceEquals(f1, pk) ? f2 : ReferenceEquals(f2, pk) ? f1 : null;
                if (o == null || o.StringId == null) return;
                int today = (int)Today;
                var hit = _entries.FindAll(e => e.Scope == "K" && e.Key == o.StringId && e.Until > today);
                if (hit.Count == 0) return;
                // 385b: zlamane slowo gracza tylko, gdy wojne wypowiedzial on (wrogosc gracza albo decyzja jego krolestwa); ROT / sojusz BK / Default = droga poza bramkami
                bool byPlayer = ReferenceEquals(f1, pk) && pk.Leader == Hero.MainHero
                                && (detail == DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility || detail == DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);
                var lords = new HashSet<string>();
                foreach (var e in hit) { if (byPlayer || e.Betray) lords.Add(e.Lord); _entries.Remove(e); }
                SyncRealm();
                if (byPlayer)
                {
                    string rel = BrokeWord(lords, o);
                    Log.Info(Tag + ": dzien " + F(Today) + " | GRACZ LAMIE ROZEJM KROLESTW z " + o.StringId + " (" + detail + ") | " + rel + ".");
                    Log.Player("You break the peace you swore with " + Realm(o) + ". Men will remember it.", true);
                    return;
                }
                var names = new List<string>();
                foreach (var id in lords) { var h = Hero.Find(id); if (h == null || !h.IsAlive) continue; int n = Punish(h); names.Add(Name(h) + " (kara " + n + " glow rodow)"); }
                Log.Info(Tag + ": dzien " + F(Today) + " | ROZEJM KROLESTW zlamany: " + f1.StringId + " wypowiada wojne " + f2.StringId + " (" + detail + ")"
                         + (names.Count > 0 ? " | ZDRADA - " + string.Join(", ", names.ToArray()) + ": klamca, relacja -30" : " | wpis uczciwy - droga poza bramkami, bez kary") + ".");
                Log.Player(Realm(o) + " breaks the peace sworn with you.", true);
            }
            catch (Exception e) { Stumble("OnWarDeclared", e); }
        }

        /// <summary>385: walka gracza z frakcja rozmowcy przed wykonaniem umowy z pola - umowa przepada (nic nie oddane).</summary>
        private static void FieldFight(PartyBase a, PartyBase d)
        {
            try
            {
                PartyBase other = a == PartyBase.MainParty ? d : d == PartyBase.MainParty ? a : null;
                if (other == null || other.MapFaction == null) return;
                for (int i = _queue.Count - 1; i >= 0; i--)
                {
                    var q = _queue[i];
                    if (!q.Field) continue;
                    var lord = Hero.Find(q.Lord);
                    if (lord == null || lord.MapFaction != other.MapFaction) continue;
                    _queue.RemoveAt(i); _armed = false;
                    Drop(q, null, "Steel is drawn before the terms agreed with " + Name(lord) + " were kept. They are void.", "walka z " + other.Name + " przed wykonaniem (" + (a == PartyBase.MainParty ? "atak gracza" : "atak na gracza") + ") - nic nie oddane");
                }
            }
            catch (Exception e) { Stumble("FieldFight", e); }
        }

        private static void RunPass(Deal d, Settlement st, Hero lord)
        {
            var me = Hero.MainHero;
            var main = MobileParty.MainParty;
            var ev = st != null ? st.SiegeEvent : null;
            MobileParty cap = null;
            if (d.Betray && ev != null && ev.BesiegerCamp != null)
            {
                var lp = lord.PartyBelongedTo;
                if (lp != null && lp.IsActive && lp.LeaderHero == lord && ev.BesiegerCamp.HasInvolvedPartyForEventType(lp.Party)) cap = lp;
                else { var l = ev.BesiegerCamp.LeaderParty; if (l != null && l.IsActive && l.LeaderHero != null) cap = l; }
            }
            string paid = Pay(d, lord);
            if (cap == null)
            {
                try { ChangeRelationAction.ApplyPlayerRelation(lord, 5); } catch (Exception e) { Stumble("RunPass.Rel", e); }
                var kept = new Deal { St = d.St, Lord = d.Lord, Days = d.Days, Men = d.Men, Oath = d.Oath, Host = d.Host, Betray = false };   // 385: slowo dotrzymane (takze gdy zdrada wylosowana, a oboz juz nie stoi)
                paid += After(kept, lord);
                Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " WYKONANE: swobodne przejscie | wodz " + Name(lord) + " dotrzymal slowa" + (d.Betray ? " (zdrada wylosowana, ale oboz juz nie stoi)" : "")
                         + " | " + paid + " | twierdza zostaje u " + (st != null && st.OwnerClan != null ? st.OwnerClan.Name.ToString() : "-") + " | relacja wodza +5.");
                Log.Player("You ride out of " + Name(st) + " and leave " + Name(lord) + "'s lords at the gate. He keeps his word.");
                return;
            }
            // zdrada: jak RunBetray 374 bez zmiany pana twierdzy - szeregowi do lochu, jency-bohaterowie gracza wolni, towarzysze i gracz w niewoli
            int men = 0, comp = 0, freed = 0;
            bool taken = false;
            try
            {
                men = SiegeSurrender374.ToPrison(main.MemberRoster, cap.Party.PrisonRoster);
                var pr = main.PrisonRoster;
                for (int i = pr.Count - 1; i >= 0; i--)
                {
                    var c = pr.GetCharacterAtIndex(i);
                    if (c == null || !c.IsHero || c.HeroObject == null) continue;
                    try { EndCaptivityAction.ApplyByReleasedAfterBattle(c.HeroObject); freed++; } catch (Exception e) { Stumble("RunPass.Free", e); }
                }
                var heroes = new List<Hero>();
                var r = main.MemberRoster;
                for (int i = 0; i < r.Count; i++) { var c = r.GetCharacterAtIndex(i); if (c != null && c.IsHero && c.HeroObject != null && c.HeroObject != me) heroes.Add(c.HeroObject); }
                foreach (var h in heroes) { try { TakePrisonerAction.Apply(cap.Party, h); comp++; } catch (Exception e) { Stumble("RunPass.Companion", e); } }
                TakePrisonerAction.Apply(cap.Party, me); taken = true;
            }
            catch (Exception e) { Stumble("RunPass.Betray", e); }
            int punished = Punish(lord);
            Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " ZDRADA przy przejsciu: wodz " + Name(lord) + " lamie slowo | " + paid + " | gracz " + (taken ? "W NIEWOLI" : "NIE pojmany (blad)")
                     + " u " + (cap.Name != null ? cap.Name.ToString() : "-") + ", towarzysze " + comp + ", szeregowi do lochu " + men + ", jency-bohaterowie gracza wolni " + freed
                     + " | twierdza zostaje z zaloga | kara relacji " + punished + " glow rodow | klamca zapisany (374)" + (d.Men.Count > 0 ? " | moi ludzie nie uwolnieni (" + d.Men.Count + ")" : "") + (d.Oath ? " | przysiega gracza nie wchodzi (slowo zlamane)" : "") + ".");
            Log.Player(Name(lord) + " breaks his word! His men take your lords at the gate - and then fall on you.", true);
        }

        /// <summary>Zdrajca: relacja z graczem -30, honorowe glowy rodow -5 x Honor wobec niego, lista klamcow 374. Zwraca ile glow rodow.</summary>
        private static int Punish(Hero lord)
        {
            int punished = 0;
            try
            {
                ChangeRelationAction.ApplyPlayerRelation(lord, -30);
                foreach (var c in Clan.All)
                {
                    if (c == null || c.IsEliminated || c == lord.Clan || c == Clan.PlayerClan || c.IsBanditFaction) continue;
                    var l = c.Leader;
                    if (l == null || !l.IsAlive || l == lord) continue;
                    int hon = Trait(l, DefaultTraits.Honor);
                    if (hon <= 0) continue;
                    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(l, lord, -5 * hon, false);
                    punished++;
                }
                SiegeSurrender374.MarkLiar(lord);
            }
            catch (Exception e) { Stumble("Punish", e); }
            return punished;
        }

        // ------------------------------------------------------------ pakt: cele AI, druzyna gracza, zlamania
        private static bool Match(Entry e, IFaction f, Clan c)
        {
            if (f != null && f.StringId != null && e.Key == f.StringId) return true;
            return c != null && c.StringId != null && e.Key == "c:" + c.StringId;
        }

        /// <summary>Postfiks celu AI (z SiegeRansom.ScorePost): pakt - oblezenie twierdzy i najazd na wies rodu gracza ma wynik 0 dla frakcji / rodow z paktem.</summary>
        internal static void ScorePact(Settlement st, Army.ArmyTypes type, MobileParty party, ref float result)
        {
            if (_entries.Count == 0 || result <= 0f) return;
            try
            {
                if (!On || st == null || party == null) return;
                if (type != Army.ArmyTypes.Besieger && type != Army.ArmyTypes.Raider) return;
                int today = (int)Today;
                // 386b: odwrotnie - druzyna rodu gracza (towarzysz) nie wybiera za cel najazdu / oblezenia osady frakcji albo rodu z uczciwym paktem
                if (KClan && Mine(party) && !MineSt(st, Clan.PlayerClan))
                {
                    foreach (var e in _entries)
                    {
                        if (e.Betray || e.Scope != "*" || e.Until <= today) continue;
                        if (Match(e, st.MapFaction, st.OwnerClan)) { result = 0f; _dZeroMine++; return; }
                    }
                    return;
                }
                if (st.OwnerClan != Clan.PlayerClan) return;
                foreach (var e in _entries)
                {
                    if (e.Betray || e.Scope != "*" || e.Until <= today) continue;
                    if (Match(e, party.MapFaction, party.ActualClan)) { result = 0f; _dZero++; return; }
                }
            }
            catch (Exception e) { Stumble("ScorePact", e); }
        }

        /// <summary>Druzyny frakcji / rodow z paktem nie atakuja druzyny gracza (SetDoNotAttackMainParty 25 h - odswiezane co dobe).</summary>
        private static void NoAttack()
        {
            try
            {
                int today = (int)Today;
                foreach (var mp in MobileParty.AllLordParties)
                {
                    if (mp == null || !mp.IsActive || mp.IsMainParty || mp.Ai == null) continue;
                    foreach (var e in _entries)
                    {
                        if (e.Betray || e.Scope != "*" || e.Until <= today) continue;
                        if (Match(e, mp.MapFaction, mp.ActualClan)) { mp.Ai.SetDoNotAttackMainParty(PactNoAttackHours); _dNoAttack++; break; }
                    }
                }
            }
            catch (Exception e) { Stumble("NoAttack", e); }
            ClearOrders();   // 386
        }

        // ------------------------------------------------------------ 386: pakt chroni partie rodu gracza (karawany, druzyny towarzyszy, wiesniacy jego wsi)
        /// <summary>Niezmienna migawka kluczy uczciwego, czynnego paktu (zasieg "*"): F - id frakcji, C - id rodow (wpisy "c:"). Podmieniana w calosci w watku
        /// glownym (SyncPact), czytana z watkow AI. null = brak paktu.</summary>
        private sealed class Pact { public readonly HashSet<string> F = new HashSet<string>(StringComparer.Ordinal), C = new HashSet<string>(StringComparer.Ordinal); }
        private static volatile Pact _pact;
        private static string _pactHook = "nie wpinana", _avoidHook = "nie wpinana", _thinkHook = "nie wpinana";
        private static int _pGoAi, _pGoMy, _pGoFuse;   // 386c: watki AI (Interlocked)
        private static readonly object PactGate = new object();
        private static readonly HashSet<TaleWorlds.ObjectSystem.MBGUID> _pAiSet = new HashSet<TaleWorlds.ObjectSystem.MBGUID>(), _pMySet = new HashSet<TaleWorlds.ObjectSystem.MBGUID>();
        private static int _pAiCalls, _pMyCalls, _pFlee, _pThreadStumbles;   // watki AI (Interlocked / PactGate)
        private static int _pOrders, _pDespite, _pMyDespite;                  // watek glowny

        private static void PactZero()
        {
            lock (PactGate) { _pAiSet.Clear(); _pMySet.Clear(); }
            Interlocked.Exchange(ref _pAiCalls, 0); Interlocked.Exchange(ref _pMyCalls, 0); Interlocked.Exchange(ref _pFlee, 0);
            Interlocked.Exchange(ref _pGoAi, 0); Interlocked.Exchange(ref _pGoMy, 0); Interlocked.Exchange(ref _pGoFuse, 0);   // 386c
            _pOrders = 0; _pDespite = 0; _pMyDespite = 0;
        }

        private static Pact BuildPact(bool withTraitors)
        {
            Pact p = null;
            int today = (int)Today;
            foreach (var e in _entries)
            {
                if (e.Scope != "*" || e.Until <= today || (e.Betray && !withTraitors) || string.IsNullOrEmpty(e.Key)) continue;
                if (p == null) p = new Pact();
                if (e.Key.StartsWith("c:", StringComparison.Ordinal)) p.C.Add(e.Key.Substring(2)); else p.F.Add(e.Key);
            }
            return p;
        }

        /// <summary>Watek glowny, po kazdej zmianie wpisow (Run, RunField, SyncRealm - BrokeWord / Daily / Import / wojna, AiBroke) i w Reset.</summary>
        private static void SyncPact()
        {
            try { _pact = BuildPact(false); }
            catch (Exception e) { _pact = null; Stumble("SyncPact", e); }
        }

        /// <summary>Czy partia nalezy do frakcji / rodu z migawki (to samo co Match dla wpisu). Tylko odczyt.</summary>
        private static bool Has(Pact k, MobileParty mp)
        {
            if (k == null || mp == null) return false;
            var f = mp.MapFaction;
            if (f != null && k.F.Count > 0 && f.StringId != null && k.F.Contains(f.StringId)) return true;
            if (k.C.Count == 0) return false;
            var c = mp.ActualClan;
            return c != null && c.StringId != null && k.C.Contains(c.StringId);
        }

        /// <summary>Partia rodu gracza objeta paktem (poza druzyna gracza - ta ma SetDoNotAttackMainParty): karawany i druzyny towarzyszy (ActualClan = rod gracza,
        /// bez zalogi i milicji - twierdze obsluguje ScorePact + Relift) oraz wiesniacy wsi rodu gracza (pakt zakazuje najazdu na te wsie - zabicie wozu wsi to ten
        /// sam cios). Nie: partia DOLACZONA do cudzej armii (AttachedTo - w szyku walczy pod cudzym rozkazem; gra i tak nie celuje w nia osobno). 386c: partia
        /// wezwana do cudzej armii, ale jeszcze w drodze (Army != null, AttachedTo == null) JEST partia rodu - chyba ze wodz tej armii wystepuje przeciw frakcji
        /// paktu (Hunting patrzy na wodza). Tylko odczyt pol - watki AI.</summary>
        private static bool Mine(MobileParty mp)
        {
            if (mp == null || mp.IsMainParty) return false;
            var pc = Clan.PlayerClan;
            if (pc == null) return false;
            if (mp.IsVillager) return MineSt(mp.HomeSettlement, pc);
            if (mp.ActualClan != pc || mp.IsGarrison || mp.IsMilitia) return false;
            var main = MobileParty.MainParty;
            var at = mp.AttachedTo;
            if (at != null && at != main && at.ActualClan != pc) return false;
            return true;
        }

        /// <summary>386c: partia rodu wezwana do cudzej armii (wodz spoza rodu gracza, nie gracz), jeszcze niedolaczona. Tylko odczyt.</summary>
        private static bool EnRoute(MobileParty mp)
        {
            var army = mp != null ? mp.Army : null;
            var lp = army != null ? army.LeaderParty : null;
            return lp != null && lp != mp && mp.AttachedTo == null && lp != MobileParty.MainParty && lp.ActualClan != Clan.PlayerClan;
        }

        /// <summary>386c: partia rodu objeta paktem TERAZ - Mine i sama (ani wodz jej armii) nie wystepuje przeciw frakcji paktu. Scigajacy taka partie jest
        /// napastnikiem (AggressorCore); scigajacy partie rodu, ktora sama na nich idzie, broni sie legalnie i samoobrony graczowi nie daje.</summary>
        private static bool Guarded(MobileParty mp, Pact k) { return Mine(mp) && !Hunting(mp, k); }

        /// <summary>386b: poscig to TYLKO EngageParty. GoAroundParty to podejscie, ktore gra co godzine daje kazdej druzynie lorda na pobliska wroga druzyne
        /// (AiEngagePartyBehavior.AiHourlyTick) - liczone jako poscig robilo z kazdego lorda przy froncie "napastnika" i rozbrajalo pakt w obie strony; atak z
        /// podejscia i tak musi przejsc przez ShouldConsiderAttacking, czyli przez weto.</summary>
        private static bool Engages(AiBehavior b) { return b == AiBehavior.EngageParty; }

        /// <summary>386b: osada frakcji / rodu z migawki paktu. Tylko odczyt.</summary>
        private static bool HasSt(Pact k, Settlement s)
        {
            if (k == null || s == null) return false;
            var f = s.MapFaction;
            if (f != null && k.F.Count > 0 && f.StringId != null && k.F.Contains(f.StringId)) return true;
            if (k.C.Count == 0) return false;
            var c = s.OwnerClan;
            return c != null && c.StringId != null && k.C.Contains(c.StringId);
        }

        /// <summary>384b + 386, jedna regula samoobrony dla gracza i partii rodu: x (albo wodz jego armii) oblega twierdze rodu gracza, najezdza / idzie oblegac
        /// ziemie rodu gracza albo SCIGA (tylko EngageParty, 386b - sam cel przy ucieczce to nie poscig: gra wpisuje w ShortTermTargetParty takze partie, przed
        /// ktora sie ucieka) druzyne gracza albo partie rodu. Zwraca powod albo null. Tylko odczyt, bez lapania wyjatkow (lapie wolajacy) - takze watki AI.</summary>
        private static string AggressorCore(MobileParty mp)
        {
            if (mp == null) return null;
            var pc = Clan.PlayerClan;
            var main = MobileParty.MainParty;
            var pk = _pact;   // 386c: jeden odczyt migawki (null = Guarded to samo co Mine)
            bool kc = KClan;
            for (int k = 0; k < 2; k++)
            {
                var x = k == 0 ? mp : (mp.Army != null ? mp.Army.LeaderParty : null);
                if (x == null || (k == 1 && x == mp)) continue;
                var camp = x.BesiegerCamp;
                var bs = camp != null && camp.SiegeEvent != null ? camp.SiegeEvent.BesiegedSettlement : null;
                if (MineSt(bs, pc)) return "oblega " + Name(bs);
                var tp = Engages(x.DefaultBehavior) ? x.TargetParty : null;
                var sp = Engages(x.ShortTermBehavior) ? x.ShortTermTargetParty : null;
                if (main != null && (tp == main || sp == main)) return "sciga druzyne gracza";
                if (kc && (Guarded(tp, pk) || Guarded(sp, pk))) return "sciga partie rodu gracza";   // 386b: przy kluczu NIE pakt nie obejmuje partii rodu - bez darmowej samoobrony; 386c: Guarded
                for (int j = 0; j < 2; j++)
                {
                    var t = j == 0 ? x.TargetSettlement : x.ShortTermTargetSettlement;
                    if (!MineSt(t, pc)) continue;
                    if (x.DefaultBehavior == AiBehavior.RaidSettlement || x.ShortTermBehavior == AiBehavior.RaidSettlement) return "najezdza " + Name(t);
                    if (x.DefaultBehavior == AiBehavior.BesiegeSettlement || x.ShortTermBehavior == AiBehavior.BesiegeSettlement) return "idzie oblegac " + Name(t);
                }
            }
            return null;
        }

        /// <summary>Partia rodu b sama wystepuje przeciw frakcji z paktem (samoobrona albo dolaczenie do bitwy - inaczej latka by jej nie puscila): sciga partie z
        /// kluczem albo stoi w bitwie naprzeciw takiej partii. Wtedy tamci moga sie bronic / dolaczyc. Tylko odczyt.</summary>
        private static bool Hunting(MobileParty b, Pact k)
        {
            if (b == null || k == null) return false;
            // 386c: czlonek armii (takze w drodze na zbiorke) wystepuje przeciw nim, gdy robi to wodz jego armii - "armia idaca na nich" to zwykly wrog.
            // Armia gracza pominieta (czyny gracza sadzi PlayerBroke / SelfDefence). Jeden poziom, bez rekurencji.
            return ArmyHunts(b, k) || HuntingCore(b, k);
        }

        /// <summary>386c: wodz armii partii b (nie ona sama, nie gracz) wystepuje przeciw frakcji paktu - b jest wtedy zwyklym wrogiem w OBIE strony
        /// (oni moga ja atakowac, ona moze atakowac ich i ma przed nimi uciekac). Tylko odczyt.</summary>
        private static bool ArmyHunts(MobileParty b, Pact k)
        {
            if (b == null || k == null) return false;
            var al = b.Army != null ? b.Army.LeaderParty : null;
            return al != null && al != b && al != MobileParty.MainParty && HuntingCore(al, k);
        }

        private static bool HuntingCore(MobileParty b, Pact k)
        {
            if (Engages(b.DefaultBehavior) && Has(k, b.TargetParty)) return true;
            if (Engages(b.ShortTermBehavior) && Has(k, b.ShortTermTargetParty)) return true;
            // 386b: najazd / oblezenie ich osady (takze w drodze) to tez wystepowanie przeciw nim - obroncy nie maja weta
            if (HasSt(k, b.BesiegedSettlement)) return true;
            if ((b.DefaultBehavior == AiBehavior.RaidSettlement || b.DefaultBehavior == AiBehavior.BesiegeSettlement) && HasSt(k, b.TargetSettlement)) return true;
            if (b.MapEvent == null) return false;
            if (HasSt(k, b.MapEvent.MapEventSettlement)) return true;
            var side = b.MapEventSide;
            var opp = side != null ? side.OtherSide : null;
            var lp = opp != null && opp.LeaderParty != null ? opp.LeaderParty.MobileParty : null;
            return Has(k, lp);
        }

        /// <summary>1 = partia z paktem a wobec partii rodu b, 2 = partia rodu a wobec partii z paktem b, 0 = para poza paktem.</summary>
        private static int PactPair(Pact k, MobileParty a, MobileParty b)
        {
            if (Mine(b)) return Has(k, a) ? 1 : 0;
            if (Mine(a)) return Has(k, b) ? 2 : 0;
            return 0;
        }

        /// <summary>SubModuleMain (menu glowne, raz na proces - jak FreeFolkRaids.ApplyAll): postfiksy DefaultMobilePartyAIModel.ShouldConsiderAttacking /
        /// ShouldConsiderAvoiding (obie zadeklarowane w tej klasie - DeclaredMethod; klasa bez pol statycznych; czynny NavalDLCMobilePartyAIModel wola baze).</summary>
        internal static void ApplyPact(Harmony h)
        {
            var t = typeof(TaleWorlds.CampaignSystem.GameComponents.DefaultMobilePartyAIModel);
            var sig = new[] { typeof(MobileParty), typeof(MobileParty) };
            try
            {
                var m = AccessTools.DeclaredMethod(t, "ShouldConsiderAttacking", sig);
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(PrisonerTerms384), nameof(AttackPact)) { priority = Priority.Last }); _pactHook = "wpieta"; }
                else _pactHook = "BRAK ShouldConsiderAttacking";
            }
            catch (Exception e) { _pactHook = "blad " + e.GetType().Name; Log.Error("PrisonerTerms384.ApplyPact(atak)", e); }
            try
            {
                var m = AccessTools.DeclaredMethod(t, "ShouldConsiderAvoiding", sig);
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(PrisonerTerms384), nameof(AvoidPact)) { priority = Priority.Last }); _avoidHook = "wpieta"; }
                else _avoidHook = "BRAK ShouldConsiderAvoiding";
            }
            catch (Exception e) { _avoidHook = "blad " + e.GetType().Name; Log.Error("PrisonerTerms384.ApplyPact(ucieczka)", e); }
            // 386c: cel podejscia (GoAroundParty) wyciety u zrodla - postfiks 140 na mysli godzinowej: po RescueHunt (150, dodaje te opcje), przed ArmsFirst (100),
            // CanonWarBalance (50) i LordEscort / SiegeWalls2 / CanonWarWatch (Last) - tamte nie podnosza wyniku <= -1. Metoda zadeklarowana w CampaignEventDispatcher (override).
            try
            {
                var m = AccessTools.DeclaredMethod(typeof(CampaignEventDispatcher), "AiHourlyTick", new[] { typeof(MobileParty), typeof(PartyThinkParams) });
                if (m != null) { h.Patch(m, postfix: new HarmonyMethod(typeof(PrisonerTerms384), nameof(ThinkPact)) { priority = 140 }); _thinkHook = "wpieta"; }
                else _thinkHook = "BRAK CampaignEventDispatcher.AiHourlyTick";
            }
            catch (Exception e) { _thinkHook = "blad " + e.GetType().Name; Log.Error("PrisonerTerms384.ApplyPact(podejscie)", e); }
            ApplyHostage(h);   // 385c: zakladnik - straz ucieczki i wyciecie rozbierania RealisticCaptivity (wlasne try, wlasna linia logu)
            try { Log.Info(Tag + ": latka paktu partii rodu (386) - atak: " + _pactHook + ", ucieczka: " + _avoidHook + ", podejscie (386c): " + _thinkHook + " (postfiksy DefaultMobilePartyAIModel.ShouldConsiderAttacking / ShouldConsiderAvoiding i CampaignEventDispatcher.AiHourlyTick 140; partie rodu przy kluczu Prisoner Exchange Pact Guards Clan Parties, druzyna gracza przy samym Prisoner Exchange For Terms; tylko przy czynnym pakcie)."); } catch { }
        }

        /// <summary>386c: para z paktem (kierunek 1: partia frakcji / rodu z paktem -> druzyna gracza albo partia rodu). Tylko odczyt.</summary>
        private static bool GoCutAi(Pact k, MobileParty t, bool kc)
        {
            if (t.IsMainParty) return true;                       // sam klucz 384 (jak NoAttack)
            return kc && Mine(t) && !Hunting(t, k);               // partia rodu sama (albo wodz jej armii) idzie na nich - moga podejsc
        }

        /// <summary>386c: kierunek 2 - partia rodu -> partia z paktem; wyjatki jak w AttackPact (trwajaca bitwa, samoobrona). Tylko odczyt.</summary>
        private static bool GoCutMine(Pact k, MobileParty self, MobileParty t)
        {
            return Has(k, t) && t.MapEvent == null && AggressorCore(t) == null && !ArmyHunts(self, k);
        }

        /// <summary>386c: postfiks (140) CampaignEventDispatcher.AiHourlyTick: opcja GoAroundParty (AiEngagePartyBehavior - podejscie do pobliskiej wrogiej druzyny,
        /// takze dodana przez RescueHunt) na partie objeta paktem dostaje -1 (konwencja repo: wynik -1 nie wygrywa i nikt go nie podnosi). Zamyka droge
        /// MobilePartyAi.GetGoAroundPartyBehavior -> EngageParty bez pytania modelu (gdy brak punktu obejscia) i krazenie lordow paktu wokol partii rodu.
        /// Bez paktu - jeden odczyt migawki. Tylko migawka i pola partii (bez _entries). Nie rzuca.</summary>
        public static void ThinkPact(MobileParty party, PartyThinkParams partyThinkParams)
        {
            var k = _pact;
            if (k == null) return;
            try
            {
                if (party == null || partyThinkParams == null || party.IsMainParty || partyThinkParams.DoNotChangeBehavior || !On) return;
                bool kc = KClan;
                bool a = Has(k, party);
                bool m = !a && kc && Mine(party);
                if (!a && !m) return;
                var scores = partyThinkParams.AIBehaviorScores;
                if (scores == null || scores.Count == 0) return;
                List<AIBehaviorData> cut = null;
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (!(e.Item2 > -1f) || e.Item1.AiBehavior != AiBehavior.GoAroundParty) continue;
                    var t = e.Item1.Party as MobileParty;
                    if (t == null || !(a ? GoCutAi(k, t, kc) : GoCutMine(k, party, t))) continue;
                    if (cut == null) cut = new List<AIBehaviorData>(2);
                    cut.Add(e.Item1);
                }
                if (cut == null) return;
                foreach (var d0 in cut)
                {
                    var d = d0;
                    partyThinkParams.SetBehaviorScore(in d, -1f);
                    if (a) Interlocked.Increment(ref _pGoAi); else Interlocked.Increment(ref _pGoMy);
                }
                // bezpiecznik (wzor WatchLaw): SetBehaviorScore ustawia pierwsza rowna pozycje - duplikat opcji zostalby z wynikiem ponad -1
                for (int i = 0; i < scores.Count; i++)
                {
                    var e = scores[i];
                    if (!(e.Item2 > -1f) || e.Item1.AiBehavior != AiBehavior.GoAroundParty) continue;
                    var t = e.Item1.Party as MobileParty;
                    if (t != null && (a ? GoCutAi(k, t, kc) : GoCutMine(k, party, t))) Interlocked.Increment(ref _pGoFuse);
                }
            }
            catch { Interlocked.Increment(ref _pThreadStumbles); }
        }

        /// <summary>Postfiks ShouldConsiderAttacking (watki AI): para z paktem nie zaczyna ataku. Bez paktu - jeden odczyt migawki. Nie rzuca.</summary>
        public static void AttackPact(MobileParty __0, MobileParty __1, ref bool __result)
        {
            if (!__result) return;
            var k = _pact;
            if (k == null) return;
            try
            {
                if (__0 == null || __1 == null || !KClan) return;
                int dir = PactPair(k, __0, __1);
                if (dir == 0) return;
                if (dir == 1)
                {
                    if (Hunting(__1, k)) return;                       // partia rodu sama wystepuje przeciw nim - moga sie bronic
                    __result = false;
                    Interlocked.Increment(ref _pAiCalls);
                    lock (PactGate) { _pAiSet.Add(__0.Id); }
                }
                else
                {
                    if (__1.MapEvent != null) return;                  // dolaczenie do trwajacej bitwy to nie zaczecie ataku (regula gracza)
                    if (AggressorCore(__1) != null) return;            // samoobrona
                    if (ArmyHunts(__0, k)) return;                     // 386c: jedzie do armii, ktora idzie na nich - wojna korony, bez weta
                    __result = false;
                    Interlocked.Increment(ref _pMyCalls);
                    lock (PactGate) { _pMySet.Add(__0.Id); }
                }
            }
            catch { Interlocked.Increment(ref _pThreadStumbles); }
        }

        /// <summary>Postfiks ShouldConsiderAvoiding (watki AI): para z paktem nie ucieka przed soba - chyba ze druga strona naprawde wystepuje przeciw niej
        /// (napastnik wobec rodu / partia rodu scigajaca ich). Zdrajcy nie sa w migawce - przed nimi ucieczka zostaje. Nie rzuca.</summary>
        public static void AvoidPact(MobileParty __0, MobileParty __1, ref bool __result)
        {
            if (!__result) return;
            var k = _pact;
            if (k == null) return;
            try
            {
                if (__0 == null || __1 == null || !KClan) return;
                int dir = PactPair(k, __0, __1);
                if (dir == 0) return;
                if (dir == 1) { if (Hunting(__1, k)) return; }
                else if (AggressorCore(__1) != null || ArmyHunts(__0, k)) return;   // 386c: ArmyHunts - tamci moga ja legalnie atakowac, wiec ucieka
                __result = false;
                Interlocked.Increment(ref _pFlee);
            }
            catch { Interlocked.Increment(ref _pThreadStumbles); }
        }

        /// <summary>LordEscort.Guard335.Attacker (watek glowny): obroncy chronionego wodza p nie biora za napastnika druzyny gracza ani partii rodu, gdy frakcja /
        /// rod p ma uczciwy pakt - 335 wymusza EngageParty z pominieciem ShouldConsiderAttacking (i SetDoNotAttackMainParty). Druzyna gracza: sam klucz 384.</summary>
        internal static bool PactBlocks(MobileParty p, MobileParty t)
        {
            var k = _pact;
            if (k == null || p == null || t == null) return false;
            try
            {
                if (t.IsMainParty) { if (!On) return false; }
                else if (!KClan || !Mine(t)) return false;
                else if (Hunting(t, k)) return false;   // 386b: partia rodu sama wystepuje przeciw nim - obrona chronionego dozwolona (jak w AttackPact)
                return Has(k, p);
            }
            catch { return false; }
        }

        /// <summary>Doba i zawarcie paktu (z NoAttack, watek glowny): dlugi rozkaz EngageParty omija inicjatywe (DefaultBehavior prowadzi do spotkania bez pytania
        /// modelu - np. rozkaz sprzed paktu albo przywrocony po obozie przez NightRest) - partia z paktem scigajaca partie rodu i partia rodu scigajaca partie z
        /// paktem (bez samoobrony) dostaja Hold + przemyslenie w najblizszej godzinie.</summary>
        private static void ClearOrders()
        {
            var k = _pact;
            if (k == null || !On) return;
            bool kc = KClan;
            try
            {
                foreach (var mp in MobileParty.AllLordParties)
                {
                    try
                    {
                        if (mp == null || !mp.IsActive || mp.IsMainParty || mp.Ai == null || mp.MapEvent != null || mp.AttachedTo != null) continue;
                        // 386c: takze GoAroundParty - od kiedy ThinkPact wycina te opcje u zrodla, gra jej nie odnowi, wiec zdjecie rozkazu sprzed paktu
                        // (albo przywroconego po obozie przez NightRest) ma sens; 386b slusznie tego nie robilo (gra nadawala cel od nowa co godzine)
                        bool go = mp.DefaultBehavior == AiBehavior.GoAroundParty;
                        if (mp.DefaultBehavior != AiBehavior.EngageParty && !go) continue;
                        var t = mp.TargetParty;
                        if (t == null) continue;
                        bool stop = false;
                        if (t.IsMainParty) stop = go && Has(k, mp);   // druzyna gracza: tylko podejscie (sam klucz 384); EngageParty na gracza jak dotad - poza ta regula
                        else if (!kc) continue;
                        else if (Mine(t)) stop = Has(k, mp) && !Hunting(t, k);
                        else if (Mine(mp)) stop = Has(k, t) && t.MapEvent == null && AggressorCore(t) == null && !ArmyHunts(mp, k);
                        if (!stop) continue;
                        mp.SetMoveModeHold();
                        mp.Ai.RethinkAtNextHourlyTick = true;
                        _pOrders++;
                    }
                    catch (Exception e) { Stumble("ClearOrders.Party", e); }
                }
            }
            catch (Exception e) { Stumble("ClearOrders", e); }
        }

        /// <summary>Linia doby: ile partii rodu pakt teraz chroni (karawany, druzyny, wiesniacy).</summary>
        private static string PactLine()
        {
            int car = 0, lords = 0, vil = 0, road = 0, aiP, myP;
            try
            {
                foreach (var mp in MobileParty.All)
                {
                    if (mp == null || !mp.IsActive || !Mine(mp)) continue;
                    if (mp.IsVillager) vil++; else if (mp.IsCaravan) car++; else { lords++; if (EnRoute(mp)) road++; }
                }
            }
            catch (Exception e) { Stumble("PactLine", e); }
            lock (PactGate) { aiP = _pAiSet.Count; myP = _pMySet.Count; }
            return " | 386 partie rodu (" + (KClan ? "klucz TAK" : "klucz NIE") + ", latka atak " + _pactHook + " / ucieczka " + _avoidHook + " / podejscie " + _thinkHook + "): ataki AI zablokowane " + _pAiCalls + " wywolan / " + aiP
                   + " partii, ataki rodu zablokowane " + _pMyCalls + " / " + myP + ", ucieczki zdjete " + _pFlee + ", rozkazy zdjete " + _pOrders + ", atak mimo paktu na partie rodu " + _pDespite
                   + ", atak partii rodu mimo paktu " + _pMyDespite + ", chronionych teraz " + (car + lords + vil) + " (karawany " + car + ", druzyny " + lords + ", wiesniacy " + vil + "; w drodze do cudzej armii " + road + "), 386c podejscia wyciete AI " + _pGoAi + " / rodu " + _pGoMy + " (zostalo mimo wyciecia " + _pGoFuse + "), potkniecia watkow " + _pThreadStumbles;
        }

        private static List<Entry> Covering(IFaction f, Clan c, Settlement st)
        {
            var list = new List<Entry>();
            int today = (int)Today;
            foreach (var e in _entries)
            {
                if (e.Until <= today || !Match(e, f, c)) continue;
                if (e.Scope == "*" || (st != null && e.Scope == st.StringId)) list.Add(e);
            }
            return list;
        }

        /// <summary>Nasluch OnSiegeEventStarted: oblezenie twierdzy rodu gracza wbrew umowie (zdrada - kara; uczciwy wpis - armia BK poza modelem celu - do kolejki
        /// zwiniecia, 384b) albo gracz oblega frakcje z PAKTEM (384b: umowa L gracza nie wiaze).</summary>
        internal static void OnSiegeStarted(SiegeEvent ev)
        {
            if (_entries.Count == 0) return;
            try
            {
                if (ev == null || ev.BesiegedSettlement == null || ev.BesiegerCamp == null) return;
                var st = ev.BesiegedSettlement;
                var lp = ev.BesiegerCamp.LeaderParty;
                if (lp == null) return;
                if (lp.IsMainParty) { PlayerBroke(st.MapFaction, st.OwnerClan, "oblezenie " + Name(st)); return; }
                if (st.OwnerClan != Clan.PlayerClan) return;
                AiBroke(Covering(lp.MapFaction, lp.ActualClan, st), "oblezenie " + Name(st) + " przez " + (lp.LeaderHero != null ? Name(lp.LeaderHero) : lp.StringId), st, ev);
            }
            catch (Exception e) { Stumble("OnSiegeStarted", e); }
        }

        /// <summary>Nasluch MapEventStarted: gracz zaczyna atak na druzyne / wies frakcji z paktem (bez samoobrony), albo AI z paktem atakuje druzyne gracza / wies rodu gracza.</summary>
        internal static void OnMapEventStarted(MapEvent me, PartyBase a, PartyBase d)
        {
            if (_queue.Count > 0 && a != null && d != null) FieldFight(a, d);   // 385
            if (_entries.Count == 0) return;
            try
            {
                if (a == null || d == null) return;
                if (a == PartyBase.MainParty)
                {
                    if (!PactWith(d.MapFaction, d.MobileParty != null ? d.MobileParty.ActualClan : (d.Settlement != null ? d.Settlement.OwnerClan : null))) return;
                    string self = SelfDefence(d);
                    if (self != null) { Log.Info(Tag + ": dzien " + F(Today) + " | gracz atakuje " + d.Name + " - samoobrona (" + self + "), pakt nie zlamany."); return; }
                    PlayerBroke(d.MapFaction, d.MobileParty != null ? d.MobileParty.ActualClan : (d.Settlement != null ? d.Settlement.OwnerClan : null), "atak na " + d.Name);
                    return;
                }
                // 386: partia rodu gracza (nie druzyna gracza) zaczyna atak na frakcje z paktem - gracz nie wydal rozkazu: tylko log i licznik, bez kary
                var am = a.MobileParty;
                if (am != null && KClan && Mine(am))
                {
                    if (!PactWith(d.MapFaction, d.MobileParty != null ? d.MobileParty.ActualClan : (d.Settlement != null ? d.Settlement.OwnerClan : null))) return;
                    string why = d.MobileParty != null ? AggressorCore(d.MobileParty) : null;
                    if (why == null && ArmyHunts(am, BuildPact(true))) why = "wojna korony - wodz jej armii wystepuje przeciw nim (386c)";
                    if (why == null) _pMyDespite++;
                    Log.Info(Tag + ": dzien " + F(Today) + " | partia rodu gracza " + a.Name + " atakuje " + d.Name + (why != null ? " - samoobrona (" + why + "), pakt nie zlamany." : " MIMO PAKTU (droga poza latka 386 i wyzerowanym celem osady 386b) - bez kary (gracz nie wydal rozkazu)."));
                    return;
                }
                bool onClan = d.MobileParty != null && KClan && Mine(d.MobileParty);   // 386
                bool onMe = d == PartyBase.MainParty || (d.Settlement != null && d.Settlement.OwnerClan == Clan.PlayerClan) || onClan;
                if (!onMe) return;
                var mp = a.MobileParty;
                var list = Covering(a.MapFaction, mp != null ? mp.ActualClan : null, null);
                list.RemoveAll(e => e.Scope != "*");
                if (onClan && list.Count > 0)
                {
                    // partia rodu sama scigala ich (samoobrona / dolaczenie) - tamci sie bronia, umowa nie naruszona
                    if (Hunting(d.MobileParty, BuildPact(true))) { Log.Info(Tag + ": dzien " + F(Today) + " | " + a.Name + " atakuje partie rodu gracza " + d.Name + ", ktora sama ich scigala - pakt nie naruszony."); return; }
                    if (list.Exists(e => !e.Betray)) _pDespite++;
                }
                AiBroke(list, "atak " + a.Name + " na " + d.Name, null, null);
            }
            catch (Exception e) { Stumble("OnMapEventStarted", e); }
        }

        private static bool PactWith(IFaction f, Clan c)
        {
            int today = (int)Today;
            foreach (var e in _entries) if (e.Until > today && e.Scope == "*" && Match(e, f, c)) return true;
            return false;
        }

        private static bool MineSt(Settlement t, Clan pc)
        {
            if (t == null || pc == null) return false;
            return t.OwnerClan == pc || (t.IsVillage && t.Village != null && t.Village.Bound != null && t.Village.Bound.OwnerClan == pc);
        }

        /// <summary>384b: atak gracza to samoobrona, gdy cel (albo lider jego armii) oblega twierdze rodu gracza (takze wypad - gra: StartPartyEncounter(MainParty,
        /// LeaderParty)), najezdza / idzie oblegac ziemie rodu gracza albo ma za cel druzyne gracza. Zwraca powod albo null.</summary>
        private static string SelfDefence(PartyBase d)
        {
            // 386: jedna regula z latka partii rodu (AggressorCore)
            try { return AggressorCore(d != null ? d.MobileParty : null); }
            catch (Exception e) { Stumble("SelfDefence", e); }
            return null;
        }

        private static void AiBroke(List<Entry> list, string what, Settlement st, SiegeEvent ev)
        {
            if (list == null || list.Count == 0) return;
            // 384b: kara tylko dla przysiegajacych ze ZDRADZIECKICH wpisow (dotad kazdy wpis pasujacej frakcji - uczciwy wodz tez dostawal -30)
            var lords = new HashSet<string>();
            bool honest = false;
            foreach (var e in list) { if (e.Betray) lords.Add(e.Lord); else honest = true; }
            if (honest)
            {
                // 384b: uczciwa umowa, a armia (BK FindArmyObjective / ArmySiegePersistence - poza wynikiem celu) i tak przyszla - oblezenie do zwiniecia
                bool queued = false;
                if (st != null && ev != null && st.StringId != null) { _relift[st.StringId] = ev.SiegeStartTime.ToHours; queued = true; }
                Log.Info(Tag + ": dzien " + F(Today) + " | UMOWA NARUSZONA przez AI mimo latki celu (" + what + ") - wpis uczciwy, brak kary (droga poza modelem celu)"
                         + (queued ? " | oblezenie w kolejce do zwiniecia (takt kampanii)" : " | atak - bez cofniecia") + ".");
            }
            if (lords.Count == 0) return;
            var names = new List<string>();
            foreach (var id in lords)
            {
                var h = Hero.Find(id);
                if (h == null || !h.IsAlive) continue;
                int n = Punish(h);
                names.Add(Name(h) + " (kara " + n + " glow rodow)");
            }
            _entries.RemoveAll(e => e.Betray && lords.Contains(e.Lord));
            SyncPact();   // 386 (wpisy zdrajcow nie sa w migawce - dla porzadku)
            Log.Info(Tag + ": dzien " + F(Today) + " | ZDRADA umowy: " + what + " | przysiegajacy " + string.Join(", ", names.ToArray()) + " - klamca, relacja -30, wpisy zdjete.");
            Log.Player("The enemy breaks the oath sworn for your prisoners: " + what.Replace("oblezenie", "siege of").Replace("atak", "attack").Replace(" na ", " on ").Replace(" przez ", " by ") + ".", true);
        }

        /// <summary>384b: zwiniecie oblezen wbrew uczciwej umowie - takt kampanii, bez misji, rozmowy i bitwy (gracza i przy obozie); jedna proba na oblezenie,
        /// jedno na takt.</summary>
        private static void Relift()
        {
            try
            {
                if (Campaign.Current == null || Mission.Current != null) return;
                var cm = Campaign.Current.ConversationManager;
                if (cm != null && (cm.IsConversationInProgress || cm.IsConversationFlowActive)) return;
                var mp = MobileParty.MainParty;
                if (mp == null || mp.MapEvent != null) return;
                var done = new List<string>();
                foreach (var kv in _relift)
                {
                    var st = Settlement.Find(kv.Key);
                    var ev = st != null ? st.SiegeEvent : null;
                    if (ev == null || Math.Abs(ev.SiegeStartTime.ToHours - kv.Value) > 0.05) { done.Add(kv.Key); continue; }   // to oblezenie juz sie skonczylo
                    if (SiegeSurrender374.CampBusy(st)) continue;   // bitwa przy obozie - czekamy
                    string lp = ev.BesiegerCamp != null && ev.BesiegerCamp.LeaderParty != null ? ev.BesiegerCamp.LeaderParty.Name.ToString() : "-";
                    string info;
                    bool ok = SiegeRansom.Lift384(st, out info);
                    if (ok) _dRelift++; else _dReliftFail++;
                    done.Add(kv.Key);
                    Log.Info(Tag + ": dzien " + F(Today) + " | " + Name(st) + " [" + st.StringId + "] oblezenie wbrew umowie (" + lp + ") - zwiniecie: " + info + ".");
                    if (ok) Log.Player("The besiegers of " + Name(st) + " remember the oath sworn for your prisoners and break camp.");
                    break;
                }
                foreach (var k in done) _relift.Remove(k);
            }
            catch (Exception e) { _relift.Clear(); Stumble("Relift", e); }
        }

        /// <summary>384b: przeglad doby - twierdze rodu gracza oblegane wbrew uczciwemu wpisowi (np. po wczytaniu - _relift nie jest w zapisie).</summary>
        private static void ScanViolations()
        {
            try
            {
                var pc = Clan.PlayerClan;
                if (pc == null || _entries.Count == 0) return;
                foreach (var st in pc.Settlements)
                {
                    var ev = st != null ? st.SiegeEvent : null;
                    if (ev == null || ev.BesiegerCamp == null || ev.BesiegerCamp.LeaderParty == null || st.StringId == null || _relift.ContainsKey(st.StringId)) continue;
                    var lp = ev.BesiegerCamp.LeaderParty;
                    if (lp.IsMainParty) continue;
                    foreach (var e in Covering(lp.MapFaction, lp.ActualClan, st))
                        if (!e.Betray)
                        {
                            _relift[st.StringId] = ev.SiegeStartTime.ToHours;
                            Log.Info(Tag + ": doba " + (int)Today + " | " + Name(st) + " oblegana wbrew umowie (" + lp.Name + ") - do zwiniecia.");
                            break;
                        }
                }
            }
            catch (Exception e) { Stumble("ScanViolations", e); }
        }

        private static void PlayerBroke(IFaction f, Clan c, string what)
        {
            // 384b: wiaze tylko PAKT (zasieg "*", przysiega wzajemna) - umowa L to przysiega wodza, gracz niczego nie slubowal
            var lords = new HashSet<string>();
            int today = (int)Today;
            foreach (var e in _entries) if (e.Until > today && e.Scope == "*" && Match(e, f, c)) lords.Add(e.Lord);
            if (lords.Count == 0) return;
            string rel = BrokeWord(lords, f);
            Log.Info(Tag + ": dzien " + F(Today) + " | GRACZ LAMIE PAKT (" + what + ") | " + rel + ".");
            Log.Player("You break the oath sworn over the prisoners. Men will remember it.", true);
        }

        /// <summary>Gracz zlamal slowo wobec przysiegajacych: ich wpisy precz (z rozejmem osady), relacja -20, wladca f -10, Honor -500 xp. Zwraca opis do logu.</summary>
        private static string BrokeWord(HashSet<string> lords, IFaction f)
        {
            var gone = new List<Entry>();
            foreach (var e in _entries) if (lords.Contains(e.Lord)) gone.Add(e);
            foreach (var e in gone)
            {
                _entries.Remove(e);
                if (e.Scope != "*" && e.Scope != "K" && !e.Betray) SiegeRansom.EndTruce384(Settlement.Find(e.Scope), e.Key);
            }
            SyncRealm();
            var rel = new List<string> { "wpisy zdjete " + gone.Count };
            try
            {
                foreach (var id in lords)
                {
                    var h = Hero.Find(id);
                    if (h != null && h.IsAlive) { ChangeRelationAction.ApplyPlayerRelation(h, -20); rel.Add(Name(h) + " -20"); }
                }
                var ruler = f != null ? f.Leader : null;
                if (ruler != null && ruler.IsAlive && ruler != Hero.MainHero && !lords.Contains(ruler.StringId)) { ChangeRelationAction.ApplyPlayerRelation(ruler, -10); rel.Add(Name(ruler) + " -10"); }
                TraitLevelingHelper.OnIncidentResolved(DefaultTraits.Honor, -500);
                rel.Add("Honor gracza -500 xp");
                string hb = HostBroke(lords);   // 385c
                if (hb != null) rel.Add(hb);
            }
            catch (Exception e) { Stumble("BrokeWord", e); }
            return string.Join(", ", rel.ToArray());
        }

        /// <summary>Doba: wygasle wpisy precz, pakt - odswiezenie SetDoNotAttackMainParty, linia stanu (gdy sa wpisy albo cos sie zdarzylo).</summary>
        internal static void Daily()
        {
            try
            {
                int today = (int)Today;
                int before = _entries.Count;
                _entries.RemoveAll(e => e.Until <= today);
                SyncRealm();
                var dead = new List<string>();
                foreach (var kv in _cool) if (kv.Value.End <= Today) dead.Add(kv.Key);
                foreach (var k in dead) _cool.Remove(k);
                if (_entries.Count > 0 && On) { NoAttack(); ScanViolations(); }
                if (_entries.Count > 0 || before > 0 || _dZero + _dZeroMine > 0 || _queue.Count > 0 || _dRelift + _dReliftFail > 0 || _dRealmBlock > 0 || _dPick > 0 || _pAiCalls + _pMyCalls + _pFlee + _pOrders + _pDespite + _pMyDespite + _pGoAi + _pGoMy > 0)
                {
                    int pact = 0, st = 0, bet = 0, realm = 0;
                    foreach (var e in _entries) { if (e.Betray) bet++; else if (e.Scope == "*") pact++; else if (e.Scope == "K") realm++; else st++; }
                    Log.Info(Tag + ": doba " + today + " | wpisy umow " + _entries.Count + " (pakt ziem " + pact + ", rozejm osady " + st + ", rozejm krolestw " + realm + ", pilnowane zdrady " + bet + "; wygaslo " + (before - _entries.Count) + ")"
                             + " | 385: wojny AI zatrzymane rozejmem krolestw " + _dRealmBlock + ", okna wyboru " + _dPick
                             + " | cele AI wyzerowane paktem " + _dZero + " (cele druzyn rodu na ich osadach " + _dZeroMine + ") | druzyny bez ataku na gracza " + _dNoAttack + " | kolejka " + _queue.Count + " | oblezenia wbrew umowie zwiniete " + _dRelift + " (nieudane " + _dReliftFail + ", czeka " + _relift.Count + ")" + (_pact != null || _pAiCalls + _pMyCalls + _pFlee + _pOrders + _pDespite + _pMyDespite + _pGoAi + _pGoMy > 0 ? PactLine() : "") + " | potkniecia " + _stumbles + ".");
                }
                _dZero = 0; _dZeroMine = 0; _dNoAttack = 0; _dRelift = 0; _dReliftFail = 0; _dRealmBlock = 0; _dPick = 0;
                PactZero();   // 386
            }
            catch (Exception e) { Stumble("Daily", e); }
            try { HostDaily((int)Today); } catch (Exception e) { Stumble("HostDaily", e); }   // 385c: po wpisach umow (zlamane slowo -> stan B juz ustawiony)
        }

        // ------------------------------------------------------------ zapis ("arm_k384")
        private static readonly char[] Bad = { '|', ';', ':', ',' };
        private static string D(double v) { return v.ToString("R", Inv); }
        private static bool P(string s, out double v) { return double.TryParse(s, NumberStyles.Float, Inv, out v); }
        private static bool Ok(string s) { return s != null && s.IndexOfAny(Bad) < 0; }

        /// <summary>"v2|Q:st:wodz:start:zadanie:jency,..:zloto:ziemie,..:zdrada:p:doba:V:C:dni:moi,..:flagi:zakladnik;...(O - oferta w menu)|klucz:zasieg:do:wodz:zdrada;...|st:wodz:start:koniec;..."
        /// 385: v2 - rekord umowy 17 pol (moi ludzie; flagi: znak 0 - przysiega 0/1; zakladnik - 385c: id bohatera albo puste) + opcjonalna piata sekcja zakladnikow (HostExport); st puste = rozmowa w polu. v1 (14 pol) czytany.</summary>
        internal static string Export()
        {
            try
            {
                var sb = new StringBuilder(256);
                sb.Append("v2|");
                bool f = true;
                var all = new List<Deal>(_queue);
                if (_offer != null) all.Add(_offer);
                foreach (var d in all)
                {
                    if (!Ok(d.St) || !Ok(d.Lord)) continue;
                    var pr = d.Pris.FindAll(Ok); var fi = d.Fiefs.FindAll(Ok); var mn = d.Men.FindAll(Ok);
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(ReferenceEquals(d, _offer) ? 'O' : 'Q').Append(':').Append(d.St).Append(':').Append(d.Lord).Append(':').Append(D(d.Start)).Append(':').Append(d.Want)
                      .Append(':').Append(string.Join(",", pr.ToArray())).Append(':').Append(d.Gold.ToString(Inv)).Append(':').Append(string.Join(",", fi.ToArray()))
                      .Append(':').Append(d.Betray ? '1' : '0').Append(':').Append(D(d.P)).Append(':').Append(D(d.Day)).Append(':').Append(D(d.V)).Append(':').Append(D(d.C)).Append(':').Append(d.Days.ToString(Inv))
                      .Append(':').Append(string.Join(",", mn.ToArray())).Append(':').Append(d.Oath ? '1' : '0').Append(':').Append(Ok(d.Host) ? d.Host : "");   // 385c: pole 17 - zakladnik
                }
                sb.Append('|'); f = true;
                int today = (int)Today;
                foreach (var e in _entries)
                {
                    if (e.Until <= today || !Ok(e.Key.Replace("c:", "c_")) || !Ok(e.Scope) || !Ok(e.Lord)) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(e.Key.Replace(":", "~")).Append(':').Append(e.Scope).Append(':').Append(e.Until.ToString(Inv)).Append(':').Append(e.Lord).Append(':').Append(e.Betray ? '1' : '0');
                }
                sb.Append('|'); f = true;
                foreach (var kv in _cool)
                {
                    if (kv.Value.End <= Today) continue;
                    var x = kv.Key.Split('|');
                    if (x.Length != 2 || !Ok(x[0]) || !Ok(x[1])) continue;
                    if (!f) sb.Append(';'); f = false;
                    sb.Append(x[0]).Append(':').Append(x[1]).Append(':').Append(D(kv.Value.Start)).Append(':').Append(D(kv.Value.End));
                }
                if (_hosts.Count > 0) { sb.Append('|'); HostExport(sb); }   // 385c: piata sekcja tylko, gdy sa zakladnicy (starszy DLL czyta 4)
                return sb.ToString();
            }
            catch (Exception e) { Stumble("Export", e); return ""; }
        }

        internal static void Import(string data)
        {
            Reset();
            int bad = 0;
            try
            {
                if (string.IsNullOrEmpty(data)) return;
                var f = data.Split('|');
                if (f.Length < 4 || (f[0] != "v1" && f[0] != "v2")) { Log.Info(Tag + ": zapis nieczytelny - od zera."); return; }
                foreach (var p in f[1].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double s, pp, dy, v, c; int g, dd;
                    if ((x.Length == 14 || x.Length == 17) && (x[0] == "Q" || x[0] == "O") && P(x[3], out s) && x[4].Length == 1 && int.TryParse(x[6], NumberStyles.Integer, Inv, out g)
                        && P(x[9], out pp) && P(x[10], out dy) && P(x[11], out v) && P(x[12], out c) && int.TryParse(x[13], NumberStyles.Integer, Inv, out dd))
                    {
                        var d = new Deal { St = x[1], Lord = x[2], Start = s, Want = x[4][0], Gold = g, Betray = x[8] == "1", P = pp, Day = dy, V = v, C = c, Days = dd };
                        foreach (var id in x[5].Split(',')) if (id.Length > 0) d.Pris.Add(id);
                        foreach (var id in x[7].Split(',')) if (id.Length > 0) d.Fiefs.Add(id);
                        if (x.Length == 17)
                        {
                            foreach (var id in x[14].Split(',')) if (id.Length > 0) d.Men.Add(id);
                            d.Oath = x[15].Length > 0 && x[15][0] == '1';
                            d.Host = x[16];   // 385c
                        }
                        if (x[0] == "O") _offer = d; else _queue.Add(d);
                    }
                    else bad++;
                }
                foreach (var p in f[2].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); int u;
                    if (x.Length == 5 && int.TryParse(x[2], NumberStyles.Integer, Inv, out u)) _entries.Add(new Entry { Key = x[0].Replace("~", ":"), Scope = x[1], Until = u, Lord = x[3], Betray = x[4] == "1" });
                    else bad++;
                }
                foreach (var p in f[3].Split(';'))
                {
                    if (p.Length == 0) continue;
                    var x = p.Split(':'); double s, e;
                    if (x.Length == 4 && P(x[2], out s) && P(x[3], out e)) _cool[x[0] + "|" + x[1]] = new Cool { Start = s, End = e }; else bad++;
                }
                if (f.Length > 4) bad += HostImport(f[4]);   // 385c
                SyncRealm();
                if (_queue.Count + _entries.Count + _cool.Count + _hosts.Count > 0 || _offer != null || bad > 0)
                    Log.Info(Tag + ": wczytano z zapisu - kolejka " + _queue.Count + (_offer != null ? ", oferta w menu" : "") + ", wpisy umow " + _entries.Count + ", przerwy " + _cool.Count + (_hosts.Count > 0 ? ", zakladnicy " + _hosts.Count : "") + (bad > 0 ? " (bledne " + bad + ")" : "") + ".");
            }
            catch (Exception e) { Stumble("Import", e); }
        }
    }
}
