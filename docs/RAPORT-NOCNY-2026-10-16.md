# Raport nocny 15/16.10.2026 (dla Jeffa)

## Najkrocej

- W grze jest **WGRANIE 63** (dokladnie co i jak - rozdz. 1 i 6). Gra sie uruchamia, autotesty przeszly, w logach Armoury **0 bledow**. Mozesz grac.
- Twoj zapis Ironman jest nietkniety (md5 bez zmian od 12:12). Kopia przed wgraniem: `D:\Backup-Bannerlord\zapisy-jeffa\IronmanjCg6MOpv28iN-2026-10-15-1212-noc-przed.sav`.
- Kazda nowa rzecz ma wlasny wylacznik w MCM (Armoury albo CrashScribe), domyslnie wlaczony. Gdyby cos Ci nie pasowalo, wylaczysz ja bez nowej wersji.
- Autotest nie gra misji: kryjowki, turnieju, Twoich bitew ani ekranu zbrojowni. Te rzeczy sprawdzisz dopiero Ty w grze. Na co patrzec - rozdz. 4.

## 1. Co zrobilem (Twoja lista po kolei)

**1. Wszystko z przekazania**
- **Drewno w kuzni dziala:** w Twojej sesji 12 kuc zuzylo 14,8 kg, a zapas spadl z 99 do 84 kg.
- **Pulapka Harmony:** sprawdzilem ok. 470 naszych latek. Zepsute bylo tylko drewno (juz naprawione). Przy kazdym starcie gra wypisuje teraz w logu, ktora latka sie nie wpiela.
- **Dlug snu AI:** naprawiony (pkt 8 nizej).
- **Armie:** zamiast 14 armii dziennie rozwiazywanych w dniu zalozenia jest 0, a armii AI jest srednio 17 zamiast 9 (pkt 10).
- **2 wsie bez tytulu BK:** dostaly tytuly: Guntak i Volug przy Omber.
- **Joffrey zyje:** Purpurowe Gody wypadna ok. 1083. dnia kampanii, czyli pod koniec roku 301.
- **Kiesa ludu:** jest w pasmie (8,8% zlota swiata).
- **Rekruci i zbrojownia:** pkt 2.
- **Kryjowka:** pkt 5.

**2. Sprzet wszystkich jednostek wedlug umiejetnosci i kultury**

Dlaczego walczyli piesciami:
- notabl kupowal rekrutowi rzecz o tier wyzej, ktorej ten nie udzwignal;
- nasza straz w bitwie zdejmowala taka rzecz, a zapasowej broni nikt juz nie dobieral.

Teraz:
- Kazdy zakup i kazde przekazanie sprzetu przechodzi jeden filtr: umiejetnosc, bron z siodla dla jezdzca i kultura. Kultury Westeros i Essos sa rozroznione, a style, ktore ROT sam daje swoim wojskom, sa dozwolone.
- To dotyczy rekrutow, poboru, weteranow z karczmy, zalog, lordow AI i Twoich ludzi.
- Rzecz nieuzywalna nie znika: notabl albo zolnierz ja sprzedaje.
- W bitwie nikt nie walczy piesciami, jesli w zbrojowni jest jakakolwiek bron.

Zbrojownia (Twoja decyzja z 15.10):
- (a) widzisz tylko swoj sprzet;
- (b) ludzie sprzedaja lup i dokupuja lepszy sprzet w miescie, a teraz takze zbedne konie;
- (c) za Twoj lepszy pancerz dostajesz stary pancerz zolnierza, ktory go nosil.

**3. Bledy z sesji**
- Ich zrodlo to pojedynek z hersztem w kryjowce: gra odsuwa wtedy wszystkich na bok jako widzow "bez druzyny", a nasz kod pytal ich, czy sa wrogami.
- Bledy "ucieczki z pola" byly z turniejow, nie z bitwy.
- Widzow pomijamy. Ranni uciekaja tylko w prawdziwych bitwach, nie w turnieju i nie na arenie.

**4. Kusze**
- Kusza kosztuje teraz 1,75 ceny luku o tej samej sile: najmocniejsza 27 -> 117 d, lekka 9 -> 23 d.
- Kuznia wymaga do mocnej kuszy mistrza.
- AI kupuje kusze wedlug sily, a nie najtansze.

**5. Zloto bandytow**
- Kazda banda z kryjowka odnosi tam codziennie 1/4 kiesy. W tescie kryjowki uzbieraly 40 -> 140 tys. zl w 40 dob.
- Po bitwie w kryjowce zobaczysz "The outlaws carried X", a po przeszukaniu "The hideout held Y".
- Kryjowka rozbita przez AI oddaje zloto zwyciezcy.

**6. Placek i marchew**
- Marchew, jablka, pomarancze i miod nie rodzily sie w zadnej wsi (gra liczyla plon BK jako zero). Teraz rodza sie we wsiach: ok. 250 marchwi i 250 jablek dziennie. Cena jablek spadla z x10 do x2,5-6,7, a marchew lezy na targach.
- Placek kosztuje dzis ok. x2 (nie x8 jak wczesniej).
- Gdy jedzenia brak, mieszczanie kupuja zamiennik, a syty lord nie przeplaca.

**7. Towarzysze**
- Cena byla zawyzona przez premie BK za umiejetnosci, ktora omijala nasze ceny. Postac ROT kosztowala ok. 14,8 tys. d.
- Teraz cena to polowa wartosci ich sprzetu plus 20 dni zoldu: mediana ok. 2 tys. d, Jaime ok. 6,3 tys.
- Umiejetnosci morskie licza sie jak zwykle role w druzynie.

**8. Oboz**
- Przy szybkim czasie gra pomijala godzine switu: w Twojej sesji 17 razy na 44 dni. Wtedy nie bylo ani kary, ani raportu, a nastepnego dnia odpoczynek liczyl sie podwojnie (stad 36,4 h).
- Atak w czasie snu zostawial mod w stanie "spisz".
- Teraz:
  - kazda godzina liczy sie raz;
  - gdy stoisz noca poza osada, kolumna sama rozbija oboz z komunikatem;
  - gdy idziesz noca, dostajesz ostrzezenie, a o switcie raport;
  - pelny sen splaca dlug;
  - AI nie dostaje juz dlugu masowo (203-423 partii naraz).

**9. Optymalizacja**
- Swaty ROT sa 4 razy szybsze (5,4 -> 1,4 ms na klatke).
- Audyt ekonomii CrashScribe i pomiary diagnostyczne ida w grze raz na 7 dni (autotest dalej codziennie).
- Armoury zabiera 5,3 ms zamiast 7,1 na klatke. Srednia klatka 104 -> 85 ms, czyli ok. +22% klatek na sekunde na mapie.
- Nasze mody to tylko ok. 7% czasu klatki. Reszta to sama gra (ruch druzyn 36%, mysl wojskowa AI 11%).

**10. Oblezenia** (sprawdzilem 20 oblezen w Twojej kampanii i testy)
- Padaly zamki, miasta nie. Z 17 oblezen bez zdobycia 11 razy wygrala odsiecz (tak ma byc).
- Naprawione:
  - wodz bral okup po jednym odpartym szturmie, choc mial przewage 7:1 - teraz czeka i szturmuje znowu;
  - armia potrafila odejsc w polowie szturmu przy 93% strat obroncow - teraz szturm trwa do konca bitwy;
  - armie BK z jedna druzyna byly kasowane w dniu zalozenia - juz nie.
- **Trebusze**: w 110 dobach Twojej kampanii nie powstal ani jeden, bo druga ekipa zaczynala je za pozno i budowala za wolno. Poprawka TB2 (trebusz od 1. doby) - rozdz. 6.

**11. Audyt ekonomii** - rozdz. 3.

## 2. Decyzje, ktore podjalem sam (mozesz poprawic)

| Decyzja | Dlaczego | Wylacznik (MCM) |
|---|---|---|
| Ranni nie uciekaja w turnieju, na arenie ani w bojce | gra sama zabrania ucieczki w turnieju | Wounded Flee Battles Only |
| Kultura sprzetu: ta sama strona Waskiego Morza albo styl, ktory ROT daje tej kulturze | doslowne id kultury jest w ROT niewykonalne (ROT uzywa kultur jako stylu) | Gear Fit (Armoury), Gear Fit Realm From Stores (CrashScribe) |
| Za Twoj lepszy pancerz dostajesz stary pancerz zolnierza (nie najgorsza wolna sztuke) | to Twoja wymiana z 15.10 | Swap Returns Their Old Kit |
| Konie ludzi w zbrojowni sa ich; zbedne sprzedaja w miescie | sa wlasnoscia zolnierzy, jak lup | Player Men Sell Spare Horses |
| Banda odklada do kryjowki 25% kiesy dziennie | przy 5 bandach ok. 1,5 tys. zl na kryjowke po 2-3 tygodniach | Hideout Daily Stash (udzial 0.25) |
| Kusza = 1,75 x luk tej samej sily (srodek Twojego 1,5-2) | najmocniejsza kusza jest o 40% silniejsza od najlepszego luku | Xbow Price Vs Bow |
| Werbunek towarzysza = 1/2 sprzetu + 20 dni zoldu | jak zaciag najemnika (10 dni x 2) | Companion Hire Days |
| Zold towarzyszy bez zmian (ok. 90 d dziennie dla postaci ROT) | ten sam zold placa lordowie AI - osobny temat | - |
| Oboz noca: nie zatrzymuje kolumny w marszu, tylko ostrzega; stojaca kolumna rozbija oboz sama | Twoj tryb zmierzchu (ask / always camp / never ask) zostaje | Night March Notice |
| Okup po odparciu tylko przy przewadze ponizej 4:1 | ten sam prog co drabiny | Siege Ransom Repel Needs Weak Edge |
| Odparcie OB2 i odsiecz OB5 bez zmian | Twoje decyzje | - |
| Straznik Harmony tylko raportuje, nie przestawia latek sam | zadna latka nie jest dzis zepsuta, a przestawianie grozilo dublami | Patch Target Guard (NIE) |
| Wegiel waga gry 5 kg; bron pali 0,5 kg z otwartej bryly | 0,5 kg za 1 d dawalo z ladunku drewna za 4 d 40 wegli za 40 d | Charcoal Weight Kg, Smith Coal Kg |
| Sol x3,5, futra x3 | sol x8 przez 4 lata (46 sztuk w 97 miastach), futer 39 dziennie przy popycie ok. 150 | Salt Rate Raised, Fur Rate Raised |
| N7 (slabszy podatek od nadwyzki ludu) WYLACZONY | w tescie 60 dob lud urosl z 8 do 10,4% zlota swiata - najpierw lud musi miec co kupic | Folk Guard No Memory (NIE) |
| Ciche logi: pomiary diagnostyczne w grze co 7 dni | koszt pomiarow, nie zapisu | Quiet Log, Economy Audit Weekly |
| Bandytow nie ruszam | to limit gry ROT, swiat dochodzi do ok. 1100 band i stoi | - |

## 3. Audyt ekonomii (Twoje pytania)

Pelny raport: `docs/AUDYT-EKONOMII-2026-10-16.md`.

- **Zloto nie znika.** W Twojej grze suma swiata stoi: 221,7 mln.
- **"Ekonomia zjada kase lordow" = zloto plynie w jedna strone, do ludu**, bo lud ma budzet na towary, ktorych nie ma. Mieszczanie nie moga wydac 105 tys. d dziennie (jaja, futra, miod, jablka, chleb, papirus), a wsie kupuja tylko 3,9% tego, co chca. Rozwiazaniem jest towar do kupienia, a nie wieksze podatki.
- **Czego za malo:** sol, owoce, miod, jaja, futra, skory, welna, ser, maslo; garum, atrament, barwniki i kamienie szlachetne nie maja w ogole producenta.
- **Czego za duzo:** ryby, drewno w zimie, bron (2580 sztuk dziennie, a zuzywa sie 135).
- **Warsztatow nie brakuje** - stoi 42% z nich. Kazde miasto ma 4 warsztaty niezaleznie od ludnosci (Oldtown 3 mln ludzi tyle samo co male miasteczko). Trzeba je inaczej rozlozyc i dowiezc surowiec (len i welna leza we wsiach).
- **Drewno 1 kg vs 100 kg:** zostawilem 100 kg w handlu. Gra zna tylko cale pensy, wiec kilogram po 0,035 d kosztowalby 1 d (x28). Kuznia liczy juz w kg. Prawdziwy blad jednostki byl w weglu - naprawiony.
- **Ceny wobec masy:** bron i zbroje sa dobre (z kg i dni pracy). Zle byly kusze (naprawione), helmy t3/t4 (naprawione) i receptury warsztatow (klejnoty x240 wsadu) - to na pozniej.
- **Tej nocy weszly pierwsze male kroki:**
  - wegiel, sol, futra, helmy;
  - bez kopalni w miastach;
  - doplata do wyrobow tylko od sprzedanych;
  - markietani biora wiecej od pelnych sakiewek - sakiewki zolnierzy rosly 140 tys. dziennie, teraz o 40% wolniej;
  - pozyczki lordow u mieszczan dzialaja.
- **Na Twoje slowo czeka nastepny krok, wiekszy:**
  - jaja z wsi;
  - len, welna i skory ze wsi do tkaczy i garbarzy;
  - typ warsztatu wedlug ceny i zysku;
  - warsztaty wedlug ludnosci miasta;
  - budowy, ktore nie stoja na deskach;
  - dwor pana jako prawdziwe zakupy;
  - opal w zimie.

## 4. Na co patrzec w grze (autotest tego nie sprawdzi)

- **Kryjowka z pojedynkiem i turniej:** po misji w logu jest linia "Druzyny w misji (NM)", a bledow ma byc 0.
- **Bitwa zaraz po werbunku we wsi:** nikt nie walczy piesciami.
- **Zbrojownia w miescie:**
  - na liscie sa tylko Twoje rzeczy;
  - wymiana oddaje Ci stary pancerz zolnierza;
  - komunikat "Your men sold ..." mowi tez o koniach.
- **Noc w marszu:** dostajesz ostrzezenie i raport o switcie, a gdy stoisz poza osada, rozbija sie oboz.
- **Kuznia:** w podpowiedzi licznika drewna jest "Open load of hardwood: N kg left", a wegiel schodzi po 0,5 kg.

## 5. Co dalej

- **Do Twojej decyzji (z audytu):**
  - wiecej wsi hodowlanych zamiast czesci rybackich;
  - wlasny dwor korony;
  - utrzymanie murow i portow;
  - jeden podatek od oszczednosci zamiast siedmiu.
- **Bandyci:** czy w pokoju bandy maja sie rozwiazywac (dzis swiat dochodzi do ok. 1100 band i tak zostaje).
- **Do kalibracji z Twojej gry:**
  - cena chleba (x4 przy zamiennikach);
  - napelnianie skarbcow kryjowek;
  - zold towarzyszy.

## 6. Wgrania i testy

| Wgranie | Kiedy | Co |
|---|---|---|
| 62 | ok. 18:50 | 8 galezi: misje, oboz, skarbiec, drobne, harmony, oblezenia, ceny, sprzet |
| 63 | ok. 19:45 | + ekonomia (pierwsze kroki audytu) + optymalizacja; N7 wylaczony |

Testy w grze tej nocy:
- baza wersji 61 z pomiarem;
- zapis testowy 15, 40, 25, 60 i 30 dob;
- nowa kampania 15 i 10 dob;
- dwa pomiary wydajnosci.

We wszystkich Armoury mialo 0 bledow. Na NIE zostaly tylko kalibracje (chleb, jablka po 25 dobach x6,7 wobec celu x6, napelnianie skarbcow) i trzy stare NIE sprzed tej nocy.

Kopie kazdej wersji: obok plikow w grze (`*.bak-2026-10-16-wgranieNN-przed`) i w `D:\Backup-Bannerlord\wgrane\`.
