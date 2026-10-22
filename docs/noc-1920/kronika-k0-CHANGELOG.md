# CHANGELOG galezi w-toku/kronika-00-silnik (Kronika Westeros - silnik K0)

Baza: n32-calosc 77c17f3c (kandydat WGRANIA 102) + scalone kronika-dane (6441d096) + nagrania lektora (fcf2b976, 9e02b0f2).
Plan: repo glowne docs/PLANY-E5-WOJNY-KRONIKA-2026-10-21.md "## PLAN kronika" (E, F, G); kontrakt danych: docs/KRONIKA-FORMAT.md (wygrywa przy sprzecznosci).
Silnik powstaje w 4 krokach lancucha: A1 dane i termy, A2 zachowanie/doba/okna/wybory/zapis (+ akcje T/R), A3 lektor i obrazki (polecenie lancucha 21.10;
wczesniejszy podzial mowil "A3 akcje swiata i obrazki, A4 lektor") - akcje swiata, progi m105kronika i autotest zostaja na kolejne kroki.

## 2026-10-22 - K0 krok B1: akcje swiata i ekonomii (gold, security, loyalty, ware, garrison, item, wound, skill; pop - POMINIETA)

**Problem:** akcje swiata z danych K1-K10 konczyly sie "POMINIETA (akcja jeszcze niewspierana)": gold 25 wierszy (26 uzyc), security 30, loyalty 30, pop 10 (22),
wound 15 (16), garrison 5, item 4, skill 2 (4), ware 0 (skrypt scratchpadu 7aee5f12/kr-b1/akcje_uzycie.py: kolumny skutki okien i opcji, po_rozbiciu, nagroda;
k00 pominiety). Uchwyty kod: w danych: wielka_rada 8, zaraza 1, dzwony 1 (poza zakresem B1).
**Przyczyna:** nowa funkcja (plan E "Chronicle.Actions.cs", KRONIKA-FORMAT 11).
**Zmiana (`Chronicle.Actions.cs`, procedury w Impl, wszystkie klasy S - klucz ChronicleWorldChanges NIE pomija je z linia jak dotad):**
- `gold:<platnik>:<odbiorca>:<kwota|N%>` - kiesa (Purse): bohater (id i tokeny), rod (glowa; rod gracza - gracz), osada (kasa SettlementComponent), `@kasa:<osada>`,
  `@skarbiec:<k>` (KingdomBudgetWallet). Bohater martwy albo Disabled (ukryty - kiesa poza obiegiem), rod wyeliminowany / bez glowy, krolestwo wyeliminowane,
  platnik = odbiorca - POMINIETA. Zamknieta ekonomia: placi min(kwota, zloto platnika, miejsce w kiesie odbiorcy); N% - z biezacego zlota platnika. Bez skarbca -
  zdarzenie gry GiveGoldAction (bohater-bohater, bohater->osada, osada->bohater i osada->osada przez partie osady - gra obcina do kasy; ApplyForSettlementToCharacter
  celowo nie, bo nie obcina do kasy osady); ze skarbcem - arytmetyka "najpierw platnik, potem odbiorca dostaje dokladnie to, co zeszlo" + CrownIncome.ShiftSnapshot
  (przelew poza wplywami dnia), CirculationWindows.NoteHeroGold, ClanIncomeBook.NoteInflow(KRansom) dla bohatera AI (wzor CapitalTreasury) i wiadomosc gracza (EN).
  Linia przy kazdej probie przelewu: "zloto NNN.w: platnik -> odbiorca kwota (zaplacone k[, niedoplata n][, N% z m][, zdjete x - dopisane y])"; nic zaplacone - POMINIETA.
- `security`/`loyalty:<osada>:<d>` - Town.Security/Loyalty wprost (gra trzyma 0-100), tylko miasto i zamek. Sprawdzone w kodzie: DailyLife i PeopleBook tych
  wartosci nie zapisuja; zapisy wprost robia tylko jednorazowe ciosy (CitySack, WarLedger, TownDebt, KingdomTreasury.LordLevy, kredyty TownFolk, NoticeBoard);
  modele (BKLoyaltyModel, model bezpieczenstwa + postfiksy KsHunger) licza dobowy dryf.
- `pop:<osada>:<d>` - POMINIETA "akcja jeszcze niewspierana: ksiega ludzi bez przyczyny kronika ..." (KW-5b INFO) z liczba ludzi osady. PeopleBook ma drogi ruchu
  (Move, TownShift, pula OutlawLaw), ale bez przyczyny "kronika" (FORMAT 11) - cudza przyczyna (rabunek MRaidFlee, podloga TakeForFloor, migracja) zepsulaby
  rownania i progi ksiegi; nowa przyczyna (MKinds 54 + pula wyrzutkow z wlasnym licznikiem) to zmiana PeopleBook/OutlawLaw - osobna decyzja.
- `ware:<z>:<do>:<towar>:<ilosc>` - Settlement.ItemRoster z rynku do rynku (miasto/zamek/wies), najwyzej tyle, ile zrodlo ma (sztuki z modyfikatorami), bez zlota.
- `garrison:<osada>:<ludzi>:<skad>` - skad = osada: zdrowi zolnierze (bez bohaterow i wightow) z jej zalogi proporcjonalnie do skladu, PeopleBook.Transfer (FTransfer);
  skad = pula: ochotnicy notabli osady i jej wsi (sloty VolunteerTypes - ten sam zasob, co autowerbunek zalogi gry), kazdy rekrut przez ksiege ludzi jak
  PeopleBook.A5Post (Straz - WatchLaw.GiveLevy; pobor wylaczony - GiveNone; inaczej TakeLevy MLevyA5 + GiveNew FLevyA5, reszta ponizej podlogi - GiveNone FNoneFloor).
  Niedobor w linii; zaloga w bitwie - CZEKA; osada bez GarrisonParty - POMINIETA.
- `item:<bohater>:<przedmiot>:<skad>` - dragon_* POMINIETA (P4); posiadacz: bohater (wyposazenie, stroj, druzyna, ktora prowadzi), rod (zywi bohaterowie i ich druzyny),
  osada (rynek, schowek). Nikt nie ma - POMINIETA. Cel: gracz - ekwipunek druzyny; inna postac - pasujacy slot BattleEquipment (pusty, inaczej zamiana - zdjety
  przedmiot wraca na miejsce wzietego), bez slotu - druzyna postaci, inaczej cofniecie i POMINIETA. Juz u celu - OK bez zmian.
- `wound:<bohater>:<dni>` - zycie do 1 i rejestr ran (`_wounds`, zapis "kj bohater do zrodlo" w arm_kronika); doba Kroniki (Actions.Daily, przed czekajacymi) trzyma
  rane (zycie z powrotem do 1; gra leczy co godzine) do dnia konca, potem "rana ... koniec terminu"; smierc / brak postaci - wpis usuniety z linia; klucz World Changes
  NIE - rana nie trzymana, termin plynie; druga rana - dluzszy termin; bohater w bitwie - CZEKA. Linia doby: ", rany n".
- `skill:<bohater>:<umiejetnosc>:<d>` - HeroDeveloper.SetInitialSkillLevel(0-330) - poziom i XP razem (ChangeSkillLevel nie umie odjac; SetSkillValue bez XP cofalby
  sie przy nastepnym XP); bez AddSkillXp, wiec ksiega umiejetnosci (SkillLedger) tego nie liczy jako nauki.
- `tools/sprawdz_logi.py`: tylko opis INFO progu KW-5b (lista akcji wspieranych); sprawdz_narzedzie czysto, test_kw.py 35/35.
- `docs/KRONIKA-FORMAT.md` rozdz. 11: notka "Stan po K0 krok B1".
**Ryzyko / co sprawdzic:**
- W grze nic nie sprawdzone (zakaz gry). Autotest: okna z gold (np. 058.2 K5 - skarbiec -> kasa -> wladca Strazy), security/loyalty (009.1 town_EW4),
  garrison:pula (034.1 town_A2), item heartsbane (090.1), wound/skill (037.1 Jaime) - linie "akcja ... -> OK (przed -> po)" i "zloto ...", potkniecia 0, KW-6 bez "z niczego".
- Przelewy ze skarbcem i kasa osady przez arytmetyke (bez zdarzenia gry): ksiegi obiegu widza zmiane pul (suma swiata bez zmian); dobowe "Przeplywy osad"/kasy moga
  pokazac ja jako nierozpoznana - sprawdzic w pierwszym biegu z gold (te same wzory co CapitalTreasury i KingdomTreasury).
- garrison:pula bez kroku BK DeduceManpower (A5Post wola go przy GarrisonLevyFromBk, metoda prywatna w PeopleBook) - BK nie zdejmie ludzi z populacji BK; ksiega
  ludzi Armoury jest zamknieta. Rekruci Kroniki licza sie w liczniku "A5" ksiegi pochodzenia.
- item: wyposazenie AI moze potem zmienic AiGear/HeroGear albo inny mod (unikat moze zostac sprzedany/zdjety) - Kronika przenosi tylko w chwili akcji.
- wound trzyma zycie 1 co dobe: postac (tez gracz przy 050.2a) nie walczy przez caly termin (do 120 dni u lordow 4_21, 3_1, 1_5) - zgodne z danymi.
- Harness A2 (stary): test "gold - niewspierana (A3)" teraz ZLE - oczekiwana zmiana (gold jest wspierane); poza gra Hero.MainHero rzuca NRE, akcja konczy sie
  "POMINIETA (wyjatek ...)" z potknieciem - w grze tej sciezki nie ma.
- Testy: harness B1 (scratchpad 7aee5f12/kr-b1/harness) 67/67 - rejestr (9 procedur, klasa S, 39 akcji), zle argumenty kazdej akcji (linie POMINIETA ze skladnia),
  brak obiektow w swiecie bez wyjatku na zewnatrz (platnik/odbiorca/osada/bohater/przedmiot nieznany, dragon_*), klucz World Changes NIE dla wszystkich 9 (bez linii
  zlota), zapis kj tam i z powrotem (Build -> Import -> Build identyczny), zle linie kj, doba z rana nieznanej postaci (wpis usuniety z linia), Reset. Stare harnessy:
  A1 42/42, A2 34/35 (oczekiwane wyzej), A3 44/44, A4 43/43. kronika_check: WYNIK OK. Build Armoury: 0 bledow, 7 starych ostrzezen.
**Status:** DO SPRAWDZENIA (autotest K0 z oknami akcji swiata przed wgraniem; nic nie wgrane).

## 2026-10-22 - K0 krok A4: ukrycie i powrot postaci (hide/unhide), term kr: w zmianach stron, progi m105kronika, skrypty wgrania

**Problem:** (1) Kronika nie umiala ukryc postaci ani jej przywrocic (akcje hide/unhide "jeszcze niewspierane"; 31 hide i 15 unhide w danych K1-K10); wiersze
#move m11 Sandor, m20 Osha, m21 Sam zabijaly postac ('remove' = KillCharacterAction.ApplyByRemove), wiec powroty z okien 082 (Sam) i 089 (Sandor) nie mialy
kogo przywrocic. (2) Term kr:NNN.w w wyzwalaczach arm_canon_moves (pary 'kr': m02, m04, m18, m22) byl dla CanonCompanions nieznanym rodzajem ("nigdy") - okno
Kroniki nie moglo poprzedzic ruchu. (3) Brak progow logu Kroniki (plan H). (4) wgraj-v5.ps1 i bieg-bitwa.ps1 kopiowaly z Lektor tylko *.wav i *.tsv - 293 nagrania
kr_*.ogg nie trafilyby do gry ("nagran Kroniki 0 ... odrzucone 293 (brak pliku)", okna ciche bez bledu).
**Przyczyna:** nowa funkcja (plan P1, P3, H, L; KRONIKA-FORMAT 8, 11, 15).
**Zmiana:**
- `CanonCompanions.cs`: wykonanie akcji wydzielone do `internal static Res ApplyAction(Hero, akcja, arg, zrodlo, out why)` (Execute #move wola to samo; switch
  akcji bez zmian, zrodlo w linii remove "zmiana strony #mNN (remove)" jak dotad); `Res` internal. Wspolne kontrole: brak postaci / martwa / po stronie gracza -
  POMINIETA (unhide bez kontroli gracza); postac ukryta przez Armoury - kazda akcja poza hide/unhide CZEKA na powrot (#move: do 60 dob). Nowe akcje:
  `hide[:<term konca>]` - zakaz (POMINIETA): glowa rodu, wladca, wylaczony przez gre, lista chronionych przed jej wydarzeniem ROT (`Guarded` = kronika_check.PROTECTED,
  32 postacie, harness: 0 roznic); jeniec, gubernator, bitwa/oblezenie - CZEKA; potem LeaveParty (armia, zbieg, druzyna lorda StartDisband - ludzie nie przepadaja)
  i DisableHeroAction (dekompilacja 1.4.8: z osady, stan Disabled -> lista martwych i wylaczonych przez CampaignObjectManager.HeroStateChanged; heir: Clan.GetHeirApparents
  pomija IsDisabled); rejestr `_hid` (klucz postaci, zrodlo "#m11" / "Kronika 008.1", koniec, dzien) w zapisie arm_canon_companions (linie "hid", niezaleznie od klucza
  CanonCompanionsToClans); drugie hide na ukrytym = nowy koniec ("UKRYCIE: nowy koniec ..."). `unhide[:<osada>]` - tylko postac z rejestru (inaczej POMINIETA
  "nie ukryty", bez teleportu); cel: osada z akcji (nie oblezona, nie kryjowka, nie wroga), inaczej siedziba rodu (SeatOf), inaczej miasto kultury; TeleportHeroAction
  (Disabled -> Active - lista zywych); brak osady - CZEKA. Ratunek przy zniszczeniu rodu: ukryty dostaje tylko rod (bez teleportu, ktory by go przywrocil).
  API: GuardState, HiddenCount, HiddenRows, IsHidden, HiddenHero, DropHidden. KnownActions + hide, unhide. FindHeroId/FindOriginal juz szukaly w liscie wylaczonych.
  Term `kr:NNN.w[=x]` w BaseTrue -> `Chronicle.Resolved`; TermTrue: klucz ChronicleOfWesteros NIE = falsz "nigdy" takze z pamiecia +N (FORMAT 3.3), +N liczone od dnia
  rozstrzygniecia okna (Chronicle.ResolvedDay), nie od nastepnej doby. Linia zmiany strony z alternatywa w wyzwalaczu: "| wyzwalacz ... (zaszlo <term>) (dzien ...".
- `Armoury/ModuleData/arm_canon_moves.tsv` (TEN SAM commit c042d09d): m11, m20, m21 `remove` -> `hide` (p09 Benjen, prolog - dalej remove); komentarz akcji.
- `Chronicle.State.cs`: `Resolved(arg, out never)` i `ResolvedDay(arg)` (most dla CanonCompanions); `Chronicle.Terms.KrTrue` internal.
- `Chronicle.Actions.cs`: procedury `hide`/`unhide` przez ApplyAction (term konca sklejany z SplitArgs, kontrola skladni Terms.Syntax; linie "ukryty <postac> (id)
  (NNN.w) do <term> | ..." i "powrot <postac> (id) -> <osada> (NNN.w) | ..."); `HiddenDaily(day, termsOn)` w dobie Kroniki (przed kluczem NIE): postac zmarla /
  przywrocona przez gre - "UKRYCIE przerwane ... wpis usuniety"; ukryty wybrany przez gre na glowe rodu / wladce - powrot "KONIEC UKRYCIA (glowa rodu)"; klucz TAK:
  term konca prawdziwy albo "nigdy" (FORMAT 11) - powrot do siedziby "KONIEC UKRYCIA (term ...)"; klucz NIE - terminy czekaja (nic nie cofa). Hidden/HiddenText
  z rejestru CanonCompanions ("n (Kronika a, #move b)") w liniach startu i doby.
- `Chronicle.cs`: doba wola Actions.HiddenDaily po Terms.Daily; linia "okno ... pokazane" z "| godzina kampanii H" (prog KW-4).
- `tools/sprawdz_logi.py`: grupa m105kronika (KW-0..KW-15 z planu H + KW-3b, KW-5b, KW-15b INFO), dispatch w main (osobny if/return), opis w pomocy; pary #para
  i okna z kotwica 'przed' czytane z danych repo, lista chronionych z tools/kronika/kronika_check.py (zapas - lista wbudowana), --crashscribe dla pary listy
  z 'AUTOTEST: OKNO wybor z listy'. sprawdz_narzedzie: czysto.
- Repo glowne (5b86d366): tools/autotest-2026-10-17/wgraj-v5.ps1 i bieg-bitwa.ps1 - takze Lektor/**/*.ogg (manifest arm_lektor_kronika.tsv szedl juz jako *.tsv);
  wgraj: kontrola OggS i md5 pliku z kolumny 5 manifestu (STOP przy zerach); oba: Armoury/GUI/Prefabs/Nameplate/*.xml, gdy sa w drzewie (galaz czcionek).
**Ryzyko / co sprawdzic:**
- W grze nic nie sprawdzone (zakaz gry w kroku). Pierwszy autotest K0a (hide/unhide na postaci testowej): linie "ukryty", "powrot", brak potkniec; bohater po hide
  w Hero.DeadOrDisabledHeroes, po unhide w osadzie (stan Active). Do sprawdzenia w grze: BK, ROTLords i DTE wobec bohatera Disabled (wyszukiwania po zywych go nie
  widza; dekompilacja tylko vanilla), CrashScribe UC (spis unikatow) - ukryta postac kanonu moze liczyc sie jako "usunieta".
- Jeniec i gubernator przy hide = CZEKA (FORMAT 11 mowi "zakaz"); po 30 dniach (Kronika) / 60 dobach (#move) - POMINIETA z powodem. Zamiana na natychmiastowe
  POMINIETE to jedna linia w CanonCompanions.Hide.
- m11/m20/m21 w kampanii Jeffa (prolog od 20.10) jeszcze nie wykonane - zmiana dziala od nastepnego WGRANIA; po wgraniu Sam/Sandor/Osha znikaja ukryciem, okna 082/089
  ich przywracaja (unhide z rejestru). Okna zalezne od zywych Sama/Sandora (064.2a, 082.2, 082.3, 089.1, 090.1, 101.1, 111.1a, 115.2d, 905.1) maja teraz szanse.
- Kolejnosc doby: CanonCompanions (ruchy #move, term kr:) przed Kronika - okno rozstrzygniete w AppTick dnia D, ruch z kr:NNN.w+1 rusza w dobie D+1 (pamiec od D).
- Nie zrobione w K0 (dalej "akcja jeszcze niewspierana"): gold, band, task, tourney, security, loyalty, pop, ware, garrison, muster, wound, skill, item, clan, court,
  lead, prisoner, free, wander, die, marry, wed, succeed, own, ruler, leader, alliance, truce; wyzwalacz brokenmen (045). ApplyAction gotowe dla clan/court/lead/
  prisoner/free/wander/die/marry/wed - podpiecie w Actions.Impl wymaga kontroli z FORMAT 8 (postac z wierszami #move - tylko para) i 11 (die glowy rodu w lekka -
  POMINIETA).
- Testy: harness A4 (scratchpad 7aee5f12/kr-a4/harness) 43/43 - parser (m11/m20/m21 = hide, p09 remove, 0 zlych wierszy), zapis hid tam i z powrotem, ApplyAction
  bez postaci, Resolved (okno, =x, inna opcja nigdy, pominiete nigdy, nieznane falsz), TermTrue +N od dnia okna, klucz NIE = nigdy (takze z pamiecia), Fires(m22)
  "zaszlo kr:008.1+1", 008.1 pominiete -> m22 nie odpala, ale nie "nigdy", hide/unhide - zle argumenty i klucz swiata; Guarded = PROTECTED (0 roznic). Stare
  harnessy: A1 42/42, A2 35/35, A3 44/44. Progi: test_kw.py 35/35 (log dobry bez NIE, kazdy KW-0..KW-15 z logiem zlym = NIE, log bez Kroniki i klucz NIE - INFO).
  wgraj-v5.ps1 -DryRun na drzewie kronika-k0: kod 0, 293 ogg + manifest "bylaby podmiana"; drzewo probne: ogg z zerami na koncu / w naglowku = STOP (kod 2),
  Nameplate/*.xml w tabeli; lista plikow bieg-bitwa.ps1 (fragment uruchomiony osobno): 293 ogg, manifest, 58 wav. kronika_check: WYNIK OK.
**Status:** DO SPRAWDZENIA (autotest K0a/K0b/K0c przed wgraniem; nic nie wgrane).

## 2026-10-21 - K0 krok A3: lektor okien Kroniki (klucz, zasada md5, .ogg) i obrazki z puli tematu

**Problem:** okna Kroniki (A2) pokazywaly sie bez glosu (Lektor szukal nagran tylko po tytule i tylko w .wav - 293 nagrania kr_*.ogg z manifestem
arm_lektor_kronika.tsv lezaly bez uzytku; warianty o tym samym tytule czytalyby zly tekst) i zawsze z pierwszym sprite'em tematu (Pictures.Pick = null).
**Przyczyna:** nowa funkcja (plan P8, P9; E: Lektor.cs _byKey + .ogg, Chronicle.Pictures.cs).
**Zmiana:**
- `Lektor.cs`: indeks PO KLUCZU (_byKey) przed indeksem po tytule - klucz z kolumny 1 arm_lektor.tsv (58 WAV okien ROT i prologu: po tytule dalej jak dotad)
  oraz z NOWEGO manifestu Lektor/EN/arm_lektor_kronika.tsv (klucz, tytul, plik, md5 tekstu, md5 pliku, paczka) - wiersze Kroniki tylko po kluczu. Klucz czytany
  z okna opowiesci Armoury (typ Armoury.ArmStoryEvent po nazwie: Key, VoiceMd5); okna ROT bez zmian (tytul). ZASADA md5: nagranie Kroniki gra tylko przy md5 tekstu
  z manifestu = VoiceMd5 okna; md5 nie-hex ("nieaktualne", 4 wiersze) = cisza z linia; okno Kroniki bez nagrania - cisza bez szukania po tytule.
  Pliki .ogg obok .wav (CheckAudio): CheckOgg - "OggS" + pakiet identyfikacji Vorbis (czestotliwosc) + bloki zer na koncu i w srodku przy wczytaniu, md5 pliku
  i dlugosc z granuli ostatniej strony przy odtworzeniu. Ta sama droga SoundEvent.CreateEventFromExternalFile("event:/Extra/voiceover") - w dekompilacji SandBox 1.4.8
  EavesdroppingMissionLogic gra tak pliki StoryMode VoicedLines/*.ogg (pelna sciezka). Linie: start/latka "| nagran Kroniki N (ogg n; ok, ze starym tekstem s,
  odrzucone r ...)" po czesci okien ROT (format "nagran okien N" bez zmian - LK-3); okno '... - start. Po kluczu kr_NNN_w (Kronika, md5 tekstu zgodny).';
  cisza '(klucz kr_..) - tekst okna inny niz nagrany (md5 okna x, nagrania y), cisza.'; doba: ", po kluczu n, Kronika z innym tekstem n" na koncu czesci okien.
  API: WindowsEnabled, KronikaText(), VoiceWhy(key, textMd5), CheckAudio/CheckOgg.
- `Chronicle.Voice.cs` (nowy): tekst nagrania DOKLADNIE jak tools/lektor/nagraj_kronika.py (tytul/gora/dol bez {=...}, tytul.rstrip('.!?') + "." + 

 + gora +
  

 + dol, "R'hllor" -> "the Red God"; '{' = zmienne = bez glosu), Md5, Why (zasada md5 przez Lektor.VoiceWhy), LineText (do linii okna).
- `StoryWindow.cs`: Spec.VoiceMd5, ArmStoryEvent.VoiceMd5 (publiczny - lektor czyta odbiciem).
- `Chronicle.Pictures.cs`: Pick - pula tematu (pliki Images/Kronika/kronika_<temat>_<n>, istniejace Images/Prolog/prolog_NN z kolumny prolog, sprite'y ROT bez
  poza rotacja i bez nieznanych w SpriteData), indeks = FNV-1a id okna mod n, poprzedni obrazek sesji -> nastepny, plik przez StoryPictures.Acquire (zly plik -
  nastepny element z powodem), temat z kolumna tylko (stannis_upadek = 081) poza swoim wydarzeniem - sprite zastepczy messenger-img, stannis-slain tylko w 081
  (twardo). Fallback (DefaultSprite), StartText (pliki w pulach), DayText, Reset (ResetCampaign), Restore (okno nie otworzylo sie - pamiec obrazka wraca).
- `Chronicle.cs`: bez ScanRecordings/_ogg (lektor z manifestu przez Voice); linia startu: "obrazki: plikow Kroniki ..., prolog_NN z tematow a/b" i "lektor: wierszy
  lektor=1 z nagraniem a/b (bez nagrania, tekst inny, stary tekst, plik odrzucony, zmienne; klucze lektora okien TAK/NIE) | nagran Kroniki ..."; linia
  "okno ... pokazane": "obrazek temat->nazwa (ROT|plik; pula n (...), indeks i -> nazwa)" i "lektor kr_NNN_w TAK | NIE (powod) | - (lektor=0)" (okno zapasu: NIE);
  doba: "| obrazki w sesji: wybrane n (z pliku n, pliki odrzucone n)".
**Ryzyko / co sprawdzic:**
- K0c (autotest, nie robiony - zakaz gry): czy silnik gra .ogg 24 kHz mono z naszej sciezki (glosy gry: .ogg 48 kHz mono) - linie "okno ROT ... -> kr_NNN_w.ogg
  (x s) - start." i "grane po 0,5 s: tak", koniec bez "ZA WCZESNIE"; "silnik nie przyjal" = 0. Jesli nie gra - nagraj_kronika.py moze dac .wav (manifest przyjmuje .wav).
- Skrypt wgrania (repo glowne tools/autotest-2026-10-17/wgraj-v5.ps1) kopiuje z Lektor tylko *.wav i *.tsv - bez zmiany (osobny commit, plan E) nagrania .ogg nie
  trafia do gry: linia startu lektora pokaze "nagran Kroniki 0 ... odrzucone 293: kr_001_1.ogg (brak pliku)", okna Kroniki beda bez glosu (bez bledu).
- Wczytanie manifestu przy Install (menu glowne): 293 pliki x (naglowek 64 B + 2 bloki 4 KB) - jednorazowo; md5 pliku (ok. 300 KB) dopiero przy odtworzeniu.
- 18 wierszy lektor=1 bez nagrania: 17 wersja=pelna (skrypt ich nie nagrywa) + 051.1a (brak w manifescie - nagrac). 4 "nieaktualne" (075.3, 079.1, 090.1, 090.1a).
- Obrazki z plikow: poza gra Acquire zwraca null (brak SpriteData) - w grze wejda prolog_01..03 (tematy dwor, polnoc, essos ...). Kronika/kronika_* dzis brak.
- Testy (scratchpad 7aee5f12/kr-a3): md5_test.py - prawdziwa funkcja windows() z nagraj_kronika.py: 289 md5 = manifest, 4 stare, 0 roznych; C# Chronicle.Voice.Md5
  vs python dla wszystkich 294 wierszy skryptu: 294 zgodne, 0 roznych; harness (prawdziwy Armoury.dll poza gra) 44/44 OK - manifest 293/ogg 293/stare 4/odrzucone 0,
  indeks okien ROT bez zmian (58), zasada md5 (zgodny / inny / nieaktualne / brak / prolog bez kontroli / zmienne), skladanie tekstu i R'hllor, CheckOgg (dobry, zera
  na koncu/naglowku/srodku, WAV jako .ogg, uciety - md5, za krotki, brak), manifest negatywny (2 wczytane, 3 odrzucone, 4 zle linie, dubel, linia z zerami), pule
  tematow, stannis_upadek tylko 081, sprite'y poza rotacja, staly skrot (FNV = python), bez powtorki z rzedu (346 okien; jedyna powtorka - pula 1 stannis_upadek),
  Fallback; harness A1 42/42 (zrzut modelu identyczny), A2 35/35. Gra NIE uruchamiana.
- Build Armoury 0 bledow, 7 ostrzezen (stare).
**Status:** DO SPRAWDZENIA (krok A3 z 4; bez wgrania).

## 2026-10-21 - K0 krok A2: zachowanie, okna, wybory, zapis

**Problem:** po A1 dane i termy byly w DLL, ale nic ich nie wolalo - Kronika nie miala doby, okien, list wyboru, skutkow ani zapisu.
**Przyczyna:** nowa funkcja (plan E: Chronicle.cs, Chronicle.Choice.cs, Chronicle.Actions.cs; FORMAT 3.2, 4, 11, 13).
**Zmiana:**
- `Armoury/src/Chronicle.cs` (nowy): `ChronicleBehavior` (OnNewGameCreated, OnSessionLaunched, DailyTick, BeforeHeroKilled, RaidCompleted, SyncData "arm_kronika"
  przez SaveText.Sync), rejestrowany w SubModuleMain PO CanonCompanionsBehavior. Silnik: `SessionStart` (Data.EnsureLoaded, paczki nowe w zapisie - "zapis sprzed
  paczki kNN" dla okien minionych przy wczytanym zapisie, kolejka okien, linia startu wg planu G), `Daily` (Terms.Daily, akcje czekajace, okna powracajace, plan
  okien, linia doby), `Plan`: po/warunki co dobe dla kazdego okna (pamiec +N), "nigdy" -> POMINIETE, kotwica przed / spoznienie > MaxLate po do -> wiesc (tekst _W)
  albo "nic" albo POMINIETE, nalezne -> PickRow (warianty w kolejnosci pliku z filtrem wersji, potem glowny; zmienne {NAZWA} z tokenow - brak wartosci = wiersz
  odpada) i forma (gracz -> forma, inaczej inni); okno do kolejki, wiesc/log/nic od razu. `AppTick` (SubModuleMain.OnApplicationTick, 4 razy na sekunde, osobny
  licznik bledow ladowania typu): Prologue.SceneGate + StoryWindow.IsOpen + StableSec 2 s, PerDay 2, GapHours 6 od okna Kroniki i od zejscia okna wydarzenia ze
  stosu stanow; ponowna kontrola warunkow przy pokazaniu; okno StoryWindow (Key = kr_NNN_w[war], tytul/gora/dol ze zmiennymi, obrazek Pictures.Pick -> null =
  pierwszy sprite ROT tematu); rozstrzygniecie przy pokazaniu (State.Resolve, skutki okna, wybor); 3 nieudane otwarcia = POMINIETE. Linie: "okno ... pokazane",
  "zamkniete po s (jak)", "wiesc/log/nic NNN.w (powod)", "POMINIETE okno NNN.w - powod", doba, start, "zapis arm_kronika n KB" przy kazdym zapisie gry.
- `Armoury/src/Chronicle.Choice.cs` (nowy): lista MultiSelection po zamknieciu okna (radio, bez wyjscia, opis z id okna, "Confirm"), ChoiceDelay 0,5 s (po
  wczytaniu StableSec), stan 'a' w zapisie ("ka") - lista wraca po wczytaniu; opcja bierna bez pytania przy formie innej niz okno, graczu poza kolumna gracz,
  kluczu Chronicle Player Choices NIE albo 0 dostepnych; plik CrashScribe/armoury-kronika-wybor.flag (a-d, LogGate 1/2) wymusza dostepna opcje; lista zniknieta bez
  wyboru wraca. Linia "wybor NNN.w = x 'etykieta' (z n; dostepne ..; wymuszone flaga TAK/NIE; bierna TAK/NIE)".
- `Armoury/src/Chronicle.Actions.cs` (nowy): parser ' ; ' + Terms.SplitArgs, rejestr 39 akcji FORMAT 11 z klasa T/R/S (klucz World Changes NIE pomija S z linia),
  wynik Done/Skip/Wait (CZEKA ponawiane co dobe do WaitActionDays 30, potem POMINIETA; zapis "kw"). Zaimplementowane: wiesc, plotka, flag, kod (tylko test; inny -
  POMINIETA "brak uchwytu"), rel, relp, relk, renown, influence. Reszta (gold, band, hide, unhide, security, loyalty, ...) - "POMINIETA (akcja jeszcze niewspierana)".
- `Armoury/src/Chronicle.Recurring.cs` (nowy): okna powracajace - kolejka instancji NNN.w#klucz ("kq"), max/odstep (licznik "kl"), prolog, wydanie najwyzej 1
  instancji widocznej na dobe, MaxLate, rejestr kluczy 400 (State.PruneInstances). Wyzwalacze: season, siegehunger, ironraid, oldage, others, captured, besieged,
  wizja; brokenmen niewspierany (okno czeka; linia startu go wymienia).
- `Armoury/src/Chronicle.Pictures.cs` (nowy): interfejs Pick (A3 wypelni; teraz null).
- `Chronicle.State.cs`: FormCounts, PruneInstances. `Prologue.Scenes.cs`: SceneGate private -> internal (logika bez zmian). `StoryWindow.cs`: ArmStoryEvent.Key.
- `Rumours.cs`: AddStoryNews(textId, osada|krolestwo, dni, noLive) - osobna lista plotek Kroniki (wlasny czas zycia, bez wypierania przez wiesci wojenne), 4. czesc
  zapisu arm_rumours (stary DLL czyta 3), sluchanie w Wars (krolestwo - karczmy jego ziem, osada - do 450 jedn. z opoznieniem krukow). `Lektor.Live.cs`:
  MarkNoLive - plotka Kroniki nie idzie do API na zywo (nagranie z pamieci gra), licznik w linii strony i doby.
- `SubModuleMain.cs`: AddBehavior(ChronicleBehavior), Chronicle.AppTick obok scen prologu, OnGameEnd -> Chronicle.GameEnd.
**Ryzyko / co sprawdzic:**
- Kampania Jeffa (prolog, n32): Kronika ruszy dopiero w dniu opowiesci >= od okien (K1 od dnia 10 x k); w prologu tylko okna powracajace z opcja prolog (015)
  i pomijanie "nigdy". Zapis Jeffa bez arm_kronika -> paczki nowe w zapisie: w prologu nic nie jest "minione" (dzien opowiesci ujemny) - zero pominiec.
- "nigdy" jest trwale: mv: przy kampanii bez postaci kanonu albo przy wyzerowanym arm_canon_moves.tsv (CanonCompanions bez wierszy) zwraca nigdy -> okna z mv:
  POMINIETE na stale (logika A1). Do obserwacji w pierwszym biegu: liczba POMINIETE "warunki swiata: mv:" w dobie 1 po starcie opowiesci.
- Skutki okna ida przy POKAZANIU (rozstrzygnieciu), nie przy zamknieciu - relp pokazuje powiadomienie gry nad otwartym oknem.
- Rumours: Wars bierze 2 najwyzej punktowane wiesci strony - plotki Kroniki (2.6+) wypieraja wiesci wojenne z tej kategorii, dopoki zyja (30-90 dni).
- Okna powracajace: oldage tylko glowy rodow z tierem >= 4 albo rodu wladcy (zgadywane "wazny rod"); ironraid = sturgia + warownia wsi z portem (zgadywane
  "wybrzeze"); odstep = dni od ostatniej instancji OKNA (nie klucza); limit 1 na dobe liczy tylko formy widoczne (okno/wiesc) - log/nic bez limitu.
- Brak w A2 (A3/A4): akcje swiata, obrazki z puli, lektor po kluczu i .ogg, hide/unhide w CanonCompanions + m11/m20/m21, term kr: w arm_canon_moves, progi
  m105kronika w sprawdz_logi.py, autotest K0a/K0b w grze.
- Testy: harness scratchpad 7aee5f12/kr-a2/harness (prawdziwy Armoury.dll poza gra): 35/35 OK - akcje (OK/POMINIETA, klucz World NIE, rejestr 39 = akcje danych),
  wybory (bierna bez pytania, stan 'a', bez dubla, forma wiesc, klucz Choices NIE), zapis Build -> Import -> Build tozsamy, stary zapis i zapis z bledami bez wyjatku,
  CheckPacks (11 paczek, dzien 0 bez pominiec), Plan i linia doby bez wyjatku, PickRow, tokeny, PruneInstances, Rumours 4. czesc zapisu tam i z powrotem, MarkNoLive.
  kr-a1/kronika_sim.py: akcje danych - wszystkie w Actions.KnownActions (w C#, a nie w danych: lead, marry, ruler, wander, ware). Gra NIE uruchamiana (zakaz kroku).
- Build Armoury 0 bledow (7 starych ostrzezen, zadne z plikow tej zmiany).
**Status:** DO SPRAWDZENIA (krok A2 z 4; bez wgrania).

## 2026-10-21 - K0 krok A1: dane Kroniki i warunki (termy)

**Problem:** 117 wydarzen Kroniki (212 okien z k00) lezy w Armoury/ModuleData/arm_kronika_kNN.tsv, ale gra ich nie czyta - silnika K0 nie ma.
Krok A1 = fundament: wczytanie plikow do modelu i liczenie warunkow (FORMAT rozdz. 2-10) - bez okien, bez doby, bez akcji (A2-A4).
**Przyczyna:** nowa funkcja (plan E: Chronicle.Data.cs, Chronicle.Terms.cs).
**Zmiana:**
- `Armoury/src/Chronicle.Data.cs` (nowy): `internal static partial class Chronicle` - Tag "Kronika Westeros (KW): ", klucze MCM (KeyOn/KeyWorld/KeyFull/KeyChoices),
  potkniecia (Stumble: Log.Error tylko 1-5), TestWanted (LogGate.Mode 1/2 + CrashScribe/armoury-kronika-test.flag); `Chronicle.Data`: EnsureLoaded() (pierwszy raz albo
  po zmianie rozmiaru/daty/zestawu plikow/flagi testu), paczki kNN (k00 tylko przy tescie), sekcje #okno #wybor #tekst #banda #zadanie #para #usuniete do klas modelu
  (WindowDef z wierszem glownym i wariantami, RowDef, OptionDef, BandDef, TaskDef, PairDef, RecurDef, VarDef), tematy z arm_kronika_tematy.tsv (TopicDef).
  Zly plik (pusty, bajt 0, nie UTF-8, brak naglowkow, nieznana sekcja, inne kolumny, dane przed naglowkiem) - linia "ZLY PLIK ... - paczka wylaczona", reszta paczek dziala.
  Zly wiersz - "ZLY WIERSZ plik:wiersz (powod)" (pierwsze 10, potem licznik w Summary()). Nieznany term w warunkach - wiersz ZOSTAJE (term = falsz), linia "NIEZNANY TERM".
  Teksty: surowe komorki {=ARM_KR_...}, TextObject swiezy przy kazdym odczycie (Tx). Dziedziczenie tekstow i tematu wariantu z wiersza glownego.
- `Armoury/src/Chronicle.Terms.cs` (nowy): wszystkie termy FORMAT 10 (ev, done, dead, alive, clanof, head, leads, prisoner[:x], war, peace, kexists, own, rotday,
  prolday, kr[=x], flag, mv[=done|skip], cw:reach|dorne, season, plan:X-N, p:region/in/near/vassal/army/rel), skladnia ' & ' / ' | ' (lub wiaze mocniej) / '!' / '+N'
  z pamiecia pierwszego spelnienia (zapis), wynik trojwartosciowy (prawda / falsz / NIGDY), nieznany term = falsz z linia raz na term. Wyszukiwanie bohater/rod/
  krolestwo/osada z tokenami (FindHero/FindClan/FindKingdom/FindSettlement, SplitArgs - wspolne z akcjami A3), indeksy odswiezane co godzine gry.
- `Armoury/src/Chronicle.State.cs` (nowy): stan okien czytany przez kr:/flag: (rozstrzygniete z forma i wierszem, pominiete z powodem, wybory, flagi, instancje
  okien powracajacych NNN.w#klucz) z Export/Import linii kr/ks/kc/kf (zapis podlaczy A2).
- `CanonCompanions.cs`: BaseTrue private -> internal (logika bez zmian; TermBase w kodzie nie istnieje); nowe internal MoveState(id) ("done"/"skip"/"wait"/null),
  MoveDay(id), FirstDay(baza), ClearEventCache() - tylko odczyt stanu.
- `Prologue.Crown.cs`: internal CrownGuardHolds(which, out never) - odczyt _rcHeld / _cdOurs (never: straznik zakonczony albo poza zakresem); logika straznika bez zmian.
- `Settings.cs` grupa "Chronicle of Westeros": ChronicleOfWesteros (TAK), ChronicleWorldChanges (TAK), ChronicleFullVersions (NIE), ChroniclePlayerChoices (TAK);
  gen_mcm Armoury 2279.
**Ryzyko / co sprawdzic:**
- Nic jeszcze nie wola Chronicle.* (ChronicleBehavior, OnSessionLaunched -> Data.EnsureLoaded, doba -> Terms.Daily, SyncData -> State/Terms Export/Import - krok A2):
  DLL zachowuje sie jak przed zmiana, poza 4 nowymi kluczami w MCM (bez dzialania do A2-A4). Zmiany w CanonCompanions i Prologue.Crown to nowe metody odczytu - stare
  sciezki bez zmian (BaseTrue tylko widocznosc).
- ClearEventCache czysci pamiec stanu wydarzen ROT CanonCompanions (_evDay) - ta pamiec sluzy tylko unikaniu powtornego odbicia; Moves() czysci ja i tak na poczatku przebiegu.
- kr: przy wylaczonej Kronice = NIGDY (FORMAT 3.3) - przy wylaczeniu klucza koniec ukrycia `kr:` w A3 skonczy ukrycie (zgodnie z FORMAT 11).
- `+N` liczy od pierwszego dnia prawdy: dla ev:/done: wszystkich 29 wydarzen zapis co dobe (Terms.Daily), dla kr: dzien rozstrzygniecia z State, dla mv: dzien z
  CanonCompanions; pozostale (dead, war, own ...) od pierwszego liczenia - A2 powinien liczyc warunek `po` codziennie (nie dopiero od `od`).
- plan:X-N bez CrashScribe (albo gdy AccessTools.TypeByName zawiedzie) = prawda (decyduje od) - linia startu A2 powinna mowic "plan Fabuli TAK/NIE" (Terms.PlanAvailable).
- Testy: python -I scratchpad 7aee5f12/kr-a1/kronika_sim.py (symulacja parsera na k00-k10: 0 zlych plikow/wierszy/termow, termy danych w C# Kinds - roznica tylko prolday
  nieuzyty, 34 akcje danych do obslugi w A3); harness 7aee5f12/kr-a1/harness (prawdziwy Armoury.dll poza gra, odbiciem): Summary jak wyzej, zrzut modelu C# == zrzut symulacji
  (485 linii, identyczne), 42 testy logiki termow OK (kr/flag/mv/plan/tokeny/nigdy/negacja/+N/zapis tam i z powrotem), pliki negatywne (bajt 0, pusty, nie UTF-8, nieznana
  sekcja, inne kolumny, dane przed naglowkiem, same komentarze) = ZLY PLIK, 18 zlych wierszy k02 zgodnie z oczekiwaniem. Gra NIE uruchamiana (zakaz kroku).
- Build Armoury 0 bledow (pelny rebuild: 7 ostrzezen, wszystkie stare, zadne z Chronicle*).
**Status:** DO SPRAWDZENIA (krok A1 z 4; bez wgrania).
