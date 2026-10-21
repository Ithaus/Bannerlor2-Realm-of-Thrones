# Rozpoznanie 20.10: start od wyjazdu Roberta, lektor, wersja PL, wiecej wydarzen i filmy

Pytania Jeffa 20.10 ok. 18:35. Rozpoznanie wf_fe6a89de-d6f (4 badaczy + krytyk, tylko odczyt). Pelne odpowiedzi z dowodami: rozpoznanie-fabula-glos-jezyk-2026-10-20.json (obok).

## Pytanie 1 (Jeff): czy mozemy zaczac gre w momencie, gdy Krol Robert wyrusza do Winterfell, ile wczesniej zaczynalaby sie gra i ile wydarzen mozna zrobic przedtem?

**Krotko:** CZESCIOWO TAK. Wczesniejszy start da sie zrobic. Kalendarz i zegar fabuly sa w naszych rekach: Armoury bierze date startu od ROT, a CrashScribe/Fabula juz trzyma wydarzenia ROT na wlasnej osi czasu. Swiat ROT jest jednak zbudowany na chwile PO aresztowaniu Neda: Robert i Ned sa w danych martwi, Joffrey jest krolem, Robb rzadzi Polnoca, Renly i Stannis maja wlasne krolestwa. Prolog trzeba wiec dobudowac. Wedlug wiki fanow (AWOIAF) Robert wyrusza w 298 AC, a egzekucja Neda jest w 299 AC. Z ich szacunkow podrozy wychodzi u mnie okolo 6-8 miesiecy. W kalendarzu Jeffa (rok 364 dni) to okolo 180-230 dni gry wczesniej, czyli start pod koniec lata lub na poczatku jesieni 298 AC. Przy tempie z Twojej sesji 20.10 to okolo 8-10 godzin grania, zanim ruszy fabula ROT. Przed egzekucja Neda miesci sie okolo 15-19 kanonicznych wydarzen. Mniej wiecej polowa to same okna z tekstem (latwe), reszta zmienia swiat (srednie). Realnie polecam "lekki prolog": mapa zostaje podzielona jak w ROT, ale wszyscy zyja w pokoju, Robert i Ned zyja, a na koniec prologu swiat przechodzi w start ROT. Calosc to tor L i wymaga NOWEJ kampanii.

**Jak:** Jak to zrobic (wariant polecany, "lekki prolog"):
1) DATA STARTU: w naszym Armoury (Calendar.cs) start = data ROT minus N dni prologu (np. 210, czyli lato 298 AC). Do tego opcja w kreatorze postaci: "Start: Wyjazd Roberta (298)" albo "Egzekucja Neda (299, jak ROT)", plus przycisk "pomin prolog".
2) ZEGAR FABULY ROT: Fabula (CrashScribe) dostaje przesuniecie o N dni. Bramki 21 wydarzen, wojny fabularne (StartDay/EndDay) i progi Innych licza sie od "startu opowiesci", a nie od startu gry. Pozostale miejsca, gdzie ROT czyta date startu (lacznie 38 w 28 typach: oblezenia, zaciag, Inni przy Piesci), obejmuje jedna wspolna latka, ktora podmienia im "start" na start opowiesci. Bez tego Renly zginie, zanim Robert dojedzie do Winterfell.
3) POKOJ W PROLOGU: do konca prologu wojny fabularne ROT miedzy krolestwami Westeros sa wylaczone (takze Smocza Skala - Krainy Burzy, ktora obowiazuje od dnia 0) i AI nie wypowiada wojen. Wyjatek: Nocna Straz kontra Wolni Ludzie. Mapa zostaje podzielona jak w ROT (Bannerlord nie zna "krolestwa w krolestwie"). Robert jest glowa rodu Baratheonow z Krolewskiej Przystani, ktory w ROT rzadzi frakcja Zachodu i korony.
4) ROBERT I NED ZYJA: w danych ROT sa martwi (alive="false"). Zmieniamy to wlasnym plikiem przerobki XML, takim jakiego ROT sam uzywa (heroes.xslt), albo kodem przy nowej grze. Ned zostaje glowa Starkow, wiec Polnoca rzadzi Ned, nie Robb. Przy klasycznym starcie 299 nic sie nie zmienia.
5) SCENY: wlasne wydarzenia w oknie ROT (ten sam wyglad) i zmiany swiata:
 - Smierc Jona Arryna, wiesc na start (okno, S)
 - Wilkory znalezione przez dzieci Starkow (okno, S; w ROT nie ma wilkorow jako postaci)
 - Wyjazd Roberta z Krolewskiej Przystani: orszak jedzie po mapie (M)
 - Slub Daenerys z Khalem Drogo w Pentos (okno, S; na mapie Dany juz ma krolestwo, wiec bedzie niezgodnosc)
 - Przyjazd do Winterfell i uczta (S/M)
 - Upadek Brana (okno, S)
 - Jon, Benjen i Tyrion jada na Mur (okno S; M, jesli przestawiamy Jona)
 - Ned zostaje Reka, Sansa i Arya ruszaja z orszakiem na poludnie (M)
 - Zamach na Brana, Catelyn jedzie na poludnie (S)
 - Trydent: Lady, Nymeria, Mycah (S, mozliwy wybor gracza)
 - Ned w Krolewskiej Przystani (S/M)
 - Catelyn chwyta Tyriona, proces w Orlim Gniezdzie (S jako okna, M jako zadanie)
 - Turniej Reki w Krolewskiej Przystani przez GrandTourney, gracz moze startowac (M)
 - Jaime atakuje Neda na ulicy (S)
 - Gora pustoszy Dorzecze, Beric wyslany: wojna Zachod - Dorzecze, ktora w ROT jest na dzien 25 (M)
 - Smierc Roberta na polowaniu: Joffrey krolem (M, kluczowe)
 - Aresztowanie Neda: Ned jencem w Krolewskiej Przystani (M)
 - Robb zwoluje chorągwie, Zielone Widly, Szepczacy Las (M/L; w ksiazce przed egzekucja, w ROT wojna Polnocy rusza po niej)
 - Daenerys: smierc Viserysa, smierc Drogo i smoki (okna, S)
 Koniec prologu: rusza oryginalne okno ROT "Egzekucja Neda". Ned ginie naprawde i rod przejmuje Robb. Od tej chwili gra idzie dokladnie jak dzis.
6) TESTY: autotest prologu (200-230 dni), potem dalej az za smierc Renly'ego, zeby sprawdzic, ze wydarzenia ROT odpalaja w dobrej kolejnosci i nic sie nie zawiesza.
Wariant pelny (jedno krolestwo Roberta, rozpad na Pieciu Kroli, Jon w Winterfell, Dany bez krolestwa w Pentos): nie polecam. ROT trzyma na sztywno odwolania do krolestw i rodow (np. sprawdza, czy Krainy Burzy nie upadly), wiec ryzyko zepsucia fabuly i zapisow jest bardzo duze.

**Praca:** Calosc to tor L. Moje szacunki: (a) samo przesuniecie startu, zegara fabuly i pokoj w prologu: M, okolo 1 noc z autotestem; (b) ozywienie Roberta i Neda, smierc Roberta, areszt Neda, przejscie w start ROT: M/L, okolo 1-2 noce (najwieksze ryzyko); (c) sceny: okna z tekstem po S (1-2 godziny kazde z tekstem), sceny zmieniajace swiat po M. Wersja z 6-8 scenami to razem okolo 2-3 nocy, wersja z 15-19 scenami okolo 4-6 nocy. Do tego obrazki do scen osobno. Kazdy dlugi autotest to do okolo 80 minut (limit w CrashScribe).

**Koszty:** Kod nic nie kosztuje (nasze mody, Harmony). Jedyny realny koszt to obrazki do nowych scen. W ROT nie ma grafik Roberta, Winterfell, Brana ani turnieju, a kadrow z serialu HBO nie wolno uzyc. Trzeba wlasnych grafik (generowanych przez AI, oplata zalezy od uslugi, nie sprawdzalem cen) albo od rysownika, albo prowizorycznie uzyc 48 istniejacych obrazkow ROT (poslaniec, kupiec, Robb). Koszt czasu: tor L, kilka nocy pracy i autotestow, plus nowa kampania dla Jeffa.

**Od Jeffa:**
- Decyzja, czy chcesz nowa kampanie od 298 AC (prolog nie wejdzie do obecnej gry).
- Jak dlugi prolog: pelny, okolo 200-230 dni gry (kanon, start koniec lata lub poczatek jesieni 298 AC, okolo 8-10 godzin grania), czy krotszy, np. 120 dni (sceny gesciej)?
- Czy 'lekki prolog' ci pasuje (mapa podzielona jak w ROT, wszyscy w pokoju, Robert i Ned zyja), czy koniecznie chcesz jedno krolestwo Roberta? Odradzam to drugie: bardzo duze ryzyko.
- Ktore sceny sa dla ciebie wazne: wszystkie 15-19 czy najpierw 6-8 kluczowych (wyjazd Roberta, Winterfell, upadek Brana, Trydent, turniej Reki, smierc Roberta, areszt Neda)? Wolisz wersje z ksiazek czy z serialu?
- Zgoda na obrazki do scen generowane przez AI (albo na razie obrazki z ROT).
- Czy przy starcie ma byc wybor 'Prolog 298' albo 'Klasyczny start ROT 299'?

**Ryzyka:**
- Wymaga NOWEJ kampanii. Obecnej kampanii Jeffa (zaczetej 20.10) nie da sie cofnac do 298 AC.
- Ozywienie Roberta i Neda nie jest sprawdzone. Bannerlord nie lubi wskrzeszania martwych postaci, wiec trzeba to sprawdzic na nowej grze (wariant z przerobka XML jak w ROT albo kodem).
- Pokoj w prologu moze sie gryzc z modami, ktore same wypowiadaja wojny lub zawieraja pokoj: Diplomacy, AIInfluence (i jego latka zgodnosci z ROT), BannerKings.
- BannerKings (tytuly, dziedziczenie) i BirthAndDeath (smierc ze starosci, ciaze) moga inaczej rozegrac smierc Roberta i dziedziczenie po nim. Nie sprawdzone.
- Jesli przesuniecie zegara ominie ktores z 38 miejsc w ROT, wydarzenie albo wojna ruszy za wczesnie, juz w prologu (np. Inni pod Piescia). Potrzebny test calej osi.
- Aktualizacja ROT (dzis v8.1.8) moze zmienic nazwy i warunki wydarzen i rozjechac nasze latki. Trzeba sprawdzac po kazdej aktualizacji ROT.
- Niezgodnosci z kanonem, ktorych nie oplaca sie usuwac: Daenerys na mapie juz rzadzi krolestwem w Essos, Jon juz jest w Nocnej Strazy, Jaime, Tyrion, Ogar, Theon i Benjen to towarzysze do wynajecia, postacie sa starsze niz w ksiazce.
- NPC z AIInfluence moga mowic o 'zmarlym krolu Robercie', dopoki nie podmienimy opisow (okolo 18 opisow w encyklopedii ROT, liczone grubo).
- Prolog to okolo 8-10 godzin grania bez wojny Pieciu Kroli. Moze sie dluzyc, dlatego proponuje opcje 'pomin prolog' i krotszy wariant (np. 120 dni).

**Linki:** https://awoiaf.westeros.org/index.php/298_AC, https://awoiaf.westeros.org/index.php/Eddard_Stark, https://awoiaf.westeros.org/index.php/Years_after_Aegon%27s_Conquest/Calculations_Events, https://awoiaf.westeros.org/index.php/299_AC, https://omniatlas.com/maps/game-of-thrones/2980101/, https://www.pcgamer.com/uk/massive-game-of-thrones-mod-for-mount-and-blade-2-gets-early-access-release, C:/Users/GAME/Bannerlor2-Realm-of-Thrones/Armoury/src/Calendar.cs, C:/Users/GAME/Bannerlor2-Realm-of-Thrones/CrashScribe/src/Fabula.cs, C:/Users/GAME/Bannerlor2-Realm-of-Thrones/docs/AUDYT-KALENDARZ.md, C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/ROT-Content/ModuleData/ROT_heroes.xml, C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/pytania-tmp/dec, C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/pytania-tmp/rot-grep-start.txt

## Pytanie 2 (Jeff): czy mozemy udzwiekowic wydarzenia fabularne, ktore sie pokazuja, zeby czytal je lektor?

**Krotko:** TAK. Tekstow jest malo, wiec to robota na jeden dzien. ROT ma 29 wydarzen fabularnych i pokazuje je w 35 roznych oknach (duzy obrazek, tytul, dwa akapity, przyciski). Razem to tylko 10 308 znakow, czyli okolo 11-12 minut nagrania na jeden jezyk. Nagrania robimy raz w ElevenLabs i wkladamy do moda jako pliki. Lektor zaczyna czytac, gdy okno sie otwiera, i milknie, gdy je zamykasz (przycisk, Enter albo Esc). Gra sama umie tak odtwarzac glos na mapie kampanii, bo robi to przy wlasnych powiadomieniach z glosem. Koszt ElevenLabs: 6 USD (Starter, sam angielski) albo 22 USD (Creator, angielski i polski z zapasem na poprawki), placone raz za jeden miesiac. Pliki zajma okolo 8-35 MB na jezyk, zaleznie od formatu. Tor M. ROT.dll zostaje nietkniety, wystarczy mala latka w naszym modzie.

**Jak:** Jak to bedzie dzialac, slowami gracza: wyskakuje okno wydarzenia (np. Krwawe Gody) i od razu odzywa sie lektor. Czyta tytul i oba akapity. Kiedy klikniesz przycisk albo nacisniesz Enter lub Esc, lektor milknie. Jesli do jakiegos okna nie ma nagrania (np. ROT doda nowe wydarzenie po aktualizacji), jest po prostu cicho i gra dziala dalej.

Kroki:
1) Wyciagam 35 tekstow z ROT. W wersji polskiej najpierw tlumacze je na polski (laczy sie z pytaniem 3). Robie z nich scenariusz dla lektora i poprawiam w nim literowki ROT, np. 'assualt' albo 'knick of time', zeby lektor ich nie przeczytal.
2) Jeff slucha 3-4 probek glosu z biblioteki ElevenLabs (np. stary kronikarz albo spokojny narrator) i wybiera jeden. Moge tez wybrac glos sam, jesli Jeff woli.
3) Skrypt generuje 35 nagran na jezyk, model v3 albo Multilingual v2. Imiona w rodzaju Daenerys, Targaryen czy Harrenhal sprawdzam na sluch, a te zle wymowione poprawiam zapisem fonetycznym i generuje jeszcze raz.
4) Pliki trafiaja do naszego moda (np. ModuleData/Lektor/EN i PL), po jednym na okno. Okno rozpoznaje po numerze tekstu ROT, a nie po tresci, wiec to samo dziala po angielsku i po polsku. Dwa warianty Harrenhal maja ten sam obrazek, wiec rozroznia je wlasnie numer tekstu.
5) Mala latka w naszym modzie, bez ruszania ROT.dll. Przy otwarciu ekranu wydarzenia (EventScreen.OnActivate) odtwarza plik tym samym sposobem co gra przy swoich powiadomieniach z glosem (zdarzenie 'event:/Extra/voiceover', bez sceny). Przy zamknieciu (OnDeactivate) zatrzymuje dzwiek. Glosnosc idzie przez silnik gry, wiec obowiazuja suwaki z ustawien. Do tego wlacznik 'Lektor wl./wyl.' i opcjonalnie lekkie sciszenie muzyki na czas czytania.
6) Autotest: komenda konsoli 'rot.trigger_event' po kolei na kilku wydarzeniach. Sprawdzam, czy gra, czy milknie po Esc i Enter, ktory suwak dziala, i czy nic nie gryzie sie z VoiceActingPatch. Potem wgranie wedlug zwyklej zasady.

Format pliku: gra na pewno odtwarza .ogg (tak ma ROT). ElevenLabs nie daje .ogg, a na komputerze nie ma konwertera (sprawdzone: brak ffmpeg, sox i oggenc). Dlatego najpierw testuje .wav 24 kHz albo .mp3 prosto z ElevenLabs. Jesli gra ich nie zagra, potrzebny bedzie konwerter do .ogg, a pobranie go wymaga zgody Jeffa.

Ten sam mechanizm obsluzy tez nowe wydarzenia z pytania 4: kazdy nowy tekst to po prostu jeden plik wiecej.

**Praca:** Tor M, jeden workflow z jedna recenzja. Szacuje 4-6 godzin pracy Claude'a: teksty i scenariusz okolo 1 h, generowanie i odsluch okolo 1 h na jezyk, latka i wlacznik 2-3 h, autotest okolo 1 h. Ze strony Jeffa okolo 15 minut na probki glosu i zalozenie konta. Wersja polska dochodzi do tego samego toru, plus tlumaczenie 35 tekstow (okolo 1 h). Sama latka to jedno miejsce w kodzie i obejmuje wszystkie 35 okien.

**Koszty:** ElevenLabs placony raz, przez jeden miesiac, potem mozna zrezygnowac, a pliki zostaja do uzytku na zawsze. Sam angielski: okolo 10 300 znakow, z jedna poprawka okolo 20 000, czyli plan Starter za 6 USD (30 000 kredytow). Angielski i polski z 2-3 podejsciami do kazdego tekstu: okolo 23 000 znakow na jedna pelna wersje, razem z poprawkami 50-70 tysiecy, czyli plan Creator za 22 USD (121 000 kredytow; na stronie promocja 11 USD za pierwszy miesiac). Darmowy plan (10 000) prawie wystarcza na sam angielski bez poprawek, ale jest tylko do uzytku niekomercyjnego, z podaniem zrodla. Miejsce na dysku: nagrania ROT maja okolo 0,7 MB na minute w .ogg, wiec lektor zajmie okolo 8 MB na jezyk w .ogg albo okolo 35 MB w .wav 24 kHz. To nic przy 14 GB ROT. W grze nic nie dochodzi: zadnych polaczen z internetem i zadnych oplat za kazde odtworzenie. Inna mozliwosc: Jeff ma juz konto Fish Audio (klucz jest w ustawieniach VoiceActingPatch), wiec nagrania mozna zrobic tez tam, ale jakosci i ceny nie sprawdzalem.

**Od Jeffa:**
- Decyzja: lektor tylko po angielsku czy po angielsku i polsku (laczy sie z pytaniem 3).
- Zalozenie konta ElevenLabs i oplacenie planu (Starter 6 USD albo Creator 22 USD). Jeff robi to sam, bo Claude nie zaklada kont i nie placi. Klucz API Jeff sam zapisuje w pliku na swoim komputerze poza repo (np. w zmiennej srodowiskowej), nie wkleja go do czatu.
- Wybor glosu lektora z 3-4 probek, albo zgoda, zebym wybral sam.
- Odpowiedz, czy mod z lektorem jest tylko dla Jeffa, czy ma byc kiedys udostepniony. Od tego zalezy, czy trzeba pytac tworcow ROT o zgode.
- Gdyby gra nie zagrala .wav ani .mp3: zgoda na pobranie konwertera audio (np. ffmpeg) do zamiany na .ogg.

**Ryzyka:**
- Prawa do tekstow ROT: do prywatnej gry Jeffa nagrania sa w porzadku. Udostepnienie moda z czytanymi tekstami ROT innym ludziom wymaga zgody tworcow ROT. Ich strony z zasadami na Nexusie nie udalo sie przeczytac (blokada 403), wiec przed ewentualna publikacja trzeba zapytac.
- Licencja ElevenLabs: na darmowym planie wolno tylko niekomercyjnie i z podaniem zrodla. Ostateczne pliki trzeba wygenerowac w czasie platnego planu, wtedy wolno ich uzywac zawsze. Nie wolno klonowac glosow prawdziwych aktorow, np. z serialu; bierzemy glosy z biblioteki ElevenLabs.
- Aktualizacja ROT moze zmienic teksty, numery tekstow albo dodac wydarzenia. Wtedy dane okno bedzie bez lektora albo z nagraniem starej wersji tekstu. Gra sie nie wywroci, a brakujace nagrania sa tanie (kilka centow za wydarzenie).
- Wymowa imion z Westeros, szczegolnie u polskiego glosu. Trzeba odsluchac wszystko i poprawic zapis fonetyczny, i to wliczam w czas.
- Format pliku: .ogg jest sprawdzony, .wav i .mp3 z ElevenLabs jeszcze nie w tym miejscu gry. Jesli nie zagraja, potrzebny jest konwerter, czyli pobranie narzedzia za zgoda Jeffa.
- Ktory suwak steruje lektorem ('Glos/Voice Over' czy 'Dzwiek'), wyjdzie dopiero na autotescie.
- VoiceActingPatch odtwarza glosy poza silnikiem gry. Lektor i glos postaci moglyby sie nalozyc tylko wtedy, gdy wydarzenie wyskoczy w trakcie rozmowy. To malo prawdopodobne, ale sprawdze na autotescie.
- Teksty ROT maja bledy, np. 'Danaerys', 'assualt', 'knick of time', 'persuit'. Jesli lektor przeczyta wersje poprawiona, bedzie sie minimalnie roznic od tego, co widac na ekranie.
- Uwaga przy okazji: VoiceActingPatch wylacza 89 prawdziwych nagran lordow z ROT i zastepuje je glosem z Fish Audio. To nie jest problem lektora, ale Jeff moze o tym nie wiedziec.

**Linki:** https://elevenlabs.io/pricing, https://elevenlabs.io/docs/models, https://elevenlabs.io/docs/api-reference/text-to-speech/convert, https://elevenlabs.io/docs/help-center/product/core-capabilities/text-to-speech/how-much-does-it-cost-to-generate-using-eleven-v3-alpha.md, https://elevenlabs.io/docs/help-center/account/general/what-happens-to-my-content-after-my-subscription-ends.md, https://elevenlabs.io/docs/help-center/legal/can-i-publish-the-content-i-generate-on-the-platform/llms.txt, https://nexusmods.com/mountandblade2bannerlord/mods/3 (strona ROT, blokada 403), https://nexusmods.com/mountandblade2bannerlord/mods/6712 (mod z glosami ElevenLabs dla postaci, znany tylko z wyszukiwarki), C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/ROT-Core/GUI/Prefabs/EventXml.xml, C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/ROT-Content/ModuleData/ROTVoiceovers, C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/ROT-Content/ModuleData/module_sounds.xml, C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/VoiceActingPatchRemake/FishConfig.json, C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/pytania-tmp/popups.json, C:/Users/GAME/AppData/Local/Temp/claude/C--Users-GAME-Bannerlor2-Realm-of-Thrones/ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3/scratchpad/zb/pytania-tmp/licz_wydarzenia.py

## Pytanie 3: czy da sie zrobic wersje moda po polsku i po angielsku (wybor jezyka), przetlumaczyc wszystkie teksty na polski i udzwiekowic wszystko po polsku przez ElevenLabs?

**Krotko:** CZESCIOWO TAK. (1) Polska wersja tekstow: TAK, i to bez wlasnego przelacznika. Gra ma oficjalny polski i w Opcjach osobno wybiera sie "Jezyk" (tekst) i "Jezyk kwestii mowionych" (glos). Wystarczy dolozyc osobny modul z polskimi plikami. ROT jest do tego dobrze przygotowany: ok. 10 800 tekstow ma swoje numery (id), a tylko ok. 110 zdan jest wpisanych na sztywno w kod. Nasze mody (Armoury, RC, GT) nie maja numerow przy tekstach i trzeba je przerobic w kodzie. Praca duza (L), kilka nocy. (2) Glosy po polsku: TAK, ale "na zywo", nie nagrane z gory. Jeff juz ma mod, ktory czyta kazda kwestie postaci przez Fish Audio (Voice Acting Patch). Wysyla on do czytania ten tekst, ktory widac na ekranie, wiec po przelaczeniu gry na polski prawdopodobnie od razu bedzie mowil po polsku (do sprawdzenia jednym testem). ElevenLabs da sie podpiac zamiast Fish (praca M-L). Wedlug cennikow z sieci wyjdzie ok. 5 razy drozej za znak niz Fish, ale przy tym, ile Jeff faktycznie gra, to ok. 3 USD za 7 tygodni. (3) "Pelne udzwiekowienie wszystkiego" (menu, encyklopedia, nagrania z gory): NIE polecam. To ok. 2,9 mln znakow, ok. 230-300 USD, ok. 41 godzin nagran i ok. 2,4 GB. Do tego 41% zdan z gry ma zmienne (imiona, liczby), wiec nagranego z gory pliku nie da sie do nich dopasowac.

**Jak:** WERSJA PL/EN TEKSTOW (bez wlasnego przelacznika):
1. Robimy nowy modul "ROT_PL" (tylko pliki XML, bez kodu) z ModuleData\Languages\PL\language_data.xml (id="Polski") i plikami z polskimi tekstami ROT, BK i naszych modow. W launcherze ustawiamy go za ROT i BK. Wybor jezyka: Opcje gry -> Jezyk = Polski albo English. Wersja angielska zostaje bez zmian, bo to teksty wpisane w mody.
2. Skrypt sam zbiera wszystkie teksty z numerami: XML z ROT-Content/ROT-Map, str_english.xml oraz numery z ROT.dll, BannerKings.dll i XML BK. Prototypy liczace juz sa w scratchpad\zb\pytania-tmp\skr. Te same skrypty po aktualizacji ROT pokaza, co nowe albo zmienione.
3. Tlumacze partiami po ok. 200-300 wpisow. Zachowuje {ZMIENNE}, warunki plci {?..} i znaczniki odmiany jak w oficjalnym polskim. Imion nie tlumacze. Nazwy miejsc wedlug decyzji Jeffa (polskie z ksiazek, np. Krolewska Przystan, Koniec Burzy, Orle Gniazdo, albo angielskie). Wazne: 35 tekstow ROT, ktore dziela numer z gra, musza trafic do polskiego pliku, inaczej wyjdzie tekst o Calradii.
4. Skrypt sprawdzajacy: poprawny XML, wszystkie zmienne na miejscu, nic nie zgubione. Potem autotest gry z jezykiem polskim (menu, encyklopedia, rozmowy, okno wydarzenia ROT).
5. Nasze mody (Armoury, RC, GT, ForgeView): w kodzie kazdy widoczny napis dostaje numer {=ARM_xxx}, wlacznie z nazwami ustawien MCM. Do tego plik EN i PL. CrashScribe zostaje po angielsku (to diagnostyka). Ok. 110 sztywnych zdan w ROT.dll i ok. 450 w BK: latka Harmony albo zostaja po angielsku (to glownie ustawienia i komendy).

GLOS PO POLSKU (zalecane: na zywo, nie nagrania z gory):
A. Krok pierwszy, prawie za darmo: przelaczyc gre na polski i jednym testem sprawdzic, czy obecny mod Fish Audio czyta po polsku (powinien, bo czyta tekst z ekranu) i jak to brzmi.
B. ElevenLabs: wlasny maly modul (albo przerobka obecnego) z tym samym podpieciem pod rozmowe, tylko z wywolaniem api.elevenlabs.io. Polskie glosy dobrane do kultur i plci, kazda postac ma staly glos (jak dzis). Do tego pamiec nagran na dysku: ta sama kwestia tym samym glosem nie jest placona drugi raz. Ruch ust tak jak dzis (generowany z dzwieku).
C. Narrator dla wydarzen fabularnych ROT (okna wydarzen nie sa dzis czytane): to osobne pytanie 2. W ROT.dll teksty wydarzen maja numery, wiec mozna je nagrac z gory jednym glosem lektora (ok. 144 teksty w ROT.Events).
D. Nagranie z gory "wszystkiego": nie polecam. 41% kwestii gry ma zmienne (imiona, liczby), a gra potrafi odtworzyc z gory nagrany plik tylko dla tekstu bez zmian.

**Praca:** Tekst PL dla ROT (ok. 10 200 wpisow, 487 tys. znakow): L, ok. 3-4 noce (tlumaczenie, sprawdzanie skryptem, autotest). Tekst PL dla Banner Kings (ok. 3 200-3 500 wpisow, ok. 300 tys. znakow): M-L, ok. 1-2 noce. Nasze mody, przerobka kodu pod jezyki: Armoury L (ok. 1 260 ustawien MCM i ok. 3 000 napisow, 2 noce), RealisticCaptivity M, GrandTourney i ForgeView S, CrashScribe zostaje. Latki na sztywne teksty ROT/BK: M albo zostawiamy po angielsku. Test, czy obecny glos Fish czyta po polsku: S (1 autotest). Podmiana na ElevenLabs z pamiecia nagran: M-L (1-2 noce i testy). Narrator wydarzen nagrany z gory: M. Razem dla pelnego polskiego tekstu i glosu w rozmowach: ok. 8-10 nocy pracy, krok po kroku i po kazdym etapie wgranie po tescie. Nagranie z gory wszystkich tekstow: L+, nie polecam.

**Koszty:** Tlumaczenie: bez dodatkowych pieniedzy, robie je ja w ramach limitow sesji Jeffa. Wyjdzie ok. 1 mln znakow angielskich (ROT, BK i nasze mody), czyli kilka milionow tokenow razem ze sprawdzaniem. To zjada tygodniowy limit, dlatego rozlozyc na kilka nocy. Glos na zywo, liczony od tego, ile Jeff realnie gra (40 tys. znakow w 7 tygodni, po polsku pewnie ok. 10-20% wiecej): ElevenLabs v3 lub Multilingual ok. 3-4 USD za 7 tygodni, Flash ok. 1,6-2 USD. Plan Starter (6 USD/mies., 75 tys. znakow przez API) z zapasem wystarcza. Dla porownania obecny Fish S1 ok. 0,7 USD za to samo (15 USD za 1 mln bajtow, wedlug OpenRouter). Narrator wydarzen ROT nagrany z gory: dziesiatki tysiecy znakow, czyli kilka USD jednorazowo. Nagranie z gory WSZYSTKIEGO (oficjalny PL 1,82 mln, ROT ok. 0,55 mln po polsku, BK ok. 0,33 mln, nasze ok. 0,2 mln, razem ok. 2,9 mln znakow): ok. 230 USD przy 0,08 USD/1K albo plan Scale 299 USD na miesiac. Kazdy dodatkowy glos dla tej samej kwestii to kolejny taki koszt. Do tego ok. 41 h dzwieku, ok. 2,4 GB w mp3 (WAV wielokrotnie wiecej) i ok. 13 h generowania jedna linia naraz.

**Od Jeffa:**
- Decyzja o zakresie: (a) tylko tekst PL, (b) tekst PL i glos w rozmowach, (c) do tego narrator wydarzen fabularnych. Pelnego nagrania wszystkiego z gory nie polecam.
- Decyzja o glosie: zostajemy przy Fish Audio (juz dziala, ok. 5 razy taniej, wystarczy test po polsku) czy przechodzimy na ElevenLabs (mozliwie lepszy polski, ale nie sprawdzone odsluchem)?
- Jesli ElevenLabs: Jeff zaklada konto, wybiera plan (na gre na zywo wystarczy Starter 6 USD/mies.) i sam wkleja klucz API do pliku konfiguracji. Ja kluczy nie wpisuje.
- Budzet miesieczny na glos i zgoda na staly koszt abonamentu.
- Nazwy z Westeros: polskie z ksiazek (Krolewska Przystan, Koniec Burzy, Orle Gniazdo, Nocna Straz) czy angielskie jak w ROT?
- Czy tlumaczyc tez Banner Kings i nasze mody, czy na start tylko ROT.
- Zgoda na jeden autotest z jezykiem polskim (przelaczenie w opcjach i powrot na angielski), poza czasem, kiedy Jeff gra.
- Czy pakiet ma byc tylko dla Jeffa, czy do publikacji (wtedy zgoda tworcow ROT i licencja komercyjna na glosy).

**Ryzyka:**
- Mieszanka jezykow: po samym przelaczeniu na polski (bez naszego pakietu) gra bedzie po polsku, a ROT po angielsku. W 35 miejscach wyskoczy zly tekst o Calradii, np. biografia Yohna Royce'a pokaze opis Imperium.
- Bledy w znacznikach odmiany i plci ({?..}, {.x}, {ZMIENNE}) w tlumaczeniu wyswietla sie jako krzaczki albo zle formy. Potrzebny skrypt sprawdzajacy i test w grze.
- Kazda aktualizacja ROT lub BK dodaje albo zmienia teksty. Trzeba uruchomic skrypt porownujacy i dotlumaczyc, inaczej nowe teksty beda po angielsku.
- Jakosc maszynowego tlumaczenia dlugich opisow (ok. 390 opisow, 160 tys. znakow) i nazw z Westeros. Bez decyzji Jeffa o nazwach wyjdzie niespojnie.
- Glos: obecny mod nie zapamietuje nagran, wiec za powtarzane kwestie placi sie za kazdym razem, a kazda kwestia czeka ok. 1 s na internet. Bez internetu albo przy limicie konta postacie milkna (w logu jest 109 przypadkow 'TTS canceled/timeout').
- Akcent: glosy z puli Fish to w duzej czesci glosy angielskie. Po polsku moga brzmiec z akcentem. Trzeba odsluchac i dobrac polskie glosy.
- Prawa: nie wolno klonowac glosow aktorow z serialu ani zadnych prawdziwych osob bez zgody (regulamin ElevenLabs). Do prywatnej gry wystarczy kazdy plan. Publikowanie nagran albo tlumaczenia ROT w sieci wymaga planu z licencja komercyjna (Starter i wyzej) oraz zgody tworcow ROT (to ich teksty).
- Klucz API lezy zwyklym tekstem w pliku w folderze gry (tak jest dzis z kluczem Fish). Nie udostepniac tego folderu.
- Przerobka naszych modow pod jezyki dotyka wielu plikow (Armoury ma 132 pliki .cs). Ryzyko pomylek przy budowaniu, wiec tylko etapami z testem.
- AIInfluence: rozmowy AI po polsku zaleza od modelu AI. Jakosc polskiego niesprawdzona.

**Linki:** https://elevenlabs.io/pricing/api, https://elevenlabs.io/pricing, https://elevenlabs.io/docs/models, https://docs.fish.audio/developer-platform/models-pricing/models-overview, https://openrouter.ai/fish-audio/s1, https://www.indiedb.com/members/daedaluspl/comments, https://www.gry-online.pl/newsroom/kolejna-modyfikacja-do-bannerlorda-w-swiecie-gry-o-tron/z61fb58, C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Native\ModuleData\Languages\PL\language_data.xml, C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\ROT-Core\ModuleData\Languages\EN\str_english.xml, C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\VoiceActingPatchRemake\FishConfig.json, C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\VoiceActingPatchRemake\bin\Win64_Shipping_Client\FishPatch.log, C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\ModuleData\Languages\VoicedLines\sandbox_voiced_lines_en.xml, C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\pytania-tmp\skr (skrypty liczace: unia.py, podzial.py, wspolne_id.py, literaly.py, nasze_mody.py), C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\zb\pytania-tmp\vap_VoicePatches.cs (dekompilacja moda glosowego)

## Pytanie 4 (Jeff): czy możemy dodać więcej wydarzeń pomiędzy głównymi wydarzeniami (widziałem taki mod, "co się dzieje", też udźwiękowiony) i czy zamiast obrazka może być filmik?

**Krotko:** Wydarzenia pomiędzy głównymi: TAK. Gra i ROT mają do tego gotowe klocki, a nasze mody już pokazują takie okna. Z lektorem: TAK (nagrania przygotowane wcześniej, np. w ElevenLabs). Filmik zamiast obrazka: TAK jako film na cały ekran przed oknem wydarzenia. Gra robi to sama w kampanii War Sails (NavalDLC), w wersji 1.4.8. Filmik w ramce okna, w miejscu obrazka: CZĘŚCIOWO. Gra umie zmniejszyć film i postawić go w wybranym miejscu ekranu (tak działa tło menu głównego), ale nie ma tego nigdzie w oknie wydarzenia. Trzeba to sprawdzić próbą, około 1 dnia. Trzecia możliwość to żywa scena 3D z prawdziwymi postaciami z gry (jak ślub czy egzekucja w grze). ROT już tak robi przy egzekucji smokiem. Mod "co się dzieje, z głosem", który Jeff widział, to najpewniej AI Influence (okno World Events + głos przez Player2). Jeff ma go na dysku, ale wyłączył go 05.10.

**Jak:** 1) NOWE WYDARZENIA (podstawa). Robimy osobny mały mod, np. "Kronika Westeros", który da się włączyć i wyłączyć. W jednym pliku jest lista wydarzeń: tytuł, tekst PL i EN, obrazek, nagranie lektora, ewentualnie film, warunek i wybory ze skutkami.

Warunki mogą być takie:
- przedział dni,
- wojna między rodami,
- ktoś żyje albo nie żyje,
- "po Krwawych Godach" (odczyt, które wydarzenia ROT już były),
- gdzie jest gracz i czyim jest wasalem,
- szansa losowa.

Do tego przerwa między oknami, żeby nie zasypywały gracza (np. co najmniej 7-14 dni).

Skutki: złoto, wpływy, relacje, ludzie, przedmioty, sława albo krótkie zadanie. Wydarzenia celowo wstawiamy w dziury ROT: dni 27-100, 177-220, 357-450 i po 450.

Okno robimy własne, wyglądem jak okno ROT, ale z 2-4 wyborami. Okno ROT ma tylko 2 przyciski. Na start bierzemy gotowe obrazki ROT (posłaniec, kupiec, Wolni Ludzie itd.).

W zapisie gry trzymamy tylko listę "które wydarzenia już były". Wyłączenie moda nie psuje zapisu.

2) LEKTOR. Każde wydarzenie nagrywamy wcześniej w ElevenLabs, osobno po polsku i po angielsku. Nagranie gra, gdy otwiera się okno, i milknie, gdy okno się zamyka (puszczane tak jak VoiceActingPatch, przez Windows). Albo nagranie jest ścieżką dźwiękową filmu.

3) FILMIK, trzy sposoby:
- (a) Film na cały ekran przed oknem wydarzenia. Ten sam mechanizm, którym War Sails puszcza swój film w kampanii: tylko na mapie, Esc pomija, polskie napisy z pliku .srt. To pewne.
- (b) "Żywy obrazek" w ramce okna: krótka pętla 5-10 s ustawiona w miejscu obrazka. Najpierw próba (ok. 1 dzień); jeśli nie wyjdzie, zostaje zwykły obrazek.
- (c) Scena 3D z prawdziwymi postaciami z gry, na gotowych scenach gry (ślub, egzekucja, koronacja, śmierć), tak jak ROT robi przy egzekucji smokiem. Nowe sceny wymagają Modding Kit.

Skąd filmy:
- obrazki ROT ożywione generatorem wideo AI (obraz na film, 5-10 s),
- nagrania z samej gry,
- nigdy fragmenty serialu HBO.

Przeróbka na format gry (VP8 .ivf + .ogg + .srt) przez ffmpeg.

4) GOTOWE MODY. World Events Announcer (sezonowe podsumowanie "co się stało w świecie", jest na 1.4.7/1.4.8) można szybko przetestować obok. Random Events i Banner Flavor Events są w klimacie Calradii i dla 1.3.x, więc służą tylko jako wzór pomysłów. Nie kopiujemy ich kodu ani grafik. AI Influence robi "co się dzieje" z głosem, ale Jeff go wyłączył 05.10 (mieszał w cenach), a Player2 zwracał błąd płatności.

**Praca:** Szkielet "Kroniki" (lista wydarzeń, warunki, własne okno z 2-4 wyborami, zapis, komenda do odpalenia wydarzenia w teście, autotest): tor M, ok. 1-2 dni pracy Claude.

Jedno wydarzenie tekstowe (tekst PL i EN, warunek, skutki, sprawdzenie komendą): tor S, ok. 30-60 min. Pakiet 20-30 wydarzeń w dziury ROT to w sumie tor L (kilka dni), robiony partiami po 5-10.

Lektor: raz tor M (odtwarzanie i zatrzymanie, głośność, PL/EN), potem ok. 10-15 min na wydarzenie (wygenerowanie i przegranie do .ogg). Jeff musi odsłuchać.

Film na cały ekran: raz tor S (wzór z War Sails, kilkadziesiąt linii), potem ok. 10-20 min na film (przeróbka, napisy PL/EN), plus czas Jeffa na wygenerowanie filmu.

Ruchomy obrazek w ramce: tor M-L, najpierw próba ok. 1 dnia, wynik niepewny.

Scena 3D na gotowej scenie gry: tor S-M na wydarzenie. Nowa scena: tor L i wymaga Modding Kit.

**Koszty:** Kod: 0 zł, tylko czas i limity Claude.

Obrazki: 48 gotowych obrazków ROT za darmo. Nowe obrazki wymagają generatora obrazów; cen nie sprawdzałem.

Lektor ElevenLabs:
- Darmowy plan: 10 tys. znaków (ok. 10 min).
- Starter: $6 miesięcznie za 30 tys. znaków (ok. 30 min).
- Creator: $22 (pierwszy miesiąc $11) za 121 tys. znaków.
- Jedno wydarzenie w stylu ROT to ok. 500-1000 znaków. 30 wydarzeń w dwóch językach to ok. 30-60 tys. znaków, czyli 1 miesiąc Creator albo 2 miesiące Starter.

Filmy AI (ceny z serwisów zbiorczych, nie z oficjalnych cenników):
- Veo 3.1 Lite: ok. $0,03/s (720p, bez dźwięku).
- Kling: ok. $0,07-0,13/s.
- Runway: ok. $0,12/s.
- Z powtórkami (x3-5) 10-sekundowy klip kosztuje realnie ok. $1-6, a 20 klipów ok. $20-120.

ffmpeg i Modding Kit są darmowe.

**Od Jeffa:**
- Wybór kolejności. Proponuję: (1) wydarzenia tekstowe w stylu ROT w dziurach między głównymi, (2) lektor, (3) filmy na cały ekran do najważniejszych wydarzeń, (4) na końcu próba ruchomego obrazka w ramce.
- Jaki klimat wydarzeń: zgodne z serialem (np. ucieczka Aryi, Tyrion namiestnikiem, wieści zza Wąskiego Morza) czy losowe 'życie Westeros' (plotki, bandyci, kupcy, zarazy, prośby lordów), czy oba. Claude przygotuje listę 20-30 pomysłów, a Jeff zaznacza, co bierzemy.
- Lektor: konto ElevenLabs (darmowe na próbę albo Starter/Creator). Klucz API Jeff wpisuje do pliku ustawień sam, Claude nie wpisuje kluczy. Do tego wybór głosu polskiego i angielskiego po odsłuchu próbek.
- Filmy: zgoda na pobranie i instalację ffmpeg (darmowy program do przerabiania filmów na format gry). Konto w generatorze wideo (Kling, Runway albo Veo) opłacane przez Jeffa, albo Jeff sam dostarcza klipy. Potwierdzenie, że to tylko do prywatnej gry.
- Opcjonalnie: zgoda na instalację darmowego Modding Kit ze Steam. Potrzebny dopiero przy nowych obrazkach w oknie ROT albo nowych scenach 3D, na start nie.
- Czy chcesz na próbę gotowy World Events Announcer (sezonowe okno 'co się stało na świecie', działa na 1.4.8 według autora)? To osobne pobranie, więc też za zgodą. AI Influence zostaje wyłączony, jak zdecydowałeś 05.10.

**Ryzyka:**
- Nasze wydarzenia nie mogą ruszać zaplanowanych wojen i oblężeń ROT. AI Influence je psuło: Robb nie wypowiadał wojny, oblężenia stawały. Dlatego tylko dodatki, bez zmiany wojen ROT, i test kampanii ok. 120 dni.
- Warunki 'po wydarzeniu X' wymagają zaglądania do środka ROT (ROTEvents i lista wydarzeń są wewnętrzne). Po każdej aktualizacji ROT trzeba to sprawdzić.
- Film tylko na mapie: nie w bitwie, nie w mieście, nie w trakcie zapisu (tak robi War Sails). Inaczej grozi zawieszeniem lub złym ekranem.
- Ruchomy obrazek w ramce może migać, wyjść poza ramkę albo schować się pod tłem okna. Wtedy zostaje zwykły obrazek.
- Rozmiar plików: film na cały ekran 1080p trwający 30 s to ok. 34 MB przy jakości gry, więc 20 takich filmów to ok. 0,7 GB. Krótkie pętle 720p to kilka MB każda (szacunek).
- Filmy z AI: twarze nie będą takie jak modele postaci w ROT ani te same w różnych klipach. Dużo prób idzie do kosza (źródła mówią o wielokrotnych powtórkach).
- Prawa: żadnych fragmentów serialu HBO, głosów aktorów ani głosów udających aktorów. Teksty piszemy sami, nie przepisujemy książek ani serialu. Obrazki ROT należą do autorów ROT. Wszystko tylko do prywatnej gry, bez publikowania.
- Za częste okna męczą gracza, więc potrzebny jest limit, np. najwyżej 1 na 7-14 dni (AI Influence ma domyślnie co 7 dni).
- Zapis gry: tylko proste dane (lista odhaczonych wydarzeń), z pamięcią o limicie 32 KB na napis w zapisie.

**Linki:** https://www.nexusmods.com/mountandblade2bannerlord/mods/1410, https://github.com/Phill544/MnBRandomEvents, https://www.nexusmods.com/mountandblade2bannerlord/mods/9137, https://www.nexusmods.com/mountandblade2bannerlord/mods/12730, https://www.nexusmods.com/mountandblade2bannerlord/mods/9907, https://www.nexusmods.com/mountandblade2bannerlord/mods/12254, https://steamcommunity.com/app/261550/discussions/0/3482997773484590809, https://steamcommunity.com/sharedfiles/filedetails/?id=3584621421, https://www.nexusmods.com/mountandblade2bannerlord/mods/12123, https://www.nexusmods.com/mountandblade2bannerlord/mods/12048, https://www.nexusmods.com/mountandblade2bannerlord/mods/6712, https://elevenlabs.io/pricing, https://hackernoon.com/the-same-10-seconds-of-ai-video-can-cost-$075-or-$4-track-which-model-is-burning-your-budget, https://echai.ventures/how-founders-use-ai/video-image-audio/what-does-ai-video-and-image-actually-cost-and-what-s-a-sensible-starter-stack-on-a-founder-budget, https://forum.zdoom.org/viewtopic.php?p=1257760

## Krytyk (poprawki i braki)

poprawka: Starter za 6 USD wystarczy na lektora po angielsku i po polsku, a nie tylko po angielsku. Angielskie teksty wydarzen to 10 308 znakow, polskie wyjda okolo 11-12 tys. (polski jest zwykle 10-20% dluzszy, to szacunek). Razem okolo 22 tys., a Starter daje 30 000 kredytow. Zostaje okolo 8 tys. na poprawki wymowy. Creator potrzebny jest tylko przy wielu powtorkach. Jesli Creator, to pierwszy miesiac kosztuje 11 USD, a nie 22: tak napisano w samej odpowiedzi, ale do kosztu wpisano 22. Abonament odnawia sie sam co miesiac, wiec po nagraniu trzeba go anulowac.
dowod: https://elevenlabs.io/pricing (czytane dzis): Free 10k, Starter 6 USD / 30k, Creator "$22 First month 50% off" / "$11 per month" (kolejnosc napisow na stronie niejasna, najpewniej 22 USD to cena normalna, a 11 USD to pierwszy miesiac), Pro 99 USD / 600k. Plan Free nie ma licencji komercyjnej. Wymogu podania zrodla na stronie cennika nie ma, to twierdzenie nie jest tam potwierdzone. Wav/PCM 44,1 kHz jest tylko w Pro. Model v3 kosztuje 1 kredyt za znak na stronie www, a przez API mniej: https://help.elevenlabs.io/hc/en-us/articles/35869113958801-How-much-does-it-cost-to-generate-using-Eleven-v3

poprawka: Konwerter do .ogg juz jest na dysku. Nie trzeba niczego pobierac ani pytac o zgode. AIInfluence ma przy sobie OggVorbisEncoder.dll (biblioteka .NET), ktorej uzywa do zamiany glosu na .ogg przed odtworzeniem w grze. To tez wskazowka, ze .ogg to sprawdzona droga, a .wav i .mp3 przez CreateEventFromExternalFile nadal nie sa sprawdzone. Naszym narzedziem mozemy zamieniac pliki ElevenLabs (pcm/wav) na .ogg. Licencji tej biblioteki nie sprawdzalem.
dowod: Modules/AIInfluence/bin/Win64_Shipping_Client/OggVorbisEncoder.dll (895 488 B). AIInfluence.dll odwoluje sie do OggVorbisEncoder, VorbisInfo i ProcessingState (to nazwy z tej biblioteki). Kodu AIInfluence nie da sie rozlozyc dekompilatorem, bo jest zaciemniony (BadImageFormatException), wiec dowod jest posredni. ffmpeg, sox i oggenc rzeczywiscie nie ma, a w Pythonie jest tylko numpy.

poprawka: "Oficjalny polski" w grze to tylko napisy, bez polskiego glosu. Polskie pliki (pelne, "Polski") maja Native, SandBox, SandBoxCore, StoryMode, CustomBattle i NavalDLC. Nagrane glosy sa tylko po angielsku. Filmy maja polskie napisy, ale angielski dzwiek. ROT ma tylko angielski.
dowod: Modules/Native/ModuleData/Languages/PL/language_data.xml (id="Polski", under_development="false"). SandBox, StoryMode i NavalDLC: w ModuleData/Languages/VoicedLines jest tylko folder EN. SandBox/Videos/CampaignIntro/campaign_intro_pl.srt obok jednego campaign_intro.ogg. ROT-Core/ModuleData/Languages ma tylko EN (8845 wpisow, 424 491 znakow, 7792 rozne teksty, 397 859 znakow). Spolszczenia ROT w sieci nie znalazlem (wyszukiwarka: brak wynikow, Nexusa i Discorda ROT nie przegladalem).

poprawka (nowe ryzyko do pytania 3): Po samym przelaczeniu gry na polski czesc tekstow ROT pokaze cudze polskie teksty z Calradii. ROT uzywa 259 numerow tekstow z gry podstawowej (okolo 15 400 znakow), a gra ma do nich wlasne polskie tlumaczenia. Przyklady: opis Renly'ego dostanie polski opis Apisa Warosa, opis Yohna Royce'a dostanie tekst o ziemiach Cesarstwa, a "Hideout" zmieni sie w "Kryjowka". Polska paczka ROT musi wiec nadpisac takze te 259 numerow i wczytac sie po grze podstawowej.
dowod: skrypt porownujacy ROT-Core/.../EN/str_english.xml z plikami Modules/*/ModuleData/Languages/PL. ROT-Content/ModuleData/ROT_heroes.xml ma text="{=xL95EfpQ}Lord of Storm's End...", a ten sam numer jest w SandBox/ModuleData/heroes.xml i w polskich plikach. Rozklad: std_common_strings 128, std_TaleWorlds_CampaignSystem 96, std_heroes 16.

poprawka: Gra umie wideo, ale tylko na caly ekran albo jako tlo pod menu. Wideo w ramce okna wydarzenia nie jest nigdzie pokazane i jest niesprawdzone. Format to .ivf (VP8, 1080p, 30 klatek), dzwiek osobno w .ogg, napisy w .srt dla kazdego jezyka. Przyklad: campaign_intro.ivf ma 174 MB na 152 s, czyli okolo 70 MB na minute. Silnik ma funkcje skalowania, przesuwania i wyswietlania obrazu na teksturze (SetScale, SetOffset, SetRenderTarget), wiec ramka moze byc mozliwa, ale trzeba to przetestowac. Pewny wariant: filmik na caly ekran przed oknem wydarzenia. Do zrobienia .ivf potrzebny jest koder VP8 (np. ffmpeg). Na dysku go nie ma, wiec pobranie wymaga zgody Jeffa.
dowod: TaleWorlds.Engine.VideoPlayerView.PlayVideo(video, sound, framerate, looping). TaleWorlds.MountAndBlade.View.Screens.VideoPlaybackScreen (osobny ekran, SetRenderOrder(-10000), pomijanie klawiszem). MBInitialScreenBase.RefreshScene: Videos/initial_menu/*_pc.ivf plus .ogg pod menu glownym. Naglowek campaign_intro.ivf: DKIF, VP80, 1920x1080, 30 kl./s, 4569 klatek. ROT nie ma folderu Videos.

poprawka (drobna, pytanie 1): Na liscie progow dni ROT brakuje dwoch: Days(180) w BlackwaterSiegeEvent i Days(267) w WallSiegeEvent. Reszta (data startu Years(299)+Hours(6), Ned na dzien 2, Robert i Ned alive="false", 1 wojna w XML, 38 odczytow daty startu) sie zgadza.
dowod: ROT.dll, ROT.Models.ROTCampaignTimeModel (tez WeeksInSeason=3, czyli ROT sam ma rok 84 dni). Zliczenie CampaignTime.Days(...) w rot-events-all.cs. ROT_heroes.xml l.380 i 473. ROT_spkingdoms.xml: 1x isAtWar="true".

potwierdzone bez zmian: Patch_DisableVoiceOverModels w VoiceActingPatchRemake wylacza bez warunku DefaultVoiceOverModel i ROTVoiceOverModel (Prefix ustawia wynik na null i zwraca false), wiec 89 nagranych kwestii ROT nie gra. Glos z pliku na mapie dziala tak jak w SandBoxGauntletGameNotification (CreateEventFromExternalFile("event:/Extra/voiceover"...)). Jest 29 klas wydarzen i 0 zmiennych w tekstach (SetTextVariable), wiec nagrania na stale pasuja do kazdego okna.

brakuje: Odpowiedzi 3 i 4 doszly do mnie uciete. Odpowiedz 2 urywa sie w polowie akapitu o formacie pliku, a 3 i 4 nie doszly wcale. Sprawdzilem ich tematy samodzielnie, ale ich tresci nie zweryfikowalem.

brakuje (pytanie 3): ile realnie tekstu jest do tlumaczenia i do nagrania. Mody bez gry podstawowej to okolo 840 tys. znakow: ROT 424 tys., BannerKings 290 tys. (bez PL), Diplomacy 32 tys. (ma juz PL), Spoils of War 22 tys., TournamentsXPanded 21 tys., BetterEconomy 19 tys., RBM 9 tys. Gra podstawowa po angielsku to okolo 1,6 mln znakow (SandBox 960 tys., NavalDLC 274 tys., Native 233 tys., StoryMode 95 tys.). Nagranie "wszystkiego" to gorna granica okolo 2,4 mln znakow. W ElevenLabs to kilka miesiecy planu Pro (600 tys. za 99 USD, razem okolo 400 USD) albo Scale (1,8 mln za 299 USD) i to wciaz za malo na jeden miesiac. Czesc tekstow to menu i encyklopedia, a nie dialogi, wiec trzeba wybrac, co czytac. Teksty ze zmiennymi (imiona, liczby) i rozmowy AIInfluence powstaja w trakcie gry, wiec nie da sie ich nagrac z gory. Potrzebny bylby glos na zywo, placony za kazde uzycie. Nasze mody (Armoury, CrashScribe, GrandTourney, RealisticCaptivity, ForgeView) nie maja folderu Languages, wiec ich teksty siedza w kodzie i trzeba je osobno przeniesc do plikow jezykowych.

brakuje (pytanie 3): tansza droga dla rozmow na zywo. Zainstalowany Voice Acting Patch (Fish Audio, model s1) juz czyta rozmowy z postaciami, a s1 obsluguje polski. Po spolszczeniu tekstow rozmow moze je czytac po polsku bez ElevenLabs. Wymaga to tylko polskiego glosu w FishConfig.json. Jakosci po polsku nie sprawdzalem.
dowod: https://huggingface.co/fishaudio/s1-mini, https://docs.fish.audio/developer-guide/models-pricing/models-overview (13 jezykow, w tym pl). Plik Modules/VoiceActingPatchRemake/FishConfig.json.

brakuje (pytanie 4): nie wiadomo, jaki "mod z udzwiekowionymi wydarzeniami" widzial Jeff. W wyszukiwaniu nie znalazlem moda do Bannerlorda czy ROT z czytanymi wydarzeniami fabularnymi (byly tylko np. WarbandQuotes https://www.nexusmods.com/mountandblade2bannerlord/mods/62/ i Sound The Alarm https://www.nexusmods.com/mountandblade2bannerlord/mods/306). Trzeba poprosic Jeffa o nazwe albo link.

brakuje (pytanie 4): skad wziac filmiki. Kadrow z serialu HBO nie wolno uzyc. Cen generatorow wideo AI nie sprawdzalem. Trzeba tez policzyc rozmiar: okolo 70 MB na minute przy 1080p VP8.

Plikow nie zapisywalem, wszystko sprawdzalem tylko odczytem.
