using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace CrashScribe
{
    /// <summary>
    /// SWATY ROT SZYBCIEJ (Z.3 pozycja 1, 13.10; rozpoznanie docs/zakonczenie/ROZPOZNANIE-Z3-CUDZY-KOD-2026-10-13.md).
    /// Ta sama REGULA i te same SZANSE co w ROT, w tej samej dobie. "Te same pary i ten sam ciag losowy" jest prawda tylko POZA GRA
    /// (sam ROT + BK na danych testu: 57 tys. wywolan, 0 roznic). W GRZE NIE (pomiar 13.10, CHANGELOG "Z.3 SWATY ROT - pomiar w grze"):
    /// model BK sam zuzywa generator gry w ocenie szansy (NpcCoupleMarriageChance: 1 krok w 130-156 wywolaniach na dobe, takze przy
    /// wyniku 0; ktore wywolanie w BK losuje - nie ustalone), a my te ocene pomijamy dla lorda z zyjacym malzonkiem (skrot (c)); do tego
    /// pamieci dnia BK (_spouseScoreCache, _isClanHeirCache) sprawiaja, ze nawet dwa przebiegi tej samej drogi od tego samego stanu
    /// generatora zuzywaja rozna liczbe krokow. Pary danego dnia nie sa wiec tymi, ktore wylosowalby sam ROT - tak jak roznia sie dwie
    /// rozgrywki. Zdania nizej o "tym samym kroku generatora" opisuja NASZE wywolania MBRandom wobec wywolan ROT, nie stan generatora w grze.
    ///
    /// Oryginal (ROT 8.1.8, ROTRelationshipsBehavior.CheckNPCMarriages, sluchacz DailyTickClan; 448 bajtow IL):
    ///   dla kazdego lorda rodu z CanMarry(): kandydaci = _unmarriedHeroes.Where(h => model.IsCoupleSuitableForMarriage(lord, h));
    ///   GetRandomElementInefficiently (gra): IsEmpty (do pierwszego trafienia) + Count (cala lista) + ElementAt(MBRandom.RandomInt(Count))
    ///   - warunek pary liczony ok. 1.6 raza na kandydata; potem NpcCoupleMarriageChance, MBRandom.RandomFloat tylko gdy szansa > 0,
    ///   kontrola zalotow (stan >= 3) i MarriageAction.Apply + break.
    /// Lista _unmarriedHeroes: wszyscy zywi z CanMarry() spoza rodu gracza, budowana raz na dobe (DailyTick) - BK nie pyta o malzonka,
    /// wiec sa na niej takze zonaci i kazdy z nich sam szuka pary, choc szansa slubu wynosi u niego zawsze 0.
    ///
    /// Nasza droga (prefiks, oryginal pominiety) wola TE SAME metody modelu z tymi samymi argumentami, tylko rzadziej:
    ///  (a) jedno przejscie: trafienia do listy w kolejnosci _unmarriedHeroes, potem MBRandom.RandomInt(liczba) i element o tym numerze
    ///      - to samo wywolanie generatora i ten sam element co ElementAt (warunek pary jest czysta funkcja stanu - nizej);
    ///  (b) podzial wedlug plci: para tej samej plci to zawsze "nie" (DefaultMarriageModel.IsCoupleSuitableForMarriage; ROTMarriageModel
    ///      konczy na _previousModel, BK tej metody nie nadpisuje) - takich kandydatow nie pytamy wcale;
    ///  (c) lord z zyjacym malzonkiem (Spouse != null i IsAlive - wtedy BK HasPrimarySpouse zwraca true bez wolania czegokolwiek, a
    ///      BKMarriageModel.NpcCoupleMarriageChance daje 0 przez IsMarriageAdequate = -10000): oryginal losuje kandydata (jeden krok
    ///      generatora, gdy jest ktokolwiek), liczy szanse 0 i idzie dalej BEZ RandomFloat. My szukamy tylko pierwszego trafienia i
    ///      zuzywamy jeden krok (MBRandom.RandomInt(1); MBFastRandom.Next(int) to zawsze dokladnie jeden XorShift, bez wzgledu na argument).
    ///      W grze ocena szansy BK, ktorej tu nie wolamy, sama potrafi zuzyc krok generatora - stad inny stan losowy niz w oryginale (wyzej).
    /// Reszta (szansa, RandomFloat, kontrola zalotow, MarriageAction.Apply, break) - przepisana 1:1 (jedyny wyjatek: prog rzutu wolnego lorda mnozy HAMULEC - nizej; staly mnoznik tylko przy czynnej regule, regulator zawsze).
    ///
    /// Dlaczego warunek pary mozna liczyc rzadziej (sprawdzone w dekompilacji gry 1.4.8, ROT, BK Redux, BKROTPatch):
    ///  - nie losuje (w grze 13.10: 0 krokow generatora w ok. 880 tys. ocen na dobe) i nie zmienia stanu gry: ROTMarriageModel (rody, wojna, wiek, wielkie rody, kultura, odleglosc domow, Mur) ->
    ///    DefaultMarriageModel (rody, plec, pokrewienstwo, Romance.GetCourtedHeroInOtherClan x2, CanMarry x2 -> model + pytanie do
    ///    zadan i spraw: same odczyty);
    ///  - jedyny slad: Hero.HomeSettlement liczy sie przy pierwszym odczycie (pole _homeSettlement, [CachedData] - puste po wczytaniu,
    ///    po zmianie rodu). Dlatego skroty (b) i (c) dotycza tylko bohaterow z policzonym domem: kandydat z pustym polem (tej samej
    ///    plci, a po skrocie (c) kazdy niezapytany) jest oceniany jak w oryginale, lord z pustym polem przeglada cala liste;
    ///  - Armoury RaceLaw.CouplePostfix (postfiks na obu poziomach modelu) zmienia tylko "tak" na "nie" dla roznych ras i zapisuje pare
    ///    do zbioru do linii "Rasy: dzien" - przy skrocie (c) ta linia nie liczy juz par, w ktorych swat ma malzonka (nie mogly sie pobrac).
    /// Podzial wedlug plci i spis pustych domow to jedno tanie przejscie po liscie (2 odczyty pola na bohatera) na wywolanie rodu, nie
    /// raz na dobe: Hero.IsFemale ma publiczny setter, a pole domu gra zeruje miedzy rodami (rody tykaja rozlozone na cala dobe).
    ///
    /// Straze (jedna linia "Mends: swaty ROT (Z.3): PODPIETY / BRAK" przy starcie sesji; gdy cos nie pasuje - liczy oryginal ROT):
    ///  dlugosci IL znanych metod (inna = inna logika), model na wierzchu ROTMarriageModel, pod nim pochodna BKMarriageModel z
    ///  IsCoupleSuitableForMarriage z gry; obce latki Harmony na lancuchu oceny pary (poza RaceLaw; lancuch = metody modelu obu
    ///  poziomow, Hero.CanMarry, Romance.GetCourtedHeroInOtherClan, Hero.HomeSettlement i UpdateHomeSettlement) albo obcy prefiks bool / transpiler
    ///  na samej metodzie ROT - nasza droga stoi; obca latka na lancuchu szansy (np. AIInfluence: prefiks DefaultMarriageModel
    ///  .NpcCoupleMarriageChance) - wylaczony tylko skrot (c). Obce latki sprawdzane przy pierwszym wywolaniu sesji i raz na dobe.
    /// Wyjatek w naszej drodze: stan generatora cofniety do wejscia, potkniecie policzone, ten rod liczy oryginal (latka zostaje).
    /// MarriageAction.Apply wolane poza ta ochrona (jak w oryginale) i przez delegata - metoda ma 9 bajtow IL, wolana wprost
    /// zostalaby wklejona w nasz kod i ominela ewentualna cudza latke.
    ///
    /// BIEG CIENIA (MarriageFastShadow): nasza droga liczy "co by wybrala" (bez slubu), stan generatora wraca do wejscia i decyduje
    /// oryginal ROT. Porownanie: para (albo brak) i stan generatora w chwili decyzji - u oryginalu lapany na wejsciu
    /// MarriageAction.ApplyInternal (albo na koncu metody, gdy slubu nie ma). Ten sam stan po tym samym starcie = ta sama liczba krokow.
    /// Linia doby: "Swaty ROT (Z.3): klanow N, par stara droga A, nowa B, roznic R, czas stara X ms, nowa Y ms; ...".
    /// To NARZEDZIE POMIARU, nie prog "roznic 0": poza gra roznic jest 0, w grze ok. 85 rodow z 330 na dobe rozni sie stanem generatora
    /// (zwykle "bez slubu / bez slubu" - rozny jest stan losowy, nie para), bo BK losuje w ocenie szansy (na gorze).
    ///
    /// TYLKO KANDYDACI Z SZANSA (MarriageOnlyViableCandidates, prosba Jeffa 13.10) - ZMIANA ROZGRYWKI, nie przyspieszenie.
    /// Oryginal losuje kandydata z calej listy "pasujacych" i dopiero potem pyta o szanse. Z BK na liscie sa tez bohaterowie, z ktorymi
    /// slubu byc nie moze: (1) kandydat z zyjacym malzonkiem (BKMarriageModel.IsSuitableForMarriage nie pyta o malzonka, a
    /// IsMarriageAdequate daje -10000 za secondHero.HasPrimarySpouse -> szansa 0), (2) kandydat wolny, ale odrzucony wynikiem BK
    /// (IsMarriageAdequate: 1.1 x wynik swata - wynik kandydata +/- kultura +/- wiara nie wieksze od 0 -> szansa 0), (3) kandydat w zalotach
    /// stanu >= 3 (kontrola ROT po udanym rzucie). Trafienie w takiego = dzienna proba wolnego lorda przepada. ROT bez BK tych strat nie
    /// ma (model gry nie wpuszcza zonatych na liste, szansa pary pasujacej jest zawsze > 0).
    /// Przy wlaczonym kluczu WOLNY lord (bez zyjacego malzonka) losuje tylko sposrod kandydatow, z ktorymi slub jest mozliwy: z tej samej
    /// listy kandydatow co dotad losujemy bez zwracania, az trafi sie pierwszy z szansa > 0 (ta sama metoda modelu, ktora oryginal pyta po
    /// losowaniu; zonaty i zalotnik odpadaja bez pytania modelu) - rozklad rowny po kandydatach z szansa, a szanse liczymy tylko dla
    /// wylosowanych (srednio 1-2 na lorda zamiast calej listy). Pierwsze losowanie to dokladnie losowanie starej reguly - stad licznik
    /// "stara regula trafilaby w kandydata bez szans". Dalej: RandomFloat &lt; szansa x mnoznik HAMULCA (nizej; 1.0 = sama szansa), kontrola zalotow, MarriageAction.Apply, break.
    /// Lord z zyjacym malzonkiem - jak dotad (skrot (c)). Wolny lord sam w zalotach stanu >= 3 nie losuje (i tak nie moglby sie ozenic).
    /// Ciag losowy i pary sa wtedy INNE niz w oryginale (zamierzone). Regula dziala tylko przy znanym lancuchu szansy bez obcych latek
    /// (ten sam warunek co skrot dla zonatych) i NIE dziala w biegu cienia (tam decyduje oryginal ROT, a nasza droga liczy stara regula,
    /// zeby obie drogi porownywaly te sama regule) - w obu razach losowanie jak w oryginale i slowo w linii startu i w linii doby.
    /// Klucz wylaczony = losowanie ROT, jak przed ta zmiana (ta sama regula co oryginal; te same pary tylko poza gra - na gorze); liczniki
    /// prob i trafien bez szans sa wtedy zwyklym odczytem tego, co wylosowala nasza droga stara regula (zadnych dodatkowych pytan do modelu).
    ///
    /// HAMULEC (MarriageChanceScale, decyzja Jeffa 13.10: "Przyhamuj, aby sluby zawieraly sie rzadziej") - ZMIANA ROZGRYWKI.
    /// Pomiar roczny (364 doby): 113 slubow AI-AI przed regula "tylko kandydaci z szansa", 215 z nia (ok. 0.59 na dobe, prawie liniowo) -
    /// wolni szlachcice, takze kandydatki dla gracza, koncza sie ok. 2 razy szybciej. Mnoznik (13.10 domyslnie 0.7, od 14.10 0.17 - nizej; zakres 0.1-1.0) obniza prog
    /// rzutu "RandomFloat &lt; szansa" - staly mnoznik (MarriageTargetPerYear = 0) TYLKO dla wolnego lorda przy CZYNNEJ regule (sciezka PickViable -> rzut w Decide; regulator - nizej). Kandydat jest
    /// wybierany jak dotad (sposrod tych z szansa > 0, bez mnoznika), rzut zuzywa ten sam jeden krok generatora. Szansa pary jest rzedu
    /// 0.001 na probe, wiec liczba slubow idzie w przyblizeniu wprost za mnoznikiem: 0.7 = ok. 150 na rok (szacunek, nie pomiar),
    /// 0.5 = mniej wiecej jak przed regula, 1.0 = prog bez zmian (dokladnie zachowanie sprzed hamulca). Lord z zyjacym malzonkiem, regula
    /// wylaczona / stojaca, bieg cienia i rod oddany oryginalowi po potknieciu - bez mnoznika (droga ROT bez zmian).
    /// Wartosc spoza zakresu: przyciecie i jedna linia w logu (raz na proces); mnoznik w linii startu i w linii doby ("mnoznik szansy X").
    /// DECYZJA JEFFA 14.10 ("sluby - 48 slubow" na rok, zasada "1 dzien = 1 dzien"; rozpoznanie docs/pytania-2026-10-14/STARZENIE-I-CZAS-ZYCIA.md
    /// rozdz. 3 pkt 3): domyslna 0.7 -> 0.17. Pomiar przy 0.7 (bieg dwuletni 13/14.10): 291 slubow AI-AI w 728 dob (159 + 132) - wszystkie ta droga
    /// (linia Armoury "Sluby AI (169c)", ktora liczy kazdy slub spoza rodu gracza, daje te sama sume 291; zonaty lord ma szanse 0, a ROT wylacza swaty gry -
    /// innej drogi slubow AI-AI w tym zestawie modow nie ma; BK x1.25 siedzi w szansie, ktora hamulec mnozy). Liniowo 0.7 x 48 / 146 = 0.23 (= 84 / 364),
    /// ale przy rzadszych slubach pula wolnych lordow rosnie (kazdy niezawarty slub = 2 wolnych, ktorzy dalej probuja) - model z dob biegu dwuletniego
    /// (ten sam model przewiduje dla 1.0 212-220 slubow w roku 1, pomiar 13.10: 215) daje przy 0.23 55-66 na rok, przy 0.17 42-52 (rok 1 i 2) - stad 0.17.
    /// Oferty slubu dla rodu gracza (gra, MarriageOfferCampaignBehavior) - osobno, caly proces x 84 / rok (MarriageOfferYear.cs).
    ///
    /// REGULATOR (recenzja 14.10, klucz MarriageTargetPerYear = 48; 0 = staly mnoznik jak wyzej). Staly mnoznik trzyma ok. 48 tylko w roku 1-2: w biegu
    /// dwuletnim wolnych lordow z proba bylo stale 650-700 przy 291 slubach (582 osoby) - doplyw ok. 300 wolnych na rok (dorastajacy, wdowcy); przy ok. 48 slubach
    /// (96 osob) pula rosnie ok. 200 na rok, a z nia sluby (model recenzji: rok 3 51-62, rok 6 64-86). Dlatego mnoznik liczony co dobe z celu:
    ///  - kazdy rzut wolnego lorda NASZEJ drogi (bez biegu cienia; takze gdy regula "tylko kandydaci z szansa" stoi - wtedy rzut z kandydatem jak w ROT)
    ///    dodaje do sumy doby szanse modelu num = oczekiwana liczbe slubow przy mnozniku 1.0 (rzuty sa niezalezne, wiec suma szans = oczekiwana liczba slubow);
    ///  - srednia tej sumy z ostatnich 28 dob (pierwsze doby - srednia z tylu, ile jest; doba z rodami liczonymi przez oryginal ROT - stoi albo potkniecie - i
    ///    pierwsza linia po wczytaniu, ktora obejmuje tylko czesc rodow, pominiete);
    ///  - mnoznik nastepnej doby = (cel / CampaignTime.DaysInYear) / srednia, najwyzej 1.0 (nie szybciej niz ROT z regula), najmniej 0.02. Zanim jest srednia
    ///    (1-2 doby po wczytaniu) - MarriageChanceScale (0.17).
    /// Oczekiwana liczba slubow przy mnozniku dnia = cel na dobe (z dokladnoscia zmiany puli w 28 dobach) - bez modelu puli, przy kazdym r, dlugosci roku i
    /// wzroscie puli. Pamiec sesji (bez zapisu). Linia doby Z.3: mnoznik dnia, oczekiwane przy 1.0 i przy mnozniku, srednia, mnoznik na jutro.
    /// Nie dziala tylko, gdy liczy oryginal ROT (MarriageFast = false, obca latka na lancuchu oceny pary albo prefiks na CheckNPCMarriages, bieg cienia).
    ///
    /// LOSOWANIE LENIWE (NW O1, noc 16.10, klucz MarriageLazyDraw; rozpoznanie wydajnosci WGRANIA 61: swaty ok. 0.96 s na dobe, ok. 1.1 mln ocen pary,
    /// z czego wiekszosc to pelne przejscia wolnych lordow przy regule wyzej). Wolny lord przy czynnej regule nie przechodzi calej listy przeciwnej
    /// plci: losuje z niej bez zwracania i ocenia pare tylko dla wylosowanego (PickViableLazy) - ten sam rozklad wyniku i te same liczniki,
    /// inny ciag losowy. Kontrola R9 co 64. losowanie (pelne przejscie obok, linia doby: probek starej drogi, rozjazdow).
    ///
    /// Wylaczniki: MarriageFast (true; false = oryginal ROT bez latki - wtedy nie dziala tez regula wyzej), MarriageFastShadow (false),
    /// MarriageOnlyViableCandidates (true), MarriageTargetPerYear (48; 0 = staly MarriageChanceScale), MarriageChanceScale (0.17 - start regulatora albo
    /// staly hamulec; 1.0 = bez hamulca) w ModuleData/CrashScribe.settings.xml. Bez zapisu w grze.
    /// </summary>
    internal static class MarriageFast
    {
        private const string Tag = "Swaty ROT (Z.3)";
        private const string OurOwner = "com.jeff.crashscribe";   // ten sam identyfikator Harmony co SubModuleMain.HarmonyId

        // dlugosci IL znanych metod (ROT 8.1.8, BK Redux, gra 1.4.8) - inna dlugosc = inna logika = straz nie puszcza
        private const int IlCheck = 448;       // ROT ROTRelationshipsBehavior.CheckNPCMarriages
        private const int IlRotCouple = 475;   // ROT ROTMarriageModel.IsCoupleSuitableForMarriage
        private const int IlDefCouple = 148;   // gra DefaultMarriageModel.IsCoupleSuitableForMarriage
        private const int IlPickRandom = 36;   // gra Extensions.GetRandomElementInefficiently
        private const int IlRotChance = 19;    // ROT ROTMarriageModel.NpcCoupleMarriageChance
        private const int IlBkChance = 48;     // BK BKMarriageModel.NpcCoupleMarriageChance
        private const int IlBkAdequate = 453;  // BK BKMarriageModel.IsMarriageAdequate
        private const int IlBkSpouse = 48;     // BK HeroExtensions.HasPrimarySpouse
        private const int IlRngNext = 73;      // gra MBFastRandom.Next(int)

        private static readonly Type[] H2 = { typeof(Hero), typeof(Hero) };

        // ---- dostep do cudzego kodu (raz na proces) ----
        private static Harmony _h;
        private static bool _resolved, _patched, _shadowPatched;
        private static string _resolveFail;
        private static MethodInfo _mCheck;
        private static FieldInfo _fList;                                 // ROTRelationshipsBehavior._unmarriedHeroes
        private static Func<Game, MBFastRandom> _getRng;                 // Game.RandomGenerator (internal) - ten sam generator co MBRandom
        private static AccessTools.FieldRef<MBFastRandom, uint> _rx, _ry, _rz, _rw;
        private static AccessTools.FieldRef<Hero, Settlement> _home;     // Hero._homeSettlement (pamiec HomeSettlement)
        private static Action<Hero, Hero, bool> _apply;                  // MarriageAction.Apply

        // ---- stan sesji kampanii ----
        private static bool _ready;            // straze przeszly - prefiks pracuje
        private static bool _shadow;           // bieg cienia w tej sesji
        private static bool _spouseModelOk;    // lancuch szansy zgodny ze znanym
        private static bool _spouseOk;         // skrot dla zonatych czynny (model + brak obcych latek)
        private static bool _standDown;        // obca latka na lancuchu - liczy oryginal
        private static string _standWhy;
        private static bool _patchCheckDue;
        private static MethodBase[] _chainCouple, _chainChance;
        private static bool _viableCfg;        // klucz MarriageOnlyViableCandidates wlaczony i sesja nie jest biegiem cienia
        private static bool _viableOn;         // regula "tylko kandydaci z szansa" czynna teraz (_viableCfg i skrot dla zonatych czynny)
        private static float _scale = 1f;      // hamulec: mnoznik szansy w rzucie wolnego lorda tej doby (staly: MarriageChanceScale; regulator: liczony co dobe); dziala przy BrakeOn()
        private static bool _scaleWarned;      // linia o przycieciu juz wypisana (raz na proces - ustawienia czytane raz na proces)
        // regulator (MarriageTargetPerYear > 0): cel, rok, mnoznik startowy, suma szans doby przy 1.0 i przy mnozniku, srednia 28 dob, liczniki
        private static int _target;            // slubow na rok (0 = staly MarriageChanceScale)
        private static bool _regOn;            // regulator czynny w tej sesji (cel > 0, nie bieg cienia)
        private static int _year = 364;        // CampaignTime.DaysInYear tej sesji
        private static float _scaleCfg = 1f;   // MarriageChanceScale po przycieciu (start regulatora albo staly hamulec)
        private static double _dayExp, _dayExpS;   // doba: suma szans rzutow wolnych lordow z hamulcem przy 1.0 i przy mnozniku dnia
        private static int _dayRolls, _dayOrig;    // doba: rzuty z hamulcem; wywolania rodu oddane oryginalowi (stoi / potkniecie) - doba bez wpisu do sredniej
        private static double _avg;            // srednia sumy szans przy 1.0 (ostatnie RegWindow dob)
        private static int _avgN, _regLines;   // dob w sredniej; linii doby od wczytania (pierwsza obejmuje tylko czesc rodow)

        // hamulec: dozwolony zakres MarriageChanceScale i wartosc, gdy w pliku stoi cos, co nie jest liczba (NaN)
        private const float ScaleMin = 0.1f, ScaleMax = 1f, ScaleDefault = 0.17f;   // domyslna jak w Config.cs (decyzja Jeffa 14.10 - ok. 48 slubow AI na rok w roku 1-2)
        private const float RegMin = 0.02f;    // regulator: najnizszy mnoznik (pula wolnych ok. 7 x wieksza niz w roku 1 - daleko poza horyzontem gry)
        private const int RegWindow = 28;      // regulator: srednia z 28 dob (zmiana puli wolnych to miesiace)
        private const int TargetMax = 2000;    // regulator: cel ponad tym - przyciecie (blad w pliku ustawien)

        // ---- bufory (jeden watek kampanii; _busy pilnuje ponownego wejscia) ----
        private static bool _busy;
        private static readonly List<Hero> _lords = new List<Hero>(16);
        private static readonly List<Hero> _males = new List<Hero>(1024);
        private static readonly List<Hero> _females = new List<Hero>(1024);
        private static readonly List<Hero> _nullHomes = new List<Hero>();
        private static readonly List<Hero> _cand = new List<Hero>(512);
        private static readonly HashSet<Hero> _court = new HashSet<Hero>();   // bohaterowie w zalotach stanu >= 3 (na czas wywolania rodu)
        private static bool _courtBuilt;

        // ---- liczniki doby i sesji ----
        private static int _dayClans, _dayNewPairs, _dayOldPairs, _dayDiff, _dayBothThrew;
        private static int _dayLords, _daySpouse, _dayFull, _dayNullMax;
        private static long _dayEvals, _dayNewTicks, _dayOldTicks;
        private static int _totClans, _totDiff, _diffLines;
        private static int _stumbles, _stumblesDay;
        // proby wolnych lordow (bez zyjacego malzonka, z co najmniej jednym kandydatem): ile, w ilu losowanie starej reguly trafilo w kandydata
        // bez szans, ilu nie mialo zadnego kandydata z szansa i ile razy pytalismy model o szanse (dwa ostatnie tylko przy nowej regule)
        private static int _dayFree, _dayOldDead, _dayNoViable, _dayChanceEvals;
        private static int _totPairs;
        // NW O1 (losowanie leniwe, klucz MarriageLazyDraw): stan sesji, pula losowania, liczniki doby i kontrola R9 (co LazySampleEvery-te losowanie)
        private static bool _lazyOn;
        private static readonly List<Hero> _pool = new List<Hero>(1024);
        private const int LazySampleEvery = 64;
        private static long _lazySeq, _dayLazySampleEvals;
        private static int _dayLazy, _dayLazySamples, _dayLazyDiff, _totLazyDiff, _lazyDiffLines;

        private struct Rng
        {
            public uint X, Y, Z, W;
            public bool Same(Rng o) { return X == o.X && Y == o.Y && Z == o.Z && W == o.W; }
        }

        // ---- rekord biegu cienia: miedzy naszym prefiksem a koncem oryginalu ----
        private static bool _shOpen, _shNewThrew, _shOldSeen;
        private static Clan _shClan;
        private static Hero _shNewH1, _shNewPick, _shOldH1, _shOldPick;
        private static Rng _shNewRng, _shOldRng;
        private static long _shT0;

        private static void Stumble(string where, Exception e)
        {
            _stumbles++; _stumblesDay++;
            if (_stumbles == 1) { try { Scribe.Report("CrashScribe", e, where, null); } catch { } }
        }

        private static int IlLen(MethodBase m)
        {
            try
            {
                var b = m != null ? m.GetMethodBody() : null;
                return b != null ? b.GetILAsByteArray().Length : -1;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Implementacja metody wirtualnej, ktora naprawde wykona sie dla typu t - zawsze jako metoda ZADEKLAROWANA (ReflectedType ==
        /// DeclaringType = klasa, ktora ja nadpisala). Metoda odziedziczona wzieta przez typ pochodny to inny obiekt MethodInfo, a Harmony
        /// trzyma latki pod zadeklarowana: Harmony.GetPatchInfo dalby dla niej null i obca latka bylaby niewidoczna (sprawdzone na
        /// Harmony 2.4.2 z gry). Gdy zadeklarowanej nie da sie znalezc - null (straz nie puszcza).
        /// </summary>
        private static MethodInfo Impl(Type t, string name, Type[] args)
        {
            try
            {
                const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var m = t.GetMethod(name, all, null, args, null);
                if (m == null || m.DeclaringType == null || m.ReflectedType == m.DeclaringType) return m;
                return m.DeclaringType.GetMethod(name, all | BindingFlags.DeclaredOnly, null, args, null);
            }
            catch { return null; }
        }

        private static Rng Snap(MBFastRandom r)
        {
            Rng s; s.X = _rx(r); s.Y = _ry(r); s.Z = _rz(r); s.W = _rw(r);
            return s;
        }

        private static void Restore(MBFastRandom r, Rng s)
        {
            _rx(r) = s.X; _ry(r) = s.Y; _rz(r) = s.Z; _rw(r) = s.W;
        }

        // ------------------------------------------------------------ wpinanie i straze

        /// <summary>Typy, pola i metody cudzego kodu - raz na proces. Zwraca powod braku albo null.</summary>
        private static string Resolve()
        {
            var tBeh = AccessTools.TypeByName("ROT.CampaignBehaviors.ROTRelationshipsBehavior");
            if (tBeh == null) return "ROT nieobecny - brak ROTRelationshipsBehavior";
            _mCheck = AccessTools.DeclaredMethod(tBeh, "CheckNPCMarriages", new[] { typeof(Clan) });
            if (_mCheck == null) return "brak metody ROTRelationshipsBehavior.CheckNPCMarriages(Clan)";
            int il = IlLen(_mCheck);
            if (il != IlCheck) return "inna wersja ROT - CheckNPCMarriages ma " + il + " bajtow IL, znana " + IlCheck;
            _fList = AccessTools.DeclaredField(tBeh, "_unmarriedHeroes");
            if (_fList == null || _fList.FieldType != typeof(List<Hero>)) return "brak pola ROTRelationshipsBehavior._unmarriedHeroes (List<Hero>)";

            il = IlLen(AccessTools.DeclaredMethod(typeof(TaleWorlds.Core.Extensions), "GetRandomElementInefficiently"));
            if (il != IlPickRandom) return "inna wersja gry - Extensions.GetRandomElementInefficiently ma " + il + " bajtow IL, znana " + IlPickRandom;

            var getter = AccessTools.PropertyGetter(typeof(Game), "RandomGenerator");
            if (getter == null || getter.ReturnType != typeof(MBFastRandom)) return "brak Game.RandomGenerator";
            _getRng = (Func<Game, MBFastRandom>)Delegate.CreateDelegate(typeof(Func<Game, MBFastRandom>), getter);
            foreach (var n in new[] { "_x", "_y", "_z", "_w" })
            {
                var f = AccessTools.DeclaredField(typeof(MBFastRandom), n);
                if (f == null || f.FieldType != typeof(uint)) return "brak pola MBFastRandom." + n;
            }
            _rx = AccessTools.FieldRefAccess<MBFastRandom, uint>("_x");
            _ry = AccessTools.FieldRefAccess<MBFastRandom, uint>("_y");
            _rz = AccessTools.FieldRefAccess<MBFastRandom, uint>("_z");
            _rw = AccessTools.FieldRefAccess<MBFastRandom, uint>("_w");

            var fHome = AccessTools.DeclaredField(typeof(Hero), "_homeSettlement");
            if (fHome == null || fHome.FieldType != typeof(Settlement)) return "brak pola Hero._homeSettlement";
            _home = AccessTools.FieldRefAccess<Hero, Settlement>("_homeSettlement");

            var mApply = AccessTools.DeclaredMethod(typeof(MarriageAction), "Apply", new[] { typeof(Hero), typeof(Hero), typeof(bool) });
            if (mApply == null) return "brak MarriageAction.Apply(Hero, Hero, bool)";
            _apply = (Action<Hero, Hero, bool>)Delegate.CreateDelegate(typeof(Action<Hero, Hero, bool>), mApply);
            return null;
        }

        /// <summary>
        /// Model malzenstw tej kampanii: znany lancuch ROT > BK > gra. Zwraca powod, dla ktorego cala latka stoi, albo null;
        /// spouseWhy = powod, dla ktorego nie dziala sam skrot dla zonatych.
        /// </summary>
        private static string CheckModel(out string spouseWhy, out string desc)
        {
            spouseWhy = null; desc = "?";
            _chainCouple = null; _chainChance = null;
            var mm = Campaign.Current != null && Campaign.Current.Models != null ? Campaign.Current.Models.MarriageModel : null;
            if (mm == null) return "brak modelu malzenstw w kampanii";
            var tTop = mm.GetType();
            if (tTop.FullName != "ROT.Models.ROTMarriageModel") return "model malzenstw na wierzchu to " + tTop.FullName + ", znany ROT.Models.ROTMarriageModel";
            var fPrev = AccessTools.DeclaredField(tTop, "_previousModel");
            var prev = fPrev != null ? fPrev.GetValue(mm) as MarriageModel : null;
            if (prev == null) return "brak modelu pod ROTMarriageModel (_previousModel)";
            var tPrev = prev.GetType();
            var tBk = AccessTools.TypeByName("BannerKings.Models.Vanilla.BKMarriageModel");
            if (tBk == null || !tBk.IsAssignableFrom(tPrev)) return "model pod ROT to " + tPrev.FullName + ", znany: pochodna BannerKings BKMarriageModel";
            desc = tTop.Name + " > " + tPrev.Name;

            var chain = new List<MethodBase>();
            var mRotCouple = Impl(tTop, "IsCoupleSuitableForMarriage", H2);
            if (mRotCouple == null || mRotCouple.DeclaringType != tTop || IlLen(mRotCouple) != IlRotCouple)
                return "inna wersja ROT - ROTMarriageModel.IsCoupleSuitableForMarriage ma " + IlLen(mRotCouple) + " bajtow IL, znana " + IlRotCouple;
            var mDefCouple = Impl(tPrev, "IsCoupleSuitableForMarriage", H2);
            if (mDefCouple == null || mDefCouple.DeclaringType != typeof(DefaultMarriageModel))
                return "model " + tPrev.Name + " ma wlasne IsCoupleSuitableForMarriage (" + (mDefCouple != null && mDefCouple.DeclaringType != null ? mDefCouple.DeclaringType.FullName : "?") + ") - para tej samej plci nie jest juz pewnym 'nie'";
            if (IlLen(mDefCouple) != IlDefCouple)
                return "inna wersja gry - DefaultMarriageModel.IsCoupleSuitableForMarriage ma " + IlLen(mDefCouple) + " bajtow IL, znana " + IlDefCouple;
            chain.Add(mRotCouple); chain.Add(mDefCouple);
            // pozostale metody modelu wolane przy ocenie pary i przez Hero.CanMarry - do kontroli obcych latek
            var more = new[]
            {
                new KeyValuePair<string, Type[]>("ShouldNpcMarriageBetweenClansBeAllowed", new[] { typeof(Clan), typeof(Clan) }),
                new KeyValuePair<string, Type[]>("IsClanSuitableForMarriage", new[] { typeof(Clan) }),
                new KeyValuePair<string, Type[]>("IsSuitableForMarriage", new[] { typeof(Hero) }),
            };
            foreach (var kv in more)
            {
                var a = Impl(tTop, kv.Key, kv.Value);
                var b = Impl(tPrev, kv.Key, kv.Value);
                if (a == null || b == null) return "brak metody modelu " + kv.Key;
                chain.Add(a); chain.Add(b);
            }
            var mCan = AccessTools.DeclaredMethod(typeof(Hero), "CanMarry", Type.EmptyTypes);
            if (mCan == null) return "brak Hero.CanMarry()";
            chain.Add(mCan);
            // reszta lancucha oceny pary w grze: zaloty i pamiec domu - skroty stoja na tym, ze to same odczyty plus jedno pole pamieci
            var mCourt = AccessTools.DeclaredMethod(typeof(Romance), "GetCourtedHeroInOtherClan", H2);
            var mHomeGet = AccessTools.DeclaredPropertyGetter(typeof(Hero), "HomeSettlement");
            var mHomeUpd = AccessTools.DeclaredMethod(typeof(Hero), "UpdateHomeSettlement", Type.EmptyTypes);
            if (mCourt == null || mHomeGet == null || mHomeUpd == null)
                return "brak Romance.GetCourtedHeroInOtherClan / Hero.HomeSettlement / Hero.UpdateHomeSettlement";
            chain.Add(mCourt); chain.Add(mHomeGet); chain.Add(mHomeUpd);
            _chainCouple = chain.ToArray();

            // skrot dla zonatych: szansa slubu lorda z zyjacym malzonkiem musi byc zerem bez losowania
            var sc = new List<MethodBase>();
            try
            {
                var mRotChance = Impl(tTop, "NpcCoupleMarriageChance", H2);
                var mBkChance = Impl(tPrev, "NpcCoupleMarriageChance", H2);
                var mDefChance = AccessTools.DeclaredMethod(typeof(DefaultMarriageModel), "NpcCoupleMarriageChance", H2);
                var mAdequate = Impl(tPrev, "IsMarriageAdequate", new[] { typeof(Hero), typeof(Hero), typeof(bool), typeof(bool) });
                var tExt = AccessTools.TypeByName("BannerKings.Utils.Extensions.HeroExtensions");
                var mSpouse = tExt != null ? AccessTools.DeclaredMethod(tExt, "HasPrimarySpouse", new[] { typeof(Hero) }) : null;
                var mNext = AccessTools.DeclaredMethod(typeof(MBFastRandom), "Next", new[] { typeof(int) });
                if (mRotChance == null || mRotChance.DeclaringType != tTop || IlLen(mRotChance) != IlRotChance)
                    spouseWhy = "ROTMarriageModel.NpcCoupleMarriageChance inne niz znane";
                else if (mBkChance == null || mBkChance.DeclaringType != tBk || IlLen(mBkChance) != IlBkChance)
                    spouseWhy = "NpcCoupleMarriageChance modelu BK inne niz znane (" + (mBkChance != null && mBkChance.DeclaringType != null ? mBkChance.DeclaringType.Name : "?") + ", " + IlLen(mBkChance) + " bajtow IL)";
                else if (mDefChance == null)
                    spouseWhy = "brak DefaultMarriageModel.NpcCoupleMarriageChance";
                else if (mAdequate == null || mAdequate.DeclaringType != tBk || IlLen(mAdequate) != IlBkAdequate)
                    spouseWhy = "BKMarriageModel.IsMarriageAdequate inne niz znane (" + IlLen(mAdequate) + " bajtow IL)";
                else if (mSpouse == null || IlLen(mSpouse) != IlBkSpouse)
                    spouseWhy = "BK HasPrimarySpouse inne niz znane (" + IlLen(mSpouse) + " bajtow IL)";
                else if (mNext == null || IlLen(mNext) != IlRngNext)
                    spouseWhy = "MBFastRandom.Next(int) inne niz znane (" + IlLen(mNext) + " bajtow IL)";
                else
                {
                    sc.Add(mRotChance); sc.Add(mBkChance); sc.Add(mDefChance); sc.Add(mAdequate); sc.Add(mSpouse);
                }
            }
            catch (Exception e) { spouseWhy = "wyjatek przy sprawdzaniu lancucha szansy (" + e.GetType().Name + ")"; }
            _chainChance = sc.ToArray();
            return null;
        }

        private static string Who(Patch p)
        {
            try
            {
                var pm = p.PatchMethod;
                return p.owner + (pm != null && pm.DeclaringType != null ? " (" + pm.DeclaringType.Name + "." + pm.Name + ")" : "");
            }
            catch { return "?"; }
        }

        /// <summary>Pierwsza obca latka na metodzie (kazdy rodzaj) albo null; raceLawOk przepuszcza postfiks Armoury RaceLaw.CouplePostfix.</summary>
        private static string Foreign(MethodBase m, bool raceLawOk)
        {
            var info = Harmony.GetPatchInfo(m);
            if (info == null) return null;
            for (int k = 0; k < 4; k++)
            {
                var set = k == 0 ? info.Prefixes : k == 1 ? info.Postfixes : k == 2 ? info.Transpilers : info.Finalizers;
                if (set == null) continue;
                foreach (var p in set)
                {
                    if (p == null || p.owner == OurOwner) continue;
                    var pm = p.PatchMethod;
                    if (raceLawOk && k == 1 && pm != null && pm.DeclaringType != null && pm.DeclaringType.FullName == "Armoury.RaceLaw" && pm.Name == "CouplePostfix") continue;
                    return Who(p) + " na " + (m.DeclaringType != null ? m.DeclaringType.Name + "." : "") + m.Name;
                }
            }
            return null;
        }

        /// <summary>
        /// Obce latki Harmony (inne mody wpinaja sie takze po nas): na samej metodzie ROT grozny jest tylko prefiks z wynikiem bool
        /// i transpiler (zmieniaja bieg oryginalu; postfiksy i prefiksy-obserwatorzy biegna takze przy naszej drodze); na lancuchu
        /// oceny pary kazda obca latka poza RaceLaw zatrzymuje nasza droge; na lancuchu szansy - tylko skrot dla zonatych.
        /// </summary>
        private static void CheckForeign()
        {
            string stand = null, spouse = null;
            try
            {
                var info = Harmony.GetPatchInfo(_mCheck);
                if (info != null)
                {
                    if (info.Prefixes != null)
                        foreach (var p in info.Prefixes)
                            if (p != null && p.owner != OurOwner && p.PatchMethod != null && p.PatchMethod.ReturnType == typeof(bool)) { stand = "prefiks " + Who(p) + " na CheckNPCMarriages"; break; }
                    if (stand == null && info.Transpilers != null)
                        foreach (var p in info.Transpilers)
                            if (p != null && p.owner != OurOwner) { stand = "transpiler " + Who(p) + " na CheckNPCMarriages"; break; }
                }
                if (stand == null && _chainCouple != null)
                    foreach (var m in _chainCouple) { stand = Foreign(m, true); if (stand != null) break; }
                if (_chainChance != null)
                    foreach (var m in _chainChance) { spouse = Foreign(m, false); if (spouse != null) break; }
            }
            catch (Exception e) { Stumble("MarriageFast.CheckForeign", e); stand = "nie udalo sie sprawdzic obcych latek"; }

            if (stand != _standWhy)
            {
                _standDown = stand != null; _standWhy = stand;
                Scribe.Line(Tag + ": " + (stand != null
                    ? "obca latka - " + stand + " - nasza droga stoi, swaty liczy oryginal ROT."
                    : "obcej latki juz nie ma - nasza droga znow czynna."));
            }
            bool sp = _spouseModelOk && spouse == null;
            if (sp != _spouseOk)
            {
                _spouseOk = sp;
                _viableOn = _viableCfg && sp;   // nowa regula pyta model o szanse czesciej niz oryginal - tylko przy znanym lancuchu bez obcych latek
                Scribe.Line(Tag + ": skrot dla zonatych " + (sp ? "znow czynny" : "wylaczony - obca latka - " + spouse)
                            + (_viableCfg ? (sp ? "; regula 'tylko kandydaci z szansa' znow czynna." : "; regula 'tylko kandydaci z szansa' stoi - losowanie jak w oryginale ROT; reszta drogi bez zmian.")
                                          : (sp ? "." : " - reszta drogi bez zmian.")));
            }
        }

        /// <summary>Stan reguly "tylko kandydaci z szansa" slowami - do linii startu i linii doby.</summary>
        private static string ViableState()
        {
            if (_viableOn) return "CZYNNE";
            if (!Config.MarriageOnlyViableCandidates) return "WYLACZONE (klucz = false) - losowanie jak w oryginale ROT";
            if (_shadow) return "NIE DZIALA w biegu cienia (decyduje oryginal ROT, nasza droga liczy stara regula)";
            return "STOI (lancuch szansy inny niz znany albo obca latka - jak skrot dla zonatych) - losowanie jak w oryginale ROT";
        }

        private static string Num(float v) { return v.ToString("0.0##", CultureInfo.InvariantCulture); }

        /// <summary>
        /// Hamulec (MarriageChanceScale): wartosc z ustawien przycieta do zakresu 0.1-1.0 (nie-liczba = domyslne 0.17). Przyciecie = jedna
        /// linia w logu, raz na proces (ustawienia czytane raz, przy starcie gry - tam log nie jest jeszcze otwarty, stad kontrola tutaj).
        /// </summary>
        private static float ScaleFromConfig()
        {
            float raw = Config.MarriageChanceScale;
            float v = float.IsNaN(raw) ? ScaleDefault : raw < ScaleMin ? ScaleMin : raw > ScaleMax ? ScaleMax : raw;
            if (v != raw && !_scaleWarned)
            {
                _scaleWarned = true;
                try
                {
                    Scribe.Line(Tag + ": MarriageChanceScale = " + raw.ToString("0.###", CultureInfo.InvariantCulture) + " poza zakresem "
                                + Num(ScaleMin) + "-" + Num(ScaleMax) + " - przyciete do " + Num(v) + ".");
                }
                catch { }
            }
            return v;
        }

        /// <summary>Hamulec dziala w rzucie wolnego lorda naszej drogi: regulator - zawsze (poza biegiem cienia); staly mnoznik - tylko przy czynnej regule (jak 13.10).</summary>
        private static bool BrakeOn() { return !_shadow && (_regOn || _viableOn); }

        private static string N3(double v) { return v.ToString("0.000", CultureInfo.InvariantCulture); }

        /// <summary>Hamulec slowami - do linii startu i linii doby: wartosc, z ktora biegnie rzut wolnego lorda, albo ze nie dziala.</summary>
        private static string ScaleState()
        {
            if (!BrakeOn())
                return "mnoznik szansy NIE DZIALA - " + (_shadow ? "bieg cienia" : "regula nieczynna") + " (ustawiony " + Num(_scaleCfg) + ")";
            if (!_regOn) return "mnoznik szansy " + Num(_scale) + " (MarriageChanceScale)";
            return "mnoznik szansy " + N3(_scale) + " (regulator MarriageTargetPerYear " + _target + " na rok = " + N3((double)_target / _year) + " na dobe przy roku " + _year
                   + (_avgN > 0 ? ", srednia oczekiwanych slubow przy 1.0 " + N3(_avg) + " na dobe z " + _avgN + " dob)"
                                : " - bez sredniej, start MarriageChanceScale)");
        }

        /// <summary>Regulator: mnoznik z celu i sredniej (bez sredniej - mnoznik startowy).</summary>
        private static float RegScale()
        {
            if (_avgN <= 0 || !(_avg > 0)) return _scaleCfg;
            double v = (double)_target / Math.Max(1, _year) / _avg;
            return v >= 1 ? 1f : v <= RegMin ? RegMin : (float)v;
        }

        /// <summary>Start sesji kampanii (OnSessionLaunched - modele juz sa): straze, latka (raz na proces), linia startu.</summary>
        internal static void OnSession()
        {
            try
            {
                _ready = false; _shadow = false; _spouseOk = false; _spouseModelOk = false; _standDown = false; _standWhy = null; _patchCheckDue = true;
                _viableCfg = false; _viableOn = false; _scale = 1f; _scaleCfg = 1f; _regOn = false; _target = 0;
                _lazyOn = false; LazyReset();   // NW O1
                RegReset();
                bool fast = Config.MarriageFast, shadow = Config.MarriageFastShadow;
                if (!fast && !shadow) { Say(false, "MarriageFast = false"); return; }
                if (!_resolved) { _resolved = true; _resolveFail = Resolve(); }
                if (_resolveFail != null) { Say(false, _resolveFail); return; }
                string spouseWhy, desc;
                string why = CheckModel(out spouseWhy, out desc);
                if (why != null) { Say(false, why); return; }

                if (_h == null) _h = new Harmony(OurOwner);
                if (!_patched)
                {
                    _h.Patch(_mCheck, prefix: new HarmonyMethod(typeof(MarriageFast), nameof(Prefix)));
                    _patched = true;
                }
                if (shadow && !_shadowPatched)
                {
                    var mInternal = AccessTools.DeclaredMethod(typeof(MarriageAction), "ApplyInternal", new[] { typeof(Hero), typeof(Hero), typeof(bool) });
                    if (mInternal == null) { Say(false, "bieg cienia: brak MarriageAction.ApplyInternal(Hero, Hero, bool)"); return; }
                    _h.Patch(mInternal, prefix: new HarmonyMethod(typeof(MarriageFast), nameof(ShadowApplyWatch)));
                    _h.Patch(_mCheck, postfix: new HarmonyMethod(typeof(MarriageFast), nameof(ShadowPostfix)));
                    _shadowPatched = true;
                }
                _shadow = shadow;
                _spouseModelOk = spouseWhy == null;
                _spouseOk = _spouseModelOk;
                _viableCfg = Config.MarriageOnlyViableCandidates && !shadow;
                _viableOn = _viableCfg && _spouseOk;
                _lazyOn = Config.MarriageLazyDraw;   // NW O1: dziala tylko przy czynnej regule (viable w Decide)
                _scaleCfg = ScaleFromConfig();   // hamulec: staly mnoznik (tylko przy _viableOn) albo start regulatora
                int tg = Config.MarriageTargetPerYear;
                _target = tg < 0 ? 0 : tg > TargetMax ? TargetMax : tg;
                if (_target != tg) Scribe.Line(Tag + ": MarriageTargetPerYear = " + tg + " poza zakresem 0-" + TargetMax + " - przyciete do " + _target + ".");
                try { _year = CampaignTime.DaysInYear; } catch { _year = 0; }
                if (_year < 1) _year = 364;
                _regOn = _target > 0 && !shadow;   // regulator: kazdy rzut wolnego lorda naszej drogi (BrakeOn)
                _scale = _scaleCfg;                // do pierwszej sredniej (RegScale w DailyLine)
                _ready = true;
                Say(true, "ROT ROTRelationshipsBehavior.CheckNPCMarriages; model " + desc + "; "
                          + (shadow ? "BIEG CIENIA - decyduje oryginal ROT, nasza droga tylko liczy; linia doby '" + Tag + ": klanow ..., roznic ...' (narzedzie pomiaru, nie prog 0 - w grze BK losuje w ocenie szansy)"
                                    : _viableOn ? "decyduje nasza droga: jedno przejscie po kandydatach przeciwnej plci; wolny lord losuje tylko sposrod kandydatow z szansa > 0 (pary i ciag losowy INNE niz w oryginale - zamierzone)"
                                                : "decyduje nasza droga: ta sama regula i te same szanse co w ROT, jedno przejscie po kandydatach przeciwnej plci (pary w grze inne, niz wylosowalby sam ROT - BK losuje w ocenie szansy)")
                          + "; skrot dla zonatych: " + (spouseWhy == null ? "tak" : "nie - " + spouseWhy)
                          + "; tylko kandydaci z szansa (MarriageOnlyViableCandidates): " + ViableState()
                          + "; " + ScaleState()
                          + "; " + LazyState());
            }
            catch (Exception e)
            {
                _ready = false;
                Stumble("MarriageFast.OnSession", e);
                Say(false, "wyjatek przy wpinaniu " + e.GetType().Name + " - szczegoly w raporcie");
            }
        }

        private static void Say(bool ok, string what)
        {
            try
            {
                Scribe.Line("Mends: swaty ROT (Z.3): " + (ok ? "PODPIETY (" + what + ")" : "BRAK (" + what + ") - zostaje oryginal ROT"
                            + (Config.MarriageOnlyViableCandidates ? "; regula 'tylko kandydaci z szansa' (MarriageOnlyViableCandidates) tez nie dziala - potrzebuje naszej drogi" : "")));
            }
            catch { }
        }

        // ------------------------------------------------------------ nasza droga

        /// <summary>Prefiks ROTRelationshipsBehavior.CheckNPCMarriages(Clan). false = oryginal pominiety (zdecydowala nasza droga).</summary>
        public static bool Prefix(object __instance, Clan __0)
        {
            if (!_ready || _busy) return true;
            MBFastRandom rng; List<Hero> list; MarriageModel model; Rng s0;
            try
            {
                if (_patchCheckDue) { _patchCheckDue = false; CheckForeign(); }
                if (_standDown) { _dayOrig++; return true; }   // regulator: doba z rodem liczonym przez oryginal - bez wpisu do sredniej
                if (_shOpen) ShadowOrphan();
                var game = Game.Current;
                rng = game != null ? _getRng(game) : null;
                list = _fList.GetValue(__instance) as List<Hero>;
                model = Campaign.Current.Models.MarriageModel;
                if (rng == null || list == null || model == null) { _stumbles++; _stumblesDay++; _dayOrig++; return true; }
                s0 = Snap(rng);
            }
            catch (Exception e) { Stumble("MarriageFast.Prefix (wejscie)", e); _dayOrig++; return true; }

            Hero h1 = null, pick = null;
            bool decided = false;
            Exception err = null;
            long t0 = Stopwatch.GetTimestamp();
            try { _busy = true; decided = Decide(model, list, __0, out h1, out pick); }
            catch (Exception e) { err = e; }
            finally
            {
                _busy = false;
                // bufory nie trzymaja bohaterow miedzy wywolaniami (po wyjsciu do menu przypinalyby stara kampanie do nastepnego wczytania)
                _lords.Clear(); _males.Clear(); _females.Clear(); _nullHomes.Clear(); _cand.Clear(); _pool.Clear();
                _court.Clear(); _courtBuilt = false;
            }
            long dt = Stopwatch.GetTimestamp() - t0;

            _dayClans++; _totClans++;
            if (err != null)
            {
                // nic nie zostalo zmienione poza generatorem (i pamieciami dnia BK): cofamy go i oddajemy ten rod oryginalowi - liczy od tego
                // samego stanu generatora, stara regula i bez hamulca
                try { Restore(rng, s0); } catch { }
                Stumble("MarriageFast.Decide", err);
                _dayOrig++;   // regulator: ten rod liczy oryginal - doba bez wpisu do sredniej
                if (_shadow) ShadowOpen(__0, null, null, default(Rng), true);
                return true;
            }
            _dayNewTicks += dt;
            if (decided) _dayNewPairs++;

            if (_shadow)
            {
                // bieg cienia: nasza droga tylko policzyla - generator wraca do wejscia, decyduje oryginal
                Rng sNew;
                try { sNew = Snap(rng); Restore(rng, s0); }
                catch (Exception e) { Stumble("MarriageFast.Prefix (cien)", e); return true; }
                ShadowOpen(__0, h1, pick, sNew, false);
                return true;
            }
            if (decided) _apply(h1, pick, true);   // poza ochrona, jak w oryginale: wyjatek slubu leci dalej tak samo
            return false;
        }

        /// <summary>
        /// Decyzja rodu bez slubu: kto z kim (albo nikt). Bez reguly "tylko kandydaci z szansa" NASZE wywolania MBRandom sa te same co
        /// w oryginale - w tej samej kolejnosci, z tymi samymi argumentami (wyjatek: krok zonatego, gdzie argument nie ma znaczenia dla
        /// stanu). Stan generatora w grze i tak wychodzi inny niz w oryginale: model BK sam losuje w ocenie szansy, a tej dla zonatego
        /// nie wolamy (naglowek klasy) - identycznie jest tylko poza gra. Z regula wolny lord losuje inaczej (PickViable), a prog jego
        /// rzutu mnozy hamulec (_scale; 1.0 = prog bez zmian) - reszta bez zmian.
        /// </summary>
        private static bool Decide(MarriageModel model, List<Hero> list, Clan clan, out Hero h1, out Hero pick)
        {
            h1 = null; pick = null;
            if (clan == Clan.PlayerClan || !model.IsClanSuitableForMarriage(clan)) return false;
            _lords.Clear();
            _lords.AddRange(clan.AliveLords);   // kopia, jak ToList() w oryginale
            bool split = false;
            for (int li = 0; li < _lords.Count; li++)
            {
                Hero hero1 = _lords[li];
                if (!hero1.CanMarry()) continue;
                if (!split) { Split(list); split = true; }
                _dayLords++;
                Hero sp1 = hero1.Spouse;
                bool free = sp1 == null || !sp1.IsAlive;   // wolny = bez zyjacego malzonka (pierwszy warunek BK HasPrimarySpouse; sam odczyt)
                bool viable = _viableOn && free;
                Hero x; float num;
                if (!Scan(model, list, hero1, viable, out x, out num)) continue;
                if (!viable)
                {
                    num = model.NpcCoupleMarriageChance(hero1, x);
                    // licznik bez wplywu na bieg: wolny lord wylosowal kandydata jak w oryginale - czy z szansa
                    if (free) { _dayFree++; if (!(num > 0f)) _dayOldDead++; }
                }
                // hamulec: wolny lord - kandydat wybrany jak dotad (szansa > 0, bez mnoznika), nizszy jest sam prog rzutu; ten sam jeden krok generatora.
                // Staly mnoznik (MarriageTargetPerYear = 0) - tylko przy czynnej regule (jak 13.10); regulator - kazdy rzut wolnego lorda naszej drogi
                // (recenzja 14.10: takze gdy regula stoi), a szansa przy 1.0 idzie do sumy doby (oczekiwane sluby - srednia regulatora). Mnoznik 1.0 = prog bez zmian.
                bool brake = free && (_regOn || viable);   // _regOn i viable sa false w biegu cienia
                float roll = brake && _scale < 1f ? num * _scale : num;
                if (brake && num > 0f) { _dayRolls++; _dayExp += num; _dayExpS += roll; }
                if (!(num > 0f) || !(MBRandom.RandomFloat < roll)) continue;
                bool courting = false;
                List<Romance.RomanticState> states = Romance.RomanticStateList;
                for (int i = 0; i < states.Count; i++)
                {
                    Romance.RomanticState rs = states[i];
                    if ((int)rs.Level >= 3 && (rs.Person1 == hero1 || rs.Person2 == hero1 || rs.Person1 == x || rs.Person2 == x)) { courting = true; break; }
                }
                if (courting) continue;
                h1 = hero1; pick = x;
                return true;
            }
            return false;
        }

        /// <summary>Jedno tanie przejscie po liscie ROT na wywolanie rodu: podzial wedlug plci (kolejnosc listy zachowana) i spis
        /// bohaterow z pusta pamiecia domu (dla nich ocena pary ma skutek uboczny - nie wolno jej pominac).</summary>
        private static void Split(List<Hero> list)
        {
            _males.Clear(); _females.Clear(); _nullHomes.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                Hero h = list[i];
                if (h.IsFemale) _females.Add(h); else _males.Add(h);
                if (_home(h) == null) _nullHomes.Add(h);
            }
            if (_nullHomes.Count > _dayNullMax) _dayNullMax = _nullHomes.Count;
        }

        /// <summary>
        /// Kandydat dla lorda. true = x wylosowany jak w oryginale (to samo wywolanie MBRandom na tej samej liscie - od tego samego stanu
        /// generatora ten sam element); false = dalej bez liczenia szansy: nie ma kandydata (bez losowania, jak w oryginale) albo lord ma
        /// zyjacego malzonka (jeden krok generatora zuzyty, szansa i tak wynosi 0, wiec ROT tez nie robi nic wiecej - ale w grze sama
        /// ocena szansy BK, ktorej tu nie wolamy, potrafi zuzyc krok generatora; naglowek klasy).
        /// viable (tylko wolny lord przy regule "tylko kandydaci z szansa"): lista kandydatow ta sama, ale wybor przez PickViable -
        /// true = x ma szanse num > 0 (juz policzona), false = nikt z listy nie ma szansy.
        /// </summary>
        private static bool Scan(MarriageModel model, List<Hero> list, Hero hero1, bool viable, out Hero x, out float num)
        {
            x = null; num = 0f;
            _cand.Clear();
            if (_home(hero1) == null)
            {
                // pamiec domu lorda pusta: kazda ocena pary moze ja wypelnic - cala lista w kolejnosci, jak Count() w oryginale
                _dayFull++;
                for (int i = 0; i < list.Count; i++)
                {
                    Hero h = list[i];
                    _dayEvals++;
                    if (model.IsCoupleSuitableForMarriage(hero1, h)) _cand.Add(h);
                }
            }
            else
            {
                bool female = hero1.IsFemale;
                List<Hero> other = female ? _males : _females;
                Hero spouse = _spouseOk ? hero1.Spouse : null;
                bool quick = spouse != null && spouse.IsAlive;   // lord z zyjacym malzonkiem: szansa slubu zawsze 0
                bool lazy = viable && !quick && _lazyOn;         // NW O1: wolny lord przy regule - losowanie leniwe (PickViableLazy), bez pelnego przejscia
                bool any = false;
                if (quick)
                {
                    _daySpouse++;
                    for (int i = 0; i < other.Count; i++)
                    {
                        _dayEvals++;
                        if (model.IsCoupleSuitableForMarriage(hero1, other[i])) { any = true; break; }
                    }
                }
                else if (!lazy)
                {
                    for (int i = 0; i < other.Count; i++)
                    {
                        Hero h = other[i];
                        _dayEvals++;
                        if (model.IsCoupleSuitableForMarriage(hero1, h)) _cand.Add(h);
                    }
                }
                // bohaterowie z pusta pamiecia domu, ktorych skroty by nie zapytaly: oryginal ocenia cala liste, a ocena wypelnia te
                // pamiec - robimy to samo (ta sama plec zawsze; przeciwna tylko po skrocie zonatego - przy pelnym przejsciu juz byla).
                // Ponowna ocena tej samej pary niczego nie zmienia (oryginal tez liczy ja do trzech razy).
                // NW O1: przy losowaniu leniwym przeciwna plec z pusta pamiecia domu tez tutaj (jak po skrocie zonatego) - pelnego przejscia nie ma,
                // a pamiec domu ma sie wypelnic tak samo jak w oryginale, zanim padnie pierwsze losowanie
                for (int i = 0; i < _nullHomes.Count; i++)
                {
                    Hero h = _nullHomes[i];
                    if (_home(h) != null) continue;
                    bool same = h.IsFemale == female;
                    if (!same && !quick && !lazy) continue;
                    _dayEvals++;
                    if (!model.IsCoupleSuitableForMarriage(hero1, h)) continue;
                    if (same) throw new InvalidOperationException("model uznal pare tej samej plci za dobra - podzial wedlug plci nie jest rownowazny");
                    any = true;
                }
                if (quick)
                {
                    if (any) MBRandom.RandomInt(1);   // krok, ktory oryginal zuzywa na wybor kandydata (szansa 0 - RandomFloat nie pada; krokow, ktore w grze zuzywa sama ocena szansy BK, nie odtwarzamy)
                    return false;
                }
                if (lazy) return PickViableLazy(model, hero1, other, out x, out num);
            }
            if (_cand.Count == 0) return false;
            if (viable) return PickViable(model, hero1, out x, out num);
            x = _cand[MBRandom.RandomInt(_cand.Count)];
            return true;
        }

        /// <summary>
        /// Regula "tylko kandydaci z szansa" dla wolnego lorda: z listy kandydatow oryginalu (_cand, niepusta) losowanie bez zwracania,
        /// az trafi sie pierwszy, z ktorym slub jest mozliwy - rozklad rowny po takich kandydatach. Slub niemozliwy = kandydat ma zyjacego
        /// malzonka (BK: szansa 0; przy czynnym skrocie dla zonatych pewne bez pytania modelu), jest w zalotach stanu >= 3 (ROT odrzuca
        /// po rzucie) albo model nie daje szansy wiekszej od 0 (ta sama metoda, ktora oryginal pyta po losowaniu). Pierwsze losowanie = losowanie
        /// starej reguly (ta sama lista, RandomInt(liczba)) - trafienie bez szans liczone do linii doby. Lista _cand jest przy tym zuzywana.
        /// </summary>
        private static bool PickViable(MarriageModel model, Hero hero1, out Hero x, out float num)
        {
            x = null; num = 0f;
            if (!_courtBuilt) BuildCourt();
            if (_court.Contains(hero1)) return false;   // sam w zalotach stanu >= 3: ROT i tak odrzucilby kazdy jego slub
            _dayFree++;
            bool first = true;
            while (_cand.Count > 0)
            {
                int i = MBRandom.RandomInt(_cand.Count);
                Hero h = _cand[i];
                Hero sp = h.Spouse;
                float c = 0f;
                if ((sp == null || !sp.IsAlive) && !_court.Contains(h))
                {
                    _dayChanceEvals++;
                    c = model.NpcCoupleMarriageChance(hero1, h);
                }
                if (c > 0f) { x = h; num = c; return true; }
                if (first) { _dayOldDead++; first = false; }
                int last = _cand.Count - 1;
                _cand[i] = _cand[last];
                _cand.RemoveAt(last);
            }
            _dayNoViable++;
            return false;
        }

        /// <summary>
        /// NW O1 (noc 16.10; klucz MarriageLazyDraw): regula "tylko kandydaci z szansa" bez pelnego przejscia po liscie. Pula = wszyscy bohaterowie
        /// przeciwnej plci (ta sama lista, z ktorej pelne przejscie robilo _cand); losowanie bez zwracania, warunek pary tylko dla wylosowanego,
        /// dla pasujacego - ta sama kontrola co PickViable (zyjacy malzonek, zaloty stanu >= 3, szansa modelu > 0). Pierwszy pasujacy z szansa wygrywa.
        /// Rozklad wyniku ten sam: kazdy "pasuje i ma szanse" z rowna szansa (losowanie bez zwracania z puli, w ktorej tylko oni koncza losowanie),
        /// tak jak bez zwracania z _cand. Liczniki znacza to samo: _dayFree - lord mial co najmniej jednego pasujacego (i nie jest sam w zalotach),
        /// _dayOldDead - pierwszy wylosowany PASUJACY nie ma szansy (pierwszy pasujacy z puli ma ten sam rozklad co pierwsze losowanie z _cand -
        /// losowanie starej reguly), _dayNoViable - pasujacy byli, zadnego z szansa, _dayChanceEvals - pytania o szanse. Inny jest tylko ciag
        /// losowy (krok generatora na kazde losowanie z puli, takze niepasujacego) - zamierzone, jak przy samej regule. Warunek pary jest czysta
        /// funkcja stanu (naglowek klasy), pamiec domu przeciwnej plci wypelnia petla _nullHomes w Scan przed pierwszym losowaniem.
        /// R9 (kontrola): co LazySampleEvery-te losowanie leniwe liczy tez pelne przejscie (tylko warunek pary - bez generatora i bez pytan o szanse)
        /// i porownuje: jest pasujacy tu i tam, wynik nalezy do pasujacych pelnego przejscia, przy wyczerpanej puli ta sama liczba pasujacych.
        /// </summary>
        private static bool PickViableLazy(MarriageModel model, Hero hero1, List<Hero> other, out Hero x, out float num)
        {
            x = null; num = 0f;
            if (!_courtBuilt) BuildCourt();
            if (_court.Contains(hero1)) return false;   // sam w zalotach stanu >= 3: jak PickViable (bez licznikow)
            _dayLazy++;
            _pool.Clear();
            _pool.AddRange(other);
            int suitable = 0;
            while (_pool.Count > 0)
            {
                int i = MBRandom.RandomInt(_pool.Count);
                Hero h = _pool[i];
                int last = _pool.Count - 1;
                _pool[i] = _pool[last];
                _pool.RemoveAt(last);
                _dayEvals++;
                if (!model.IsCoupleSuitableForMarriage(hero1, h)) continue;
                suitable++;
                if (suitable == 1) _dayFree++;
                Hero sp = h.Spouse;
                float c = 0f;
                if ((sp == null || !sp.IsAlive) && !_court.Contains(h))
                {
                    _dayChanceEvals++;
                    c = model.NpcCoupleMarriageChance(hero1, h);
                }
                if (c > 0f) { x = h; num = c; break; }
                if (suitable == 1) _dayOldDead++;
            }
            if (x == null && suitable > 0) _dayNoViable++;
            _pool.Clear();
            if (++_lazySeq % LazySampleEvery == 0)
            {
                try { LazyCheck(model, hero1, other, x, suitable); }
                catch (Exception e) { Stumble("MarriageFast.LazyCheck", e); }
            }
            return x != null;
        }

        /// <summary>NW O1 R9: pelne przejscie (sam warunek pary, jak stara droga) wobec wyniku losowania leniwego - rozjazd = blad przeniesienia reguly.</summary>
        private static void LazyCheck(MarriageModel model, Hero hero1, List<Hero> other, Hero x, int suitableSeen)
        {
            _dayLazySamples++;
            int all = 0; bool inSet = x == null;
            for (int i = 0; i < other.Count; i++)
            {
                Hero h = other[i];
                _dayLazySampleEvals++;
                if (!model.IsCoupleSuitableForMarriage(hero1, h)) continue;
                all++;
                if (h == x) inSet = true;
            }
            bool bad = (all > 0) != (suitableSeen > 0) || !inSet || (x == null && all != suitableSeen) || suitableSeen > all;
            if (!bad) return;
            _dayLazyDiff++; _totLazyDiff++;
            if (_lazyDiffLines >= 3) return;
            _lazyDiffLines++;
            Scribe.Line(Tag + ": losowanie leniwe - ROZJAZD z pelnym przejsciem: lord " + Name(hero1) + ", pasujacych w pelnym przejsciu " + all
                        + ", w losowaniu " + suitableSeen + ", wynik " + (x != null ? Name(x) + (inSet ? "" : " SPOZA pasujacych") : "brak")
                        + " (linia " + _lazyDiffLines + " z 3 w sesji).");
        }

        /// <summary>Bohaterowie w zalotach stanu >= 3 (MatchMadeByFamily i wyzej, takze Marriage) - ten sam warunek co kontrola ROT po rzucie,
        /// zebrany raz na wywolanie rodu (lista gry: sluby z tej kampanii i zaloty gracza).</summary>
        private static void BuildCourt()
        {
            _court.Clear();
            List<Romance.RomanticState> states = Romance.RomanticStateList;
            if (states != null)
                for (int i = 0; i < states.Count; i++)
                {
                    Romance.RomanticState rs = states[i];
                    if (rs == null || (int)rs.Level < 3) continue;
                    if (rs.Person1 != null) _court.Add(rs.Person1);
                    if (rs.Person2 != null) _court.Add(rs.Person2);
                }
            _courtBuilt = true;
        }

        // ------------------------------------------------------------ bieg cienia

        private static void ShadowOpen(Clan clan, Hero h1, Hero pick, Rng sNew, bool threw)
        {
            _shOpen = true; _shClan = clan; _shNewH1 = h1; _shNewPick = pick; _shNewRng = sNew; _shNewThrew = threw;
            _shOldSeen = false; _shOldH1 = null; _shOldPick = null;
            _shT0 = Stopwatch.GetTimestamp();
        }

        /// <summary>Prefiks-obserwator MarriageAction.ApplyInternal (tylko bieg cienia): decyzja oryginalu i stan generatora w tej chwili.</summary>
        public static void ShadowApplyWatch(Hero __0, Hero __1)
        {
            if (!_shOpen || _shOldSeen) return;
            try
            {
                _dayOldTicks += Stopwatch.GetTimestamp() - _shT0;
                _shOldSeen = true; _shOldH1 = __0; _shOldPick = __1;
                var game = Game.Current;
                var rng = game != null ? _getRng(game) : null;
                _shOldRng = rng != null ? Snap(rng) : default(Rng);
            }
            catch (Exception e) { Stumble("MarriageFast.ShadowApplyWatch", e); }
        }

        /// <summary>Postfiks CheckNPCMarriages (tylko bieg cienia): porownanie obu drog dla tego rodu.</summary>
        public static void ShadowPostfix()
        {
            if (!_shOpen) return;
            try
            {
                bool same;
                if (_shOldSeen)
                {
                    _dayOldPairs++;
                    same = !_shNewThrew && _shNewH1 == _shOldH1 && _shNewPick == _shOldPick && _shNewRng.Same(_shOldRng);
                }
                else
                {
                    _dayOldTicks += Stopwatch.GetTimestamp() - _shT0;
                    var game = Game.Current;
                    var rng = game != null ? _getRng(game) : null;
                    same = !_shNewThrew && _shNewH1 == null && rng != null && _shNewRng.Same(Snap(rng));
                }
                if (!same) ShadowDiff(_shOldSeen ? Pair(_shOldH1, _shOldPick) : "bez slubu");
            }
            catch (Exception e) { Stumble("MarriageFast.ShadowPostfix", e); }
            ShadowClose();
        }

        /// <summary>Oryginal nie doszedl do konca (wyjatek) - rekord zamykany przy nastepnym wywolaniu albo linii doby.</summary>
        private static void ShadowOrphan()
        {
            try
            {
                if (_shNewThrew) _dayBothThrew++;
                else ShadowDiff("wyjatek oryginalu");
            }
            catch { }
            ShadowClose();
        }

        private static void ShadowClose()
        {
            _shOpen = false; _shClan = null; _shNewH1 = null; _shNewPick = null; _shOldH1 = null; _shOldPick = null;
        }

        private static void ShadowDiff(string oldWhat)
        {
            _dayDiff++; _totDiff++;
            if (_diffLines >= 5) return;
            _diffLines++;
            Scribe.Line(Tag + ": ROZNICA w rodzie " + Name(_shClan) + " - stara droga: " + oldWhat + ", nowa: "
                        + (_shNewThrew ? "wyjatek" : _shNewH1 != null ? Pair(_shNewH1, _shNewPick) : "bez slubu")
                        + "; stan losowy w chwili decyzji " + (_shOldSeen && !_shNewThrew && _shNewRng.Same(_shOldRng) ? "zgodny" : "niezgodny albo nieporownany")
                        + " (linia " + _diffLines + " z 5 w sesji).");
        }

        private static string Pair(Hero a, Hero b) { return "slub " + Name(a) + " + " + Name(b); }

        private static string Name(Hero h)
        {
            try { return h == null ? "?" : (h.Name != null ? h.Name.ToString() : h.StringId); } catch { return "?"; }
        }

        private static string Name(Clan c)
        {
            try { return c == null ? "?" : (c.Name != null ? c.Name.ToString() : c.StringId); } catch { return "?"; }
        }

        // ------------------------------------------------------------ linia doby

        private static int Day()
        {
            try { return (int)CampaignTime.Now.ToDays; } catch { return 0; }
        }

        /// <summary>Raz na dobe (DailyTick): linia z licznikami od poprzedniej, obce latki do ponownego sprawdzenia.</summary>
        internal static void DailyLine()
        {
            try
            {
                _patchCheckDue = true;
                if (_shOpen) ShadowOrphan();
                // regulator: srednia sumy szans przy 1.0 (pierwsza linia po wczytaniu obejmuje tylko czesc rodow - rody tykaja rozlozone na dobe; doba z rodem
                // liczonym przez oryginal ROT - bez wpisu) i mnoznik nastepnej doby
                float next = _scale;
                bool fed = false;
                if (_ready && _regOn)
                {
                    _regLines++;
                    if (_regLines >= 2 && _dayOrig == 0 && _dayClans > 0)
                    {
                        int n = _avgN < RegWindow ? _avgN + 1 : RegWindow;
                        _avg += (_dayExp - _avg) / n;
                        if (_avgN < RegWindow) _avgN++;
                        fed = true;
                    }
                    next = RegScale();
                }
                if (_ready && (_dayClans > 0 || _stumblesDay > 0))
                {
                    var ci = CultureInfo.InvariantCulture;
                    double ms = 1000.0 / Stopwatch.Frequency;
                    string work = "lordow szukajacych " + _dayLords + " (z zyjacym malzonkiem - skrot " + _daySpouse + ", cala lista " + _dayFull
                                  + "), ocen pary " + _dayEvals + ", bohaterow bez pamieci domu najwyzej " + _dayNullMax
                                  + (_stumblesDay > 0 ? ", POTKNIECIA " + _stumblesDay + " (razem " + _stumbles + ", pierwsze w raporcie; rod z potknieciem liczy oryginal ROT od tego samego stanu losowego)" : "");
                    // koniec linii: regula "tylko kandydaci z szansa" - proby wolnych lordow (bez zyjacego malzonka, z kandydatem) i sluby
                    int wed = _shadow ? _dayOldPairs : _dayNewPairs;   // w biegu cienia decyduje oryginal ROT
                    _totPairs += wed;
                    string pct = _dayFree > 0 ? " (" + (100.0 * _dayOldDead / _dayFree).ToString("0", ci) + "%)" : "";
                    string tail = "; tylko kandydaci z szansa (MarriageOnlyViableCandidates) " + ViableState() + ": proby wolnych lordow " + _dayFree
                                  + (_viableOn ? ", w tym stara regula trafilaby w kandydata bez szans " + _dayOldDead + pct + ", bez zadnego kandydata z szansa " + _dayNoViable
                                                 + ", pytan o szanse " + _dayChanceEvals
                                               : ", w tym trafienie w kandydata bez szans " + _dayOldDead + pct)
                                  + "; " + ScaleState()   // hamulec: z jaka wartoscia biegla ta doba (albo ze nie dzialal)
                                  + (BrakeOn() ? ", oczekiwane slubow dzis przy 1.0 " + N3(_dayExp) + ", przy mnozniku " + N3(_dayExpS) + " (rzutow wolnych lordow z hamulcem " + _dayRolls + ")"
                                                 + (_regOn ? ", jutro mnoznik " + N3(next) + (fed ? "" : _dayOrig > 0 ? " (doba bez wpisu do sredniej - rody liczone przez oryginal ROT " + _dayOrig + ")"
                                                                                                         : " (doba bez wpisu do sredniej - pierwsza po wczytaniu)") : "")
                                               : "")
                                  + "; sluby dzis " + wed + ", od wczytania " + _totPairs
                                  + "; " + LazyState() + ": losowan " + _dayLazy + ", probek starej drogi " + _dayLazySamples + " (ocen pary w probkach " + _dayLazySampleEvals
                                  + "), rozjazdow " + _dayLazyDiff + " (od wczytania " + _totLazyDiff + ")";   // NW O1
                    if (_shadow)
                        Scribe.Line(Tag + ": klanow " + _dayClans + ", par stara droga " + _dayOldPairs + ", nowa " + _dayNewPairs + ", roznic " + _dayDiff
                                    + ", czas stara " + (_dayOldTicks * ms).ToString("0.0", ci) + " ms, nowa " + (_dayNewTicks * ms).ToString("0.0", ci) + " ms"
                                    + "; dzien " + Day() + ", " + work + (_dayBothThrew > 0 ? ", wyjatek w obu drogach " + _dayBothThrew : "")
                                    + "; od wczytania: klanow " + _totClans + ", roznic " + _totDiff + tail + ".");
                    else
                        Scribe.Line(Tag + ": dzien " + Day() + " - klanow " + _dayClans + ", slubow " + _dayNewPairs + ", czas " + (_dayNewTicks * ms).ToString("0.0", ci)
                                    + " ms; " + work + tail + ".");
                }
                _scale = next;   // regulator: mnoznik nastepnej doby (staly mnoznik - bez zmian)
            }
            catch (Exception e) { Stumble("MarriageFast.DailyLine", e); }
            _dayClans = _dayNewPairs = _dayOldPairs = _dayDiff = _dayBothThrew = 0;
            _dayLords = _daySpouse = _dayFull = _dayNullMax = 0;
            _dayEvals = _dayNewTicks = _dayOldTicks = 0;
            _dayFree = _dayOldDead = _dayNoViable = _dayChanceEvals = 0;
            _dayExp = _dayExpS = 0; _dayRolls = _dayOrig = 0;
            LazyReset();   // NW O1
            _stumblesDay = 0;
        }

        /// <summary>NW O1: liczniki doby losowania leniwego od zera.</summary>
        private static void LazyReset() { _dayLazy = _dayLazySamples = _dayLazyDiff = 0; _dayLazySampleEvals = 0; }

        /// <summary>NW O1: stan losowania leniwego slowami - do linii startu i linii doby.</summary>
        private static string LazyState()
        {
            if (!Config.MarriageLazyDraw) return "losowanie leniwe (MarriageLazyDraw) [wyl] - pelna lista jak dotad";
            if (!_viableOn) return "losowanie leniwe (MarriageLazyDraw) NIE DZIALA - regula 'tylko kandydaci z szansa' nieczynna";
            return "losowanie leniwe (MarriageLazyDraw) [WL]";
        }

        /// <summary>Regulator: srednia i liczniki od zera (nowa sesja - pamiec sesji, bez zapisu).</summary>
        private static void RegReset()
        {
            _dayExp = _dayExpS = 0; _dayRolls = _dayOrig = 0;
            _avg = 0; _avgN = 0; _regLines = 0;
        }

        /// <summary>Nowa gra / wczytanie: prefiks stoi do OnSessionLaunched, liczniki od zera (latka Harmony zostaje - raz na proces).</summary>
        internal static void ResetSession()
        {
            _ready = false; _shadow = false; _spouseOk = false; _spouseModelOk = false; _standDown = false; _standWhy = null; _patchCheckDue = true;
            _viableCfg = false; _viableOn = false; _scale = 1f; _scaleCfg = 1f; _regOn = false; _target = 0;
            _lazyOn = false; LazyReset(); _totLazyDiff = 0; _lazyDiffLines = 0; _lazySeq = 0;   // NW O1
            RegReset();
            _busy = false;
            ShadowClose();
            _lords.Clear(); _males.Clear(); _females.Clear(); _nullHomes.Clear(); _cand.Clear();
            _court.Clear(); _courtBuilt = false;
            _dayClans = _dayNewPairs = _dayOldPairs = _dayDiff = _dayBothThrew = 0;
            _dayLords = _daySpouse = _dayFull = _dayNullMax = 0;
            _dayEvals = _dayNewTicks = _dayOldTicks = 0;
            _dayFree = _dayOldDead = _dayNoViable = _dayChanceEvals = 0;
            _totClans = _totDiff = _diffLines = _totPairs = 0;
            _stumblesDay = 0;
        }
    }

    /// <summary>Start sesji (straze i latka) oraz linia doby "Swaty ROT (Z.3)" - bez zapisu w grze.</summary>
    internal sealed class MarriageFastBehavior : CampaignBehaviorBase
    {
        public MarriageFastBehavior() { MarriageFast.ResetSession(); }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, delegate (CampaignGameStarter s) { MarriageFast.OnSession(); });
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, MarriageFast.DailyLine);
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
