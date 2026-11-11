# Doktryna dowodzenia (antyk - sredniowiecze) jako reguly decyzyjne dla AI bitwy

Zakres: bitwa polowa do ~1000 ludzi na strone, formacje: piechota (P), strzelcy (S), jazda (J), strzelcy konni (SK), wodz AI.
Stan weryfikacji: WebSearch potwierdzil tylko ogolny uklad zrodel (Wegecjusz ks. III: odwody, 7 szykow; Strategikon: zasadzki i pozorowana
ucieczka; Praecepta: klin katafraktow, czworobok piechoty z przejsciami dla jazdy, poscig jako osobny temat; regula templariuszy: zakaz
szarzy bez pozwolenia, zbiorka przy choragwi). Tresc szczegolowa ponizej pisana Z WIEDZY. Numery rozdzialow = [~] gdy niepewne.
Progi liczbowe w polach "AI" to PROPOZYCJE projektowe, nie dane historyczne.

## A. Zrodla - co ktore wnosi (skrot)

| Zrodlo | Glowna nauka dla AI |
|---|---|
| Wegecjusz, Epitoma III (IV/V w.) | teren wyzej, slonce/kurz/wiatr w oczy wroga (III.14), odwod zamiast wydluzania linii (III.17), 7 szykow (III.20), "zloty most" dla uciekajacych (III.21), bitwa walna tylko przy przewadze lub z koniecznosci (III.9, III.26) |
| Onasander, Strategikos (I w.) | wodz nie walczy osobiscie [~33], wyborowy oddzial trzymany z boku na swiezosc [~22], lekkozbrojni przed frontem/w odstepach [~17-20], nie rozciagac linii kosztem glebokosci [~21], poscig w szyku |
| Frontyn, Strategemata II | II.1 wybor czasu (zmeczyc/zglodzic wroga czekaniem), II.2 miejsce, II.3 szyk (silne skrzydla, slaby srodek - Ilipa, Kanny), II.4 zamet u wroga, II.5 zasadzki, II.6 wypuscic osaczonego, II.7 ukryc niepowodzenie, II.8 przywrocic szyk stanowczoscia |
| Strategikon Maurycego (ok. 600) | dwie linie + odwod (jedna linia = hazard), cursores (poscig w luznym szyku) + defensores (zwarci, ida za nimi), straz skrzydel i oskrzydlajacy, oddzialy zasadzkowe, zakaz bezladnego poscigu, ks. XI: inna taktyka na kazdy typ wroga |
| Taktika Leona VI (ok. 900) | powtarza Maurycego; konst. XVIII: na konnych lucznikow - szybko zwierac, strzelac w konie, ostroznie scigac |
| Praecepta militaria (ok. 965) | piechota w pustym czworoboku z przejsciami = ruchoma baza jazdy; klin katafraktow stepa, w ciszy, celem jest wodz wroga; prokoursatores scigaja, linie ida za nimi w szyku |
| Usama ibn Munkidh (XII w.) | anegdoty, nie traktat: Frankowie "najostrozniejsi na wojnie", licza sie z zasadzka, nie scigaja daleko; pojedyncza zdyscyplinowana szarza rozstrzyga [~] |
| Regula templariuszy (retrais, ok. 1165; kl. ~161-168 Upton-Ward) | nikt nie szarzuje ani nie opuszcza szyku bez rozkazu; marszalek daje znak choragwia; 5-10 rycerzy pilnuje choragwi; zapasowa choragiew zwinieta; po rozbiciu zbiorka przy dowolnej choragwi wlasnej strony; z pola schodzi sie dopiero gdy zadna nie stoi |
| Christine de Pizan, Fais d'armes (ok. 1410) | Wegecjusz + Frontyn + praktyka XIV w.: trzy hufce (czolowy, walny, tylny), strzelcy na skrzydlach wysunieci, spieszeni zbrojni w srodku; nie bic sie bez przewagi; zostawic droge ucieczki |

## B. Reguly decyzyjne

Format: JESLI -> WODZ ROBI | zrodlo | powodzenie / porazka | wskazowka AI.

### Faza 0 - przed bitwa

**R1. Teren wyzej.** JESLI w zasiegu rozstawienia jest wzniesienie -> zajmij je i ustaw linie na stoku, strzelcow najwyzej. | Weg. III.13; Frontyn II.2. | Dziala gdy wrog musi podejsc; zawodzi gdy wrog ma przewage strzelcza i sam czeka. | AI: wybor pozycji = wysokosc + oslona skrzydla; stan "broni wzgorza" porzucic, jesli traci od ostrzalu > niz zadaje.

**R2. Slonce, wiatr, kurz.** JESLI mozna wybrac kierunek -> slonce i wiatr za plecami (strzaly niosa dalej, kurz leci w oczy wroga). | Weg. III.14; Strategikon VII [~]. | Drobna premia, nie warto dla niej oddawac wzgorza. | AI: przy remisie pozycji wybrac te z wiatrem/sloncem z tylu; deszcz/mgla = slabsi strzelcy -> strona strzelcza czeka, strona wrecz atakuje.

**R3. Oprzyj skrzydlo.** JESLI masz mniej jazdy lub mniej ludzi -> oprzyj jedno/oba skrzydla o rzeke, las, urwisko, zabudowania (7. szyk Wegecjusza). | Weg. III.20. | Zawodzi gdy przeszkoda jest przejezdna dla jazdy. | AI: slabszy w jezdzie nigdy nie staje w otwartym polu jesli jest alternatywa.

**R4. Typ terenu pod wlasna bron.** JESLI przewaga w jezdzie -> otwarte, rowne pole; JESLI przewaga w piechocie -> teren ciasny, nierowny, zalesiony; przeciw SK - zwarty szyk w otwartym, bez zarosli do zasadzek. | Weg. III.9, III.13; Strategikon XI. | AI: ocena skladu obu stron wybiera pole z listy miejsc rozstawienia.

**R5. Bic sie czy czekac.** JESLI brak przewagi (liczba, jakosc, morale, pozycja) -> nie przyjmuj bitwy walnej z marszu: stoj w obronie, niech wrog atakuje pod gore/pod ostrzal. JESLI wrog zmeczony marszem, glodny, w trakcie rozwijania szyku albo przeprawy -> atakuj natychmiast. | Weg. III.9, III.11, III.26; Frontyn II.1; Onasander. | AI: postawa (atak/obrona/zwloka) z porownania sil + stanu wroga, nie stala dla frakcji.

**R6. Zmienny plan.** JESLI bijesz sie drugi raz z tym samym przeciwnikiem -> nie powtarzaj szyku; plan trzymany w tajemnicy. | Weg. III.26 (wrog nie moze znac zamiaru); Strategikon VIII [~]. | AI: losowanie wariantu z wag zaleznych od skladu, z kara za powtorzenie poprzedniego.

### Faza 1 - szyk

**R7. Zawsze odwod.** Zatrzymaj z tylu 10-25% sil (najlepsi piesi i konni), za srodkiem i za skrzydlami; lepiej krotsza linia z odwodem niz dluga bez niego. | Weg. III.17; Onasander [~22]; Strategikon II (dwie linie + trzecia). | Odwod wygrywa, gdy wchodzi w luke lub w bok zwyciezajacego wroga (Tagliacozzo 1268); przegrywa, gdy wodz rzuca go za wczesnie. | AI: osobna formacja "odwod" z zakazem ataku do spelnienia R19/R20/R22.

**R8. Glebokosc przed dlugoscia, ale nie daj sie oskrzydlic.** JESLI wrog liczniejszy -> nie rozciagaj do jego dlugosci; odmow jedno skrzydlo (szyk skosny: 2., 3., 6. Wegecjusza), atakuj mocniejszym. | Weg. III.20; Onasander [~21]. | Zawodzi, gdy skrzydlo odmowione nie ma oparcia ani jazdy oslonowej.

**R9. Jazda na skrzydlach, ciezka przy piechocie.** Ciezka jazda bezposrednio przy boku piechoty, lekka/SK dalej na zewnatrz do oskrzydlania; slabsza jazde wymieszaj z lekka piechota. | Weg. III.16; Strategikon II-III (straz skrzydla + oskrzydlajacy). | AI: dwie role jazdy - "oslona boku" (nie odchodzi) i "oskrzydlenie" (wolna).

**R10. Strzelcy maja oslone.** Strzelcy przed frontem tylko na czas ostrzalu; przed zwarciem cofaja sie przez odstepy za ciezka piechote albo staja na skrzydlach za przeszkoda (pale, row, zywoplot). | Onasander [~17-20]; Weg. III.14-15; Pizan (skrzydla strzelcze). | Bez przeszkody i bez oslony jazdy strzelcy gina od szarzy (Patay 1429); z palami i blotem odpieraja ja (Azincourt 1415).

**R11. Miejsce wodza.** Wodz za pierwsza linia (u Wegecjusza prawe skrzydlo miedzy piechota a jazda, u Maurycego srodek drugiej linii), z przyboczna straza jako ostatnim odwodem; nie walczy wrecz, dopoki bitwa nie wisi na wlosku. | Weg. III.18; Onasander [~33]; Strategikon II. | Smierc/ucieczka wodza = zalamanie (Hastings: pogloska o smierci Wilhelma; Manzikert). | AI: wodz AI nie szarzuje w pierwszej minucie; wchodzi tylko w R22/R27.

**R12. Typ wroga wybiera plan (Strategikon XI):**
- wrog strzelczy pieszy ("Persowie") -> skroc czas pod ostrzalem: szybkie podejscie, zwarcie, uderzenie w bok; nie stoj;
- wrog SK ("Scytowie") -> zwarty szyk, piechota z tarczami na froncie, wlasni strzelcy piesi (dluzszy zasieg niz konni), zakaz poscigu;
- wrog z ciezka szarzujaca jazda ("Frankowie") -> nie przyjmuj szarzy w otwartym polu: teren nierowny, zwloka, zasadzka, pozorowana ucieczka, uderzenie w bok po rozbiciu ich szyku;
- wrog lekki pieszy w lesie ("Slowianie") -> nie wchodz w las, wyciagnij w pole.

### Faza 2 - nawiazanie walki i ostrzal

**R13. Przegrywasz wymiane strzelcza -> nie stoj.** JESLI wrog zadaje ostrzalem wyraznie wieksze straty niz ty jemu -> wybierz JEDNO: (a) natarcie calej linii, zeby zwarcie zaslonilo cel; (b) szarza jazdy na strzelcow; (c) cofniecie poza zasieg / za oslone terenu; (d) sciana tarcz i czekanie TYLKO jesli wrog ma ograniczony zapas strzal i brak mu sil do zwarcia. | Strategikon XI (Persowie); Leon XVIII; praktyka: Arsuf 1191 (wytrzymac w szyku, potem jedna szarza), Carrhae 53 p.n.e. (stanie = kleska, bo wrog mial doste strzal). | AI: licznik strat od pociskow na formacje w oknie 30-60 s; przekroczenie progu wymusza zmiane postawy.

**R14. Szarza jazdy na strzelcow - warunki.** Szarzuj na strzelcow GDY: sa wysunieci lub na skrzydle bez przeszkody terenowej, bez pik/ciezkiej piechoty w odleglosci wsparcia, a wroga jazda jest zwiazana, slabsza albo daleko. Uderzaj z boku/tylu, nie czolowo pod pelny ostrzal. NIE szarzuj gdy: pale/row/bloto/stok, strzelcy wtopieni miedzy spieszonych zbrojnych, wroga jazda w odwodzie czeka na kontre. | Praktyka: Bannockburn 1314 (jazda Keitha rozprasza lucznikow z flanki - sukces), Patay 1429 (sukces, zanim wbili pale), Azincourt 1415 i Crecy 1346 (czolowo, w zly teren - kleska). | AI: ocena celu = odslonietosc boku + brak przeszkody + dystans do wrogiej jazdy/pikinierow; po rozbiciu strzelcow jazda WRACA (R23), nie goni.

**R15. Najpierw zdejmij jazde wroga.** JESLI masz przewage w jezdzie -> najpierw rozbij jazde na skrzydle, potem zawroc na tyl piechoty/strzelcow (Kanny). JESLI jestes slabszy w jezdzie -> jazda nie przyjmuje walki, trzyma sie przy piechocie i kontruje dopiero jazde zmieszana po szarzy. | Frontyn II.3; Weg. III.16, III.19.

**R16. Na strzelcow konnych nie goni sie ciezka jazda.** JESLI SK nekaja -> odpowiadaj pieszymi strzelcami i krotkimi kontrolowanymi wypadami (cursores) do granicy wsparcia; strzelaj w konie; glowna linia nie rusza. | Strategikon XI; Leon XVIII; Dorylaeum 1097, Arsuf. | Porazka: wypad dalej niz ~100-150 m od wlasnej linii = otoczenie.

**R17. Uderz w zamet.** JESLI wroga formacja zmienia szyk, obraca sie, przechodzi przeszkode, miesza sie z wlasnymi uciekajacymi albo ma odsloniety bok -> atak natychmiast, najblizsza wolna formacja. | Weg. III.19, III.26 ("okazja wazy wiecej niz mestwo"); Frontyn II.4.

### Faza 3 - zwarcie i manewr

**R18. Zmiana zmeczonych.** JESLI formacja pierwszej linii stracila ~30-40% lub walczy dlugo -> podmien swiezym oddzialem z drugiej linii przez odstepy, nie czekaj az peknie. | Onasander [~22]; Strategikon II; Praecepta (jazda wraca do czworoboku piechoty odetchnac).

**R19. Luka w linii.** JESLI srodek lub skrzydlo sie cofa/przerwane -> odwod zatyka luke uderzeniem w BOK wdzierajacego sie wroga, sasiednie formacje zaginaja skrzydlo, reszta walczy dalej. | Weg. III.17, III.25; Frontyn II.8 (wodz chwyta znak i staje w luce). | AI: wyzwalacz - formacja w ucieczce albo front przesuniety w tyl o X m.

**R20. Skrzydlo peka.** JESLI skrzydlo ucieka -> (a) odwod skrzydlowy kontratakuje poscig wroga w bok; (b) jesli brak odwodu: zagnij linie (front w bok), oprzyj o teren; (c) NIE przerzucaj zwyciezajacego skrzydla - niech dokonczy i zawroci na tyl; (d) ukryj niepowodzenie przed reszta. | Weg. III.25 (kto wygral na jednym skrzydle, ma jeszcze bitwe); Frontyn II.7; Strategikon II. | AI: pekniecie skrzydla nie moze wywolac ogolnego odwrotu, dopoki srodek i odwod stoja.

**R21. Oskrzydlenie, gdy front zwiazany.** JESLI piechoty sa zwiazane walka i masz wolna jazde/odwod -> uderz w tyl lub bok, celem najpierw strzelcy i wodz, potem tyl piechoty. | Weg. III.20 (szyki 2-6); Strategikon III (oskrzydlajacy); Praecepta (klin na wodza).

**R22. Uderzenie rozstrzygajace.** JESLI wrog zuzyl odwod, a jego linia faluje (straty, czesc ucieka) -> rzuc caly odwod i straz wodza w jedno miejsce, w zwartym szyku, stepa do ostatnich metrow. | Praecepta (klin w ciszy, bez pospiechu); regula templariuszy (jedna szarza na znak). | Porazka: szarza przedwczesna na nienaruszona piechote (Courtrai 1302, Bannockburn).

### Faza 4 - poscig, pozorowana ucieczka, zasadzka

**R23. Poscig kontrolowany.** Sciga 1/3 jazdy w luznym szyku (cursores/prokoursatores); 2/3 (defensores) i piechota ida za nimi w szyku. Nikt nie rabuje do konca bitwy. Po rozbiciu wrogiej jazdy/strzelcow - zbiorka przy choragwi i powrot na pole. | Strategikon II-III, VII; Praecepta; regula templariuszy. | Porazka: Lewes 1264 (Edward gonil skrzydlo, srodek przegral), Hastings (pogon za Normanami z wzgorza).

**R24. Pozorowana ucieczka - wlasna.** UZYJ gdy: wrog stoi na mocnej pozycji i jest niezdyscyplinowany/zapalczywy, a ty masz jazde lub SK oraz ukryty/odlegly odwod. Wykonanie: krotki atak, odwrot czescia sil w strone zasadzki, na sygnal zwrot i uderzenie z bokow. | Strategikon IV ("zasadzka scytyjska"), XI; Frontyn II.5; Hastings 1066. | Porazka: wlasne wojsko bierze pozor za prawde (morale!) - tylko dla oddzialow doborowych; piechota nie udaje ucieczki.

**R25. Obrona przed pozorowana ucieczka.** JESLI wrog cofa sie nagle bez widocznych strat, w porzadku, w strone lasu/wzgorza/poza zasieg wzroku -> nie scigaj glowna linia; wyslij zwiad/cursores, utrzymaj wzgorze i szyk. JESLI ucieka w bezladzie i porzuca bron po realnych stratach -> poscig R23. | Strategikon IV, VII, XI (Scytowie); Leon XVIII; Usama (ostroznosc Frankow) [~]. | AI: rozroznienie "odwrot pozorny" = niskie straty + zwarty szyk + SK/jazda w skladzie.

**R26. Zasadzka i ukryty oddzial.** JESLI teren daje zaslone (las, garb, zabudowa) i masz jazde -> ukryj 10-20% jazdy z boku pola; uderza w tyl/bok po zwiazaniu frontu albo w scigajacych. JESLI wrog ma mniej widocznych sil niz powinien -> zaloz zasadzke, zostaw straz skrzydla. | Strategikon IV; Frontyn II.5; Onasander.

### Faza 5 - zalamanie i koniec

**R27. Zloty most.** JESLI wrog otoczony/peka -> zostaw mu droge ucieczki i bij uciekajacych; osaczony walczy do konca. | Weg. III.21; Frontyn II.6; Pizan. | AI: nie zamykac pierscienia jazda, dopoki wrog nie biegnie; wtedy najwieksze straty zadaje poscig.

**R28. Odwrot w porzadku.** JESLI stosunek sil spadl ponizej progu, a odwod zuzyty -> odwrot etapami: jazda i strzelcy oslaniaja, piechota cofa sie frontem do wroga ku wzgorzu/lasu/taborowi; wodz zbiera rozbitych przy choragwi. | Weg. III.22, III.25; regula templariuszy (zbiorka przy najblizszej choragwi; z pola dopiero gdy zadna nie stoi). | AI: zamiast powszechnej ucieczki - formacje z morale > prog oslaniaja reszte.

**R29. Sygnaly proste i nieliczne.** Jeden sygnal = jeden rozkaz (naprzod, stoj, odwrot, zbiorka); rozkaz obowiazuje formacje do nastepnego sygnalu; nikt nie zmienia zadania sam, poza ratowaniem sasiada w bezposrednim zagrozeniu. | Weg. III.5; Strategikon II-III (za duzo trab = zamet); regula templariuszy. | AI: histereza - rozkaz dla formacji nie zmienia sie czesciej niz co 15-30 s; utrata wodza/choragwi = opoznienie rozkazow i powrot formacji do ostatniego zadania.

**R30. Ocena co kilkadziesiat sekund, zmiana planu tylko na wyzwalacz.** Plan zmienia sie przy: (1) przegranej wymianie strzelczej (R13), (2) utracie/wygranej skrzydla (R20/R21), (3) ujawnieniu zasadzki lub odwodu wroga, (4) smierci wodza, (5) zuzyciu odwodu przez wroga (R22). | Weg. III.26 ("okolicznosci dyktuja rade"); Strategikon VIII. | Chroni przed miotaniem sie AI i przed sztywnym planem.

## C. Najczestsze przyczyny klesk w zrodlach (czego AI ma NIE robic)

1. Bezladny poscig jednego skrzydla (Lewes, Hastings, ostrzezenia Maurycego).
2. Brak odwodu albo uzycie go w pierwszym starciu (Weg. III.17).
3. Stanie pod ostrzalem bez odpowiedzi (Carrhae) albo szarza czolowa na przygotowanych strzelcow (Crecy, Azincourt).
4. Szarza pojedynczych oddzialow bez rozkazu (Arsuf - o wlos; Mansura 1250 - kleska; stad regula templariuszy).
5. Wodz w pierwszym szeregu - smierc konczy bitwe.
6. Zamkniecie wroga bez wyjscia - walka z rozpaczy (Weg. III.21).
7. Rozciagniecie linii, zeby dorownac dlugosci wroga - przerwanie srodka.

## D. Miejsca niepewne

- Numery rozdzialow Onasandra i ksiag Strategikonu oznaczone [~] podane z pamieci; przed cytowaniem sprawdzic (Dennis 1984; Loeb "Aeneas Tacticus, Asclepiodotus, Onasander").
- Klauzule reguly templariuszy (~161-168, wyd. Upton-Ward 1992) i liczba 5-10 straznikow choragwi - z pamieci; wyszukiwanie potwierdzilo tylko zakaz szarzy bez pozwolenia i zakaz opuszczania pola, poki stoi choragiew.
- Usama to pamietnik z anegdotami, nie doktryna; uogolnienia o Frankach sa jego opinia.
- Proporcje (1/3 cursores, 10-25% odwodu) to przyblizenia praktyki bizantynskiej/rzymskiej, nie sztywne przepisy.
- Wegecjusz III.14 o sloncu i wietrze: wyszukiwanie potwierdzilo temat rozdzialu, pelnego tekstu o kurzu nie pobrano.

## Zrodla (WebSearch)

- https://en.wikipedia.com/wiki/De_re_militari
- https://www.imperium-romana.org/de-re-militari.html
- https://facultystaff.richmond.edu/~wstevens/history331texts/Vegetius.html
- https://www.Wikipedia.org/wiki/Strategikon_of_Maurice
- https://jassa.org/?p=4619
- https://en.wikipedia.org/wiki/Praecepta_Militaria
- https://doaks.org/research/publications/books/sowing-the-dragonas-teeth-byzantine-warfare-in-the
- https://www.medievalists.net/2010/12/la-regle-du-temple-as-a-military-manual-or-how-to-deliver-a-cavalry-charge/
- https://templarsnow.blogspot.com/2020/11/the-templar-cavalry-charge.html
- https://templarhistory.com/?p=57
