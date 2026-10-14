using System;
using System.IO;
using System.Xml;

namespace CrashScribe
{
    internal static class Config
    {
        // --- co lapiemy ---
        public static bool CatchUnhandled = true;      // bledy, ktore i tak wywala gre
        public static bool CatchFirstChance = true;    // bledy zlapane gdzies dalej - czesto prawdziwa przyczyna
        public static bool OnlyModdedFirstChance = true; // ...ale tylko te, w ktorych widac jakiegos moda
        public static bool WrapModMethods = true;      // siec Harmony na metodach naszych modow
        public static bool SwallowModErrors = true;    // zlapany blad w modzie: zapisz i jedz dalej zamiast CTD
        public static bool WrapCampaignHotspots = true;// dodatkowa siec na newralgicznych miejscach gry
        public static bool ShowInGame = true;          // krotka czerwona notka na ekranie, ze cos poszlo nie tak
        public static int MaxReportsPerSession = 300;
        public static bool Stage8ProfileLog = true;    // E8/0 (242): linia "Profil CS (E8/0)" raz na dobe - bledy pierwszej szansy i czas Blame.Culprits (tylko pomiar)
        public static int KeepSessions = 15;
        public static bool RepeatKeyByMessage = true;  // SCRIBE-KLUCZ-POWTOREK (11.10): powtorka = ten sam komunikat (cyfry/hex na '#') i, przy FirstChance, ten sam wolajacy z zywego stosu; linia powtorki z opisem; false = po staremu (typ + pierwsza ramka sladu)

        // mody objete siecia; puste = wszystkie spoza Native
        public static string WrapModules = "RealisticCaptivity,GrandTourney,Armoury";

        // --- werbunek ---
        public static bool LocalRecruits = true;   // ochotnicy wedle kultury OSADY, nie notabla (chlopi z Polnocy to Polnocnicy)

        // --- rozrusznik fabuly ROT (os serialu + tempo nadrabiania) ---
        public static bool FabulaPacerEnabled = true;  // fabula ROT idzie po kolei, wedle osi serialu
        public static int FabulaPaceDays = 4;          // najwyzej jedno wydarzenie fabularne na tyle dni
        public static float FabulaTimeScale = 4.33f;   // rozciagniecie calej osi fabuly: 1.0 = daty ROT bez zmian (rok 84 dni); 4.33 = rok 364 dni Armoury (Jeff 04.10: "rozciagniete") - lata zgadzaja sie z serialem

        // --- Zew Nocnego Krola (patrz NightKingCall.cs) ---
        public static bool NightKingCallEnabled = true;   // trupy z mniejszych band Innych ida do najsilniejszej, az ta dobije do progu oblezenia ROT
        public static int NightKingCallTarget = 520;      // tyle ZDROWYCH trupow w jednej bandzie (ROT oblega od 500; zapas na rannych)
        public static int NightKingCallKeep = 40;         // tyle zdrowych zostaje kazdemu dawcy
        public static int NightKingCallKeepNk = 120;      // tyle zostaje Nocnemu Krolowi, gdy oddaje
        public static float NightKingCallFullness = 0.85f;

        // --- Pochod Nocnego Krola (patrz NightKingCall.March) ---
        public static bool NightKingMarchEnabled = true;          // gotowa horda dostaje CEL: osada za Murem, ktorej garnizon zgniecie
        public static int NightKingMarchMin = 500;                // tylu ZDROWYCH musi miec horda, zanim damy jej cel (prog oblezenia ROT)
        public static float NightKingMarchOdds = 2.0f;            // ilu naszych na jednego obronce (garnizon + milicja); prog samego ROT to 2.5 i nie przepuszcza nic
        public static bool NightKingMarchRespectCooldown = true;  // szanujemy 5-dniowa przerwe ROT po kazdym oblezeniu
        public static bool NightKingMarchVerbose = true;          // raz na dzien wypisz tabelke osad za Murem z garnizonami i stanem wojny

        // --- Kalendarz Innych (T2 noc 08/09.10; patrz NightKingCall.OpenDay) ---
        public static bool NightKingCalendarEnabled = true;  // wylacznik calosci: Pochod i Zew czekaja na dzien celu
        public static int NightKingSiegeFromDay = 728;       // Piesc, Craster i minimum dla kazdego celu (poczatek 3. roku)
        public static bool NightKingRespectShackles = true;  // kajdany ROT 300/400/500 x FabulaTimeScale takze dla Pochodu
        public static int NightKingWallFromDay = 2184;       // Mur od 7. roku (serial s7 ok. 304 AC)
        public static int NightKingCallFromDay = 728;        // Zew spi do pierwszego terminu
        public static bool NightKingCalendarSiegeGate = true; // T2c (patrz NightKingGate.cs): Inni nie zaczynaja ani nie prowadza oblezenia / szturmu / rabunku zamknietego celu

        // --- Inni bez dosypki z niczego (T2b noc 08/09.10; patrz OthersGrowth.cs) ---
        public static bool OthersNoFreeGrowth = true;        // wylacznik calosci: Inni rosna tylko z nekromancji i odbitych jencow (+ szablon nowej bandy)
        public static bool OthersNoBirthDowry = true;        // bez +100 trupow ROT przy narodzinach bandy (zostaje szablon 53 + wodz)
        public static int OthersStartDowryDays = 2;          // bilans otwarcia: +100 przepuszczone w pierwszych N dobach kampanii (Jeff 07.10: 616 na start; 0 = bez wyjatku)
        public static bool OthersNoDailyWights = true;       // bez +2 trupow ROT dziennie na bande
        public static bool OthersNoMapVolunteers = true;     // bez "ochotnikow z mapy" gry (3-7 BasicTroop poza osada)

        // --- Reparacje w wojnach fabularnych ROT (T9 noc 08/09.10; patrz StorylineReparations.cs) ---
        public static bool StorylineWarNoReparations = true;  // pokoj w wojnie, ktora ROT i tak wypowiada od nowa nastepnej doby - bez reparacji Diplomacy (inne pokoje bez zmian)

        // --- Pokoj z biedy (E1 noc 09/10.10; patrz PovertyPeace.cs) ---
        public static bool PovertyPeace = true;  // krolestwo z pustym skarbcem: rzadziej wypowiada wojne (ocena BK), biedne rody chetniej glosuja za pokojem; wojny fabularne ROT bez zmian
        public static bool PovertyPeaceProposals = true;        // E1b: krolestwo z bieda skarbca >= 0.5 raz na 7 dob sklada wniosek o pokoj z wrogiem o najgorszym wyniku wojny (wojny niefabularne); false = tylko wnioski gry i BK
        public static bool PovertyPeaceArrearsMeasure = false;  // E1b: bieda z niedoplaty zwrotu zoldu korony (28 dob, wzgledem 25% i sredniej swiata) - wlaczyc po paczce 165; false = zapas skarbca (E1)

        // --- Oboz obleniczy (patrz Mends.RbSiegeCampFed) ---
        public static bool RbSiegeAttritionOnlyStarving = true; // straty obozu z RealisticBannerlord (2-5 dziennie, rosnace) tylko gdy oboz GLODUJE; garnizon - tylko gdy miasto bez zapasow // najsilniejsza banda pelna w tylu procentach limitu (ROT: oblezenie przy >= 0.8)

        // --- Oblezenie Harrenhal: przepustka BK (B6; patrz Mends.HarrenhalBkPass) ---
        public static bool HarrenhalBkArmyPass = true; // BK nie odmawia armii dowodcy jednego skryptowego CreateArmy oblezenia Harrenhal (Roose albo zastepca); false = po staremu (NRE polykany, zastepca)
        // --- Oblezenie Harrenhal: czekanie na Roose'a i sprzatanie po wydarzeniu (B6b; patrz Mends.HarrenhalWait, HarrenhalEnd, HarrenhalOldSaveCheck) ---
        public static bool HarrenhalWaitForRoose = true;     // Roose w bitwie albo w cudzym oblezeniu: proba odlozona (co godzine), zastepca dopiero po HarrenhalWaitHours; false = zastepca od razu (po staremu)
        public static int HarrenhalWaitHours = 72;           // limit czekania w godzinach gry (doba = 24 h niezaleznie od kalendarza Armoury); <= 0 = nie czekamy
        public static bool HarrenhalReleaseCommander = true; // koniec wydarzenia (postfiks CompleteEvent) i zmiana dowodcy (nasz port albo powrot sterow do ROT z Roose'em): armia oblezenia rozwiazana, dowodca odmrozony; false = jak ROT
        public static bool HarrenhalOldSaveFix = true;       // po wczytaniu: wydarzenie skonczone, a jego dowodca dalej zamrozony - odmrozenie (inni zamrozeni lordowie Polnocy tylko w logu)
        // --- Oblezenie Muru: sprzatanie dowodcy po wydarzeniu (B6c; patrz Mends.WallEnd, WallReleasePrev, WallOldSaveCheck) ---
        public static bool WallReleaseCommander = true;      // koniec wydarzenia (postfiks WallSiegeEvent.CompleteEvent) i zmiana dowodcy (nasz port albo powrot sterow do ROT z Mance'em): armia oblezenia rozwiazana, dowodca odmrozony; false = jak ROT
        public static bool WallOldSaveFix = true;            // po wczytaniu: oblezenie Muru skonczone, a jego dowodca dalej zamrozony - odmrozenie (inni zamrozeni lordowie Wolnych Ludzi tylko w logu)
        // --- Atuty BK z nagrody za bitwe (decyzja Jeffa 4, 12.10; patrz Mends.BkRewardPerks) ---
        public static bool BkBattleRewardPerks = true;       // latka BK BKBattleRewardTweakPatches nie wchodzi w 1.4.8 (nazwy parametrow) - nasze postfiksy z ta sama logika (CommanderWarband +25% wplywu, MercenaryFamousSellswords +20% i styl Cataphract +12% slawy), kazdy tylko gdy BK go nie ma; false = bez tych atutow

        // --- S9 (projekt oblezen 14.10, rozdz. 2; recenzja 14.10): BK bez nowych krolestw z rodow odchodzacych z lennem (patrz Mends.BkRebelKingdoms) ---
        public static bool BkRebelKingdoms = false;          // false (S9): secesja grupy radykalnej BK (RadicalGroup.TriggerRevolt / RebellionActions.CreateRebelKingdom - droga wszystkich panstw "County / Barony / Kingdom of" w fin30) - rody wychodza z lennem jako rody bez krolestwa (droga gry LeaveWithRebellion; po ustapieniu wladcy pokoj ze starym); bunt chlopski - BKClanBehavior._disableRebelKingdom = true (rod buntownikow po 30 dobach zostaje bez krolestwa); pliki BK nietkniete; true = jak dotad (BK robi krolestwa, latki secesji nie wpiete)

        // --- E8/M (247): edukacja BK gracza (patrz Quiet.KnownBkEducation, Mends.BkEducationCheck) ---
        public static bool QuietBkEducation = true;          // znany wyjatek BK CorrectPlayerEducation (finalizer BKROT go polyka): jedna linia zamiast bloku ERROR + raz na gre linia jezyka gracza w BK; false = blok ERROR jak dotad, bez linii

        // --- Z.3 (13.10): swaty lordow ROT szybciej (patrz MarriageFast.cs) ---
        // Pomiar w grze 13.10 (CHANGELOG "Z.3 SWATY ROT - pomiar w grze"): poza gra (sam ROT + BK na danych testu) nasza droga daje te same pary
        // i ten sam ciag losowy co oryginal; W GRZE NIE - model BK sam zuzywa generator gry w ocenie szansy (NpcCoupleMarriageChance), a my
        // pytamy o szanse rzadziej, wiec pary danego dnia sa inne, niz wylosowalby sam ROT (jak w dwoch rozgrywkach). Regula i szanse te same.
        public static bool MarriageFast = true;              // ROT CheckNPCMarriages liczone nasza droga: ta sama regula i te same szanse, ta sama doba - jedno przejscie po kandydatach przeciwnej plci, lord z zyjacym malzonkiem tylko "czy jest ktokolwiek"; pary w grze inne niz w samym ROT (wyzej); false = oryginal ROT bez latki
        public static bool MarriageFastShadow = false;       // bieg cienia: decyduje oryginal ROT, nasza droga tylko liczy od tego samego stanu generatora; linia doby "Swaty ROT (Z.3): klanow N, par stara droga A, nowa B, roznic R, czas stara X ms, nowa Y ms" - NARZEDZIE POMIARU, nie prog "0": w grze roznic stanu losowego jest ok. 85 rodow z 330 na dobe (BK losuje w ocenie szansy); wolniej niz kazda z drog osobno - tylko do testu
        public static bool MarriageOnlyViableCandidates = true;   // ZMIANA ROZGRYWKI (Jeff 13.10): wolny lord losuje tylko sposrod kandydatow, z ktorymi slub jest mozliwy (bez zyjacego malzonka, poza zalotami stanu >= 3, szansa modelu > 0) - dotad ponad polowa dziennych prob trafiala w kandydata bez szans; potrzebuje MarriageFast, nie dziala w biegu cienia; false = losowanie ROT jak dotad (ta sama regula co oryginal; poza gra takze te same pary, w grze nie - wyzej)
        public static float MarriageChanceScale = 0.17f;     // HAMULEC (Jeff 13.10: "Przyhamuj, aby sluby zawieraly sie rzadziej"; Jeff 14.10: "sluby - 48 slubow" na rok, zasada "1 dzien = 1 dzien"): mnoznik szansy slubu w rzucie wolnego lorda; pomiar: 113 slubow AI-AI na rok przed regula, 215 z regula bez hamulca, przy 0.7 291 w 728 dob (bieg dwuletni 13/14.10: 159 + 132). Od recenzji 14.10: przy MarriageTargetPerYear > 0 (domyslnie) to tylko mnoznik STARTOWY (pierwsze 1-2 doby po wczytaniu, zanim regulator ma srednia; 0.17 = model biegu dwuletniego dla roku 1-2); przy MarriageTargetPerYear = 0 - staly hamulec jak 13.10, TYLKO przy czynnej regule MarriageOnlyViableCandidates; zakres 0.1-1.0 (poza nim przyciecie z linia w logu - MarriageFast.OnSession); w biegu cienia nie dziala
        public static int MarriageTargetPerYear = 48;        // recenzja 14.10 (decyzja Jeffa "sluby - 48 slubow" na rok): REGULATOR - mnoznik doby = cel / CampaignTime.DaysInYear / srednia (28 dob) oczekiwanej liczby slubow przy mnozniku 1.0 (suma szans rzutow wolnych lordow naszej drogi), najwyzej 1.0, najmniej 0.02; staly mnoznik trzymal 48 tylko w roku 1-2 (pula wolnych lordow rosnie ok. 200 na rok - linia Z.3); dziala na kazdy rzut wolnego lorda naszej drogi (takze gdy regula MarriageOnlyViableCandidates stoi); 0 = staly MarriageChanceScale jak 13.10 (off); klucz NOWY - stary plik ustawien go nie ma, wiec dziala domyslna 48
        public static bool MarriageOffersPerYear = true;     // 14.10 (ta sama decyzja): oferty slubu dla rodu gracza (gra, MarriageOfferCampaignBehavior) na rok jak w roku gry 84 dni - caly proces rozciagniety x DaysInYear / 84: doba rodu AI przechodzi do rzutow gry z prawdopodobienstwem 84 / CampaignTime.DaysInYear (rok 364 = 0.231) i przerwa miedzy ofertami 7 x DaysInYear / 84 dob (30.3; gra 7 - recenzja 14.10); patrz MarriageOfferYear.cs; false = oferty jak w grze

        // --- P1 audytu modow 14.10: tytuly BannerKings na mapie ROT (patrz BkTitles.cs) ---
        public static bool BkTitlesUseRotFile = true;   // AM-26: sciezka pliku tytulow ROT ustawiana przed KAZDYM odczytem BK (InitializeTitles nowej gry i RetrofitOrphanedTitles kazdego wczytania) - dotad BKROTPatch ustawial ja tylko dla nowej gry, a wczytanie w nowym procesie bralo kalradyjski titles.xml BK (30 ksiestw Calradii w kampanii); false = jak dotad (b47fecc3)
        public static bool BkTitlesRot818 = true;       // AM-27 (tylko z BkTitlesUseRotFile): nasz plik tytulow dla mapy ROT 8.1.8 (zasob DLL, zapisywany do Documents\...\CrashScribe\bk_titles_rot818.xml) zamiast titles_6_3.xml BKROT z ROT 6.x - kazde z 97 miast i 130 zamkow z tytulem (dotad 25 bez), Lorath / Ibben / Qarth z tytulem krolewskim; false = titles_6_3 BKROT

        // --- P3 audytu modow 14.10 (RAPORT-AUDYT-MODOW.md rozdz. 3 i 5; kazdy klucz false = zachowanie b47fecc3) ---
        public static bool LocalLeviesPreachersOnly = true;   // AM-20 (tylko z LocalRecruits): krok 1 LocalLevies (kultura osady dla notabla) tylko dla kaplanow i bez kaplana z kultura glowy rodu pana - rzemieslnicy i kupcy podbitego miasta raz przyjmuja kulture pana jak w BK (dotad CrashScribe cofal to nazajutrz: 675 Artisan + 24 Merchant w 2 lata, pan placil wplywem za nic); obcy ochotnicy do kosza dalej u wszystkich; false = kazdy notabl dostaje kulture osady (b47fecc3)

        internal static void Load(string moduleDataDir)
        {
            try
            {
                var path = Path.Combine(moduleDataDir, "CrashScribe.settings.xml");
                if (!File.Exists(path)) return;
                var doc = new XmlDocument();
                doc.Load(path);
                var root = doc.DocumentElement;
                if (root == null) return;
                foreach (XmlNode n in root.ChildNodes)
                {
                    if (n.NodeType != XmlNodeType.Element) continue;
                    var v = n.InnerText.Trim();
                    switch (n.Name)
                    {
                        case "CatchUnhandled": CatchUnhandled = B(v); break;
                        case "CatchFirstChance": CatchFirstChance = B(v); break;
                        case "OnlyModdedFirstChance": OnlyModdedFirstChance = B(v); break;
                        case "WrapModMethods": WrapModMethods = B(v); break;
                        case "SwallowModErrors": SwallowModErrors = B(v); break;
                        case "WrapCampaignHotspots": WrapCampaignHotspots = B(v); break;
                        case "ShowInGame": ShowInGame = B(v); break;
                        case "MaxReportsPerSession": MaxReportsPerSession = I(v, MaxReportsPerSession); break;
                        case "Stage8ProfileLog": Stage8ProfileLog = B(v); break;
                        case "KeepSessions": KeepSessions = I(v, KeepSessions); break;
                        case "RepeatKeyByMessage": RepeatKeyByMessage = B(v); break;
                        case "WrapModules": WrapModules = v; break;
                        case "LocalRecruits": LocalRecruits = B(v); break;
                        case "FabulaPacerEnabled": FabulaPacerEnabled = B(v); break;
                        case "FabulaPaceDays": FabulaPaceDays = I(v, FabulaPaceDays); break;
                        case "FabulaTimeScale": FabulaTimeScale = F(v, FabulaTimeScale); break;
                        case "NightKingCallEnabled": NightKingCallEnabled = B(v); break;
                        case "NightKingCallTarget": NightKingCallTarget = I(v, NightKingCallTarget); break;
                        case "NightKingCallKeep": NightKingCallKeep = I(v, NightKingCallKeep); break;
                        case "NightKingCallKeepNk": NightKingCallKeepNk = I(v, NightKingCallKeepNk); break;
                        case "NightKingCallFullness": NightKingCallFullness = F(v, NightKingCallFullness); break;
                        case "NightKingMarchEnabled": NightKingMarchEnabled = B(v); break;
                        case "NightKingMarchMin": NightKingMarchMin = I(v, NightKingMarchMin); break;
                        case "NightKingMarchOdds": NightKingMarchOdds = F(v, NightKingMarchOdds); break;
                        case "NightKingMarchRespectCooldown": NightKingMarchRespectCooldown = B(v); break;
                        case "NightKingMarchVerbose": NightKingMarchVerbose = B(v); break;
                        case "NightKingCalendarEnabled": NightKingCalendarEnabled = B(v); break;
                        case "NightKingSiegeFromDay": NightKingSiegeFromDay = I(v, NightKingSiegeFromDay); break;
                        case "NightKingRespectShackles": NightKingRespectShackles = B(v); break;
                        case "NightKingWallFromDay": NightKingWallFromDay = I(v, NightKingWallFromDay); break;
                        case "NightKingCallFromDay": NightKingCallFromDay = I(v, NightKingCallFromDay); break;
                        case "NightKingCalendarSiegeGate": NightKingCalendarSiegeGate = B(v); break;
                        case "OthersNoFreeGrowth": OthersNoFreeGrowth = B(v); break;
                        case "OthersStartDowryDays": OthersStartDowryDays = I(v, OthersStartDowryDays); break;
                        case "OthersNoBirthDowry": OthersNoBirthDowry = B(v); break;
                        case "OthersNoDailyWights": OthersNoDailyWights = B(v); break;
                        case "OthersNoMapVolunteers": OthersNoMapVolunteers = B(v); break;
                        case "StorylineWarNoReparations": StorylineWarNoReparations = B(v); break;
                        case "PovertyPeace": PovertyPeace = B(v); break;
                        case "PovertyPeaceProposals": PovertyPeaceProposals = B(v); break;
                        case "PovertyPeaceArrearsMeasure": PovertyPeaceArrearsMeasure = B(v); break;
                        case "RbSiegeAttritionOnlyStarving": RbSiegeAttritionOnlyStarving = B(v); break;
                        case "HarrenhalBkArmyPass": HarrenhalBkArmyPass = B(v); break;
                        case "HarrenhalWaitForRoose": HarrenhalWaitForRoose = B(v); break;
                        case "HarrenhalWaitHours": HarrenhalWaitHours = I(v, HarrenhalWaitHours); break;
                        case "HarrenhalReleaseCommander": HarrenhalReleaseCommander = B(v); break;
                        case "HarrenhalOldSaveFix": HarrenhalOldSaveFix = B(v); break;
                        case "WallReleaseCommander": WallReleaseCommander = B(v); break;
                        case "WallOldSaveFix": WallOldSaveFix = B(v); break;
                        case "BkBattleRewardPerks": BkBattleRewardPerks = B(v); break;
                        case "BkRebelKingdoms": BkRebelKingdoms = B(v); break;
                        case "QuietBkEducation": QuietBkEducation = B(v); break;
                        case "MarriageFast": MarriageFast = B(v); break;
                        case "MarriageFastShadow": MarriageFastShadow = B(v); break;
                        case "MarriageOnlyViableCandidates": MarriageOnlyViableCandidates = B(v); break;
                        case "MarriageChanceScale": MarriageChanceScale = F(v, MarriageChanceScale); break;   // zakres pilnuje MarriageFast.OnSession (tu log jeszcze nie jest otwarty)
                        case "MarriageTargetPerYear": MarriageTargetPerYear = I(v, MarriageTargetPerYear); break;   // zakres pilnuje MarriageFast.OnSession
                        case "MarriageOffersPerYear": MarriageOffersPerYear = B(v); break;
                        case "BkTitlesUseRotFile": BkTitlesUseRotFile = B(v); break;
                        case "BkTitlesRot818": BkTitlesRot818 = B(v); break;
                        case "LocalLeviesPreachersOnly": LocalLeviesPreachersOnly = B(v); break;
                    }
                }
            }
            catch { }
        }

        private static bool B(string v) { return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase); }
        private static int I(string v, int d) { int r; return int.TryParse(v, out r) ? r : d; }
        private static float F(string v, float d)
        {
            float r;
            return float.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out r) ? r : d;
        }
    }
}
