# Etap 6 - rozpoznanie C: dokumenty i decyzje Jeffa (11.10.2026)

Zakres: wszystko, co dokumenty mowia o etapie 6 PLANU ("Rzemioslo, surowce i konie", pozycje 6.1-6.19 i "Skonczone, gdy"):
decyzje Jeffa z data i miejscem (plik:linia), co zdecydowane, co juz jest w grze, co nieaktualne, co Jeff odrzucil, co czeka na pytanie.
Tylko odczyt: kod, gra i zapisy nietkniete. Linie wedlug repo z 11.10 (commit ed803e7) i CHANGELOG zbioru e4\koniec (7658247).

Skroty plikow:
- PLAN = `docs/PLAN-DO-KONCA-MODA-2026-10-09.md` (etap 6: :171-197; pytania rozdz. 2: :252-262; pomysly: :264-277; zrodla: :311)
- SP = `docs/STAN-PRAC.md`; PRZ = `docs/PRZEKAZANIE-2026-10-08.md`; ES = `docs/audyt-2026-10-09/ESENCJA-AUDYTOW-2026-10-09.md`
- K13 = `docs/PLAN-K13-2026-10-07.md` (UWAGA: jego numery 127-139 to NIE numery paczek - patrz 4.2)
- P174 = `docs/PROJEKT-174-PRODUKCJA-UZBROJENIA-2026-10-09.md`; P175 = `docs/PROJEKT-175-ARMIE-2026-10-09.md`; P177 = `docs/PROJEKT-177-STAL-VALYRIANSKA-2026-10-09.md`
- LW = `docs/przekazanie-lawa-2026-10-07/PRZEKAZANIE-DLA-DRUGIEGO-CZATU.md`; LWK = `.../DIAGNOZA-NADMIARU-KONI.md`; LWA = `.../AUDYT-PRODUKCJI-I-WARSZTATOW.md`
- A02/A03/A14/A00 = `docs/audyt-2026-10-09/02-wies-produkcja-krainy.md`, `03-surowce-naprawy.md`, `14-ARMIE-KROLESTW.md`, `00-AUDYT-SWIATA-2026-10-09.md`
- A05 = `docs/AUDYT-2026-10-05-EKONOMIA-WOJNA-SUROWCE-LORDOWIE.md`; AS = starsze AUDYT-* z 04.10 (SUROWCE, PRODUKCJI-MODY/VANILLA, PELNY-*)
- RO = `docs/RAPORT-ODPOWIEDZI-2026-10-09.md`; E411/E48 = `docs/etap4/PLAN-KODU-E411.md` / `PLAN-KODU-E48.md`
- CH = `SCRATCH\e4\koniec\CHANGELOG.md` (13 449 linii; repo-owy CHANGELOG.md konczy sie na 09.10 i nie ma wpisow etapow 2-4)

---

## 0. Najwazniejsze w 12 punktach

1. **Wszystkie pytania "Ty" etapu 6 sa juz rozstrzygniete.** H (wytop tylko w nowej kampanii) - TAK (SP:845, 09.10 04:25); I/J Dorne - PIASKOWE RUMAKI,
   nie wielblady; proporcje winnic/oliwek "rzeczywiste proporcje ustal" - dobiera projekt (SP:845); K rycerz z koniem - 11.10 17:30 TAK, cel jazdy AI ok. 20%
   (SP:1190); N kuznia - "zostawic filtr" (SP:848). W PLAN:255-257 dalej stoja jako pytania - nieaktualne.
2. **6.1 "wytop + przetop" TAK (11.10)** i z decyzja H: tylko nowa kampania, a od 09.10 08:05 rzeczy NK mozna wlaczac domyslnie (SP:888-890 "bede gral, jak skonczymy
   projekt"). Projekt wytopu jest gotowy w K13:213-234 ("Iron Bloom" 15 kg za 100 kg rudy; PULAPKA `BulkScale(iron)` K13:227-229). Slowo "przetop" nie jest nigdzie
   zdefiniowane dla etapu 6 - patrz 5.1.
3. **6.14 TAK, ale tylko z prawdziwym koniem.** Zasada Jeffa 30.08 "awans na konnego = kon z taboru, warunki globalne" (A14:1698, P175:30) i 08.10 "zeby konie nie
   pojawialy sie magicznie" (SP:657) obowiazuja; audyt 14 wycofal wersje "kon z niczego" (A14:1697-1700) i dal jedyna zgodna forme: rod KUPUJE konia rycerzowi
   (stado wsi wlasnych lenn, potem targ) za zloto rodu, kon do zbrojowni jezdzca (A14:1794-1798).
4. **6.19 jest nowe (11.10 17:30), bez dokladnego cytatu** - tylko streszczenie (SP:1190-1192, PRZ:845, PLAN:195). Czesc juz istnieje: warsztaty wybieraja wyrob
   kultury miasta albo neutralny (A05:318, AUDYT-PRODUKCJI-VANILLA:489, P174:585 "Lotus Bay robi bron swojej kultury"), unikaty sa wylaczone z warsztatow od 04.10
   (CH:6251 "warsztaty ich nie robia"), szeregowi nie nosza unikatow od 16.09 (CH:7063-7092). KOLIZJA do wyjasnienia: Jeff 30.08 pozwolil GRACZOWI uczyc sie
   unikatu przez przetop i kuc go bez limitu (CH:10520-10523, :10567-10570) - "nikt nie kuje unikatow" wprost temu przeczy (5.2).
5. **Pulapka kultur ROT przy 6.19 (CLAUDE.md 2):** kultura `battania` = Polnoc, `vlandia` = Westerlands, `empire` = Braavos, `sturgia` = Zelazne Wyspy, `nord` = Lorath,
   `khuzait` = Dothrakowie, `aserai` = Dorne (DEMOGRAFIA-ANEKS:370-373); KROLESTWO `vlandia` = Zelazny Tron, `empire` = Dolina (KRYTYKA-E4:240) - wiazac przez obiekt
   kultury, nigdy przez StringId krolestwa. BK zmienia kulture osady (asymilacja, podboj) - T8 zostawil "kultura z mapy ROT - na pozniej" (CH:4007 wpis T8, Ryzyko).
6. **Konie: regula Jeffa z 07.10 jest wiazaca:** "kon ginie jak ginie, jak nie ginie to nie ginie" - tylko zabity, ocalaly kon = lup zwyciezcy, przegrani odkupuja;
   P0 pomiar TAK, P1 hodowla wedlug popytu TAK + "moze stadnin jest za duzo", P3 juczne z targu "nie ma nic za darmo"; "musza miec zapasowe konie" (LW:102-106, SP:422-424).
   Z tego w grze jest: kon jako sztuka zbrojowni (sklad7b, SP:895), kon za awans AI do zbrojowni + spis "Konie AI (175)" (CH:3725), kon wlasnoscia zolnierza (160).
   NIE ma: linii "Konie:" bilansu calego swiata (P0 LWK:79), smierci koni wojska, hodowli wedlug popytu, jucznych z targu, zapasowych koni, udzwigu 60 kg/+10 kg.
7. **Konsumpcja koni przez ludnosc zamkow/miast (ok. 55 tys. zl/dobe) czeka na decyzje** - wedlug "kon ginie jak ginie" nie powinna znikac; od niej zalezy zawor
   panow samych zamkow (CH:1770, CH:1694, PRZ:519). To jedyna sprawa koni z otwartym pytaniem do Jeffa poza 6.x.
8. **Lore wierzchowcow - jedna decyzja zastapiona:** Jeff 14.09 "wielblady tylko Dorne" (CH:7830-7832) -> 07.10 "wielblady tylko w cieplych krainach" (LW:101) ->
   09.10 "Dorne: PIASKOWE RUMAKI (nie wielblady)" (SP:845). Smoki tylko Daenerys + misja gracza, mamuty tylko Za Murem/olbrzymi, jednorozce Skagos (LW:101, CH:7830).
9. **Rzemioslo z K13 - zrobione w TOWARY 3 tylko kroki 127-130 i 139** (146 ksiega towarow, 147 rzemieslnicy BK z wsadu, 148-149 rzemioslo miasta, 150 odziez wojska,
   135-poprawka naprawy z materialem). Zostaly: 131 receptury + 132 x1.3 (= 6.3), 133 wytop (= 6.1), 134 zawor (= 6.4), 136-138 warunkowe (= 6.5). Zlom (K13 135)
   wszedl jako 174 Q4 / 174b.6 i 158 (Jeff: "zlom = przetapianie", SP:810).
10. **Kalendarz:** zrodla etapu 6 z 07.10 i pozniej licza rok 364 (K13:6, P174:13, ES:3). Na 168 dniach licza tylko dokumenty z 04.10: AUDYT-SUROWCE:196-197, :216
    (potrzeby dzienne surowcow zawyzone x2.17), BILANS-ZUZYCIA:110, HISTORIA-ZBROJE-STRATY:282-283 (rada "mnoz 168/364" - nieaktualna); A05 sam przelicza na 364
    (A05:14, :1792-1795 - "raport historia" zawyzony x2.167, rozstrzygniete).
11. **Jeff juz odrzucil (nie budowac drugi raz):** koszt naprawy wedlug godzin (projekt cedac4d; "nasza robocizna jest lepsza", SP:416-418); okienko wyboru rodzaju
    zbroi w Craft (SP:848); rekrut konny placi cene konia z targu (7a, zastapione 160 - SP:656-659); staly odsetek strat koni (LW:102); karawany ze strzalami (SP:752);
    wraki odnawiane przez kowali (26.08 "wrak max 10%" -> 07.10 "wraki ida na zlom", SP:384-386); staly mnoznik cen wojennych ("nie sztucznie", MODEL-MATERIALOW:104-107).
12. **Parametrow etapu 6 nie pytac** (Jeff 06.10 pkt 2: "Ty tworzysz ekonomie i jej sens, to co mnie pytasz", SP:471-473); pytac tylko o to, co zmienia gre Jeffa
    (kanon, wyglad targu, kuznia gracza). Poprawka bledu nie zmienia po cichu poziomu produkcji (SP:474-477). Test: zbiorczo, jeden bieg 120 dob na koniec etapu
    (SP:1150-1153; PRZ:838 "testy dopiero jak zakonczysz caly etap").

---

## 1. Decyzje Jeffa dotyczace etapu 6 (chronologicznie)

| Data, godz. | Decyzja (cytat lub skrot) | Plik:linia | Pozycja | Stan |
|---|---|---|---|---|
| 26.08 | pancerze "dokladnie taka sama mechanika jak bron ... wybor, odkrywanie, przetop i jakosc identyczne" | PLAN-kuznia-1do1.md:3-4 | tlo 6.19, 8.16 | jakosc z CRAFT jak bron - w grze (132, SP:454-455); krok 5 (popup) skreslony 09.10 (SP:848) |
| 29.08 | "Konie i pancerze tez! Caly ekwipunek!" (wymog umiejetnosci: kon = Jazda) | CH:12076-12079 | 6.14 | wiazace (SP:794-801) |
| 30.08 | "wielblady, rydwany, slonie - nie moga na nich jezdzic wojskowi np. Polnocy; karawany okej" | CH:10662-10664 | 6.9 | w grze (CrashScribe) |
| 30.08 | awans na konnego = kon z taboru, dla gracza i AI ("warunki globalne") | A14:1698, P175:30 | 6.14 | wiazace - 6.14 musi miec prawdziwego konia |
| 30.08 | gracz: "jesli pokonam bohatera i przekuje jego pancerz, to moge potem go tworzyc"; "moge kuc wiecej niz jeden, jak juz opanuje" | CH:10569-10570, :10522 | 6.19 | w grze; KOLIZJA z 6.19 "nikt nie kuje unikatow" (5.2) |
| 14.09 | "wywal mamuty - maja je tylko Wolni Ludzie i jezdza na nich giganci ... wielblady tylko Dorne; rydwany tylko armie na wschodzie" | CH:7830-7832 | 6.9 | w grze; "wielblady tylko Dorne" ZASTAPIONE 09.10 (piaskowe rumaki) |
| 15.09 | "praktycznie nigdzie nie ma koni do kupienia" -> Stajnia AI bierze najwyzej 25% polki | CH:7582 | 6.12 | w grze |
| 16.09 | "Ramsey Armour jest tylko jeden i nosi go Ramsey ... pozamieniaj na odpowiednie historyczne"; nauczony wzor tylko bohaterom, szeregowi nigdy | CH:7065, :7090 | 6.19 | w grze (UniqueLaw, RealmWard - Waskie Morze dzieli sprzet) |
| 04.10 | "wyklucz przedmioty unikatowe" -> 177 unikatow ROT: warsztaty ich nie robia, AI nie kupuje | CH:6253, ROT-UNIKATY.md | 6.19 | w grze (RotUniques) |
| 04.10 | sztabka 0,5 kg - "ustalic, ile to jest ... podniesc wydobycie kopaln"; cena wyrobu: "im surowce drozsze, tym drozszy pancerz"; staly mnoznik wojenny - "nie sztucznie" | MODEL-MATERIALOW.md:3-5, :99-107 | 6.1, 6.2 | tlo; model wytopu w K13 133 |
| 05.10 | wojsko ok. 108 tys., gospodarke dociagamy do niego; produkcja wsi stopniami x1.3/1.6/1.9 z popytem | SP:329-330 | 6.2, 6.3 | wiazace |
| 05.10 | karawany rozwoza surowce masowe (ruda, drewno, skory, len, welna) - prawdziwe partie, nie przerzut | SP:332-333 | 6.7 | w grze (103, 174.2 kontrakty) |
| 05.10 | strzaly i belty z drewna, zelaza i pracy warsztatow - "razem z wieksza liczba rak" | SP:336 | 6.2 | w grze (172) |
| 05.10 | ruda: wariant B - "ruda razem z rekami" | SP:572-574, P174:29 | 6.2 | wiazace; 174.3 dal pierwszy krok (kopalnie x1.5) |
| 06.10 | "ruda jest zle wyceniona, w miescie bez rudy powinna kosztowac krocie" | SP:280 | 6.1 | 115 cena od niedoboru - w grze |
| 06.10 pkt 1-3 | handel wedle zysku chwili; "o parametrach ekonomii decyduje projekt, nie Jeff"; poprawka bledu nie zmienia poziomu produkcji | SP:468-477 | caly etap | wiazace |
| 07.10 (lawa) | audyt produkcji "bez oliwek w sniegu", warsztaty "dobrane do surowcow okolicznych wsi (wsie z ruda -> cos z ruda w miescie)", zgodnie z lore | LW:100, LWA:3-5 | 6.9, 6.10 | T8 (17 wsi, papirus, las) w grze; bramka A3, kuznia przy kopalni B1, blad A2 - NIE |
| 07.10 (lawa) | "trzymajmy sie lore swiata": smoki tylko Daenerys + misja gracza; mamuty Za Murem/Polnoc; wielblady w cieplych krainach; jednorozce Skagos | LW:101, SP:421-422 | 6.9 | zdecyd. |
| 07.10 (lawa) | konie: P0 TAK; P1 hodowla wedle popytu TAK + "moze stadnin jest za duzo"; P2 "kon ginie jak ginie, jak nie ginie to nie ginie"; P3 juczne z targu - "nie ma nic za darmo" | LW:102, SP:422-423 | 6.11, 6.12, 6.16 | zdecyd., niezrobione (poza czescia 175/160) |
| 07.10 (lawa) | "musza miec zapasowe konie" + "zwieksza udzwig, masz konia i juki" (propozycja 60 kg / +10 kg; MarchPace czyta udzwig) | LW:103-106, SP:423-424 | 6.16 | zdecyd., niezrobione |
| 07.10 (lawa) | "konie: rekrut konny placi cene konia z targu, hodowca po cenie targu" (paczki 7a/7b) | CHANGELOG-wpis-7ab:1-2 | - | 7a ZASTAPIONE 08.10 (160) |
| 07.10 14:25 | "nasza robocizna jest lepsza" - projekt kosztu naprawy wedlug godzin NIE do wdrozenia | SP:416-418, LW:110-112 | 6.17 | ODRZUCONE (cedac4d) |
| 07.10 15:35 | "wraki ida na zlom" - kowale nigdzie nie odnawiaja wrakow; wrak = zlom do przetopu | SP:384-386 | 6.1 (przetop), 6.17 | w grze (158) |
| 07.10 | pokretla 45->35, 20->13, 5->2; zbroja z CRAFT jak bron; naprawa u kwatermistrza Spoils z materialem | SP:451-456 | 6.17, 8.16 | w grze (127, 132, 134) |
| 07.10 | spichlerz z PRAWDZIWEJ produkcji wsi (zboze, ryby, ser, mieso...) | SP:457-459 | 6.18 | E4/6 w zbiorze E4 |
| 08.10 08:00 | "po co mam placic za konia - albo najemnik ma konia ... albo przychodzi bez konia"; "system zamkniety, zeby konie nie pojawialy sie magicznie" | SP:656-659 | 6.14, 6.16 | 160 w grze; zrodlo konia = weterani (E4/10) |
| 08.10 12:15 | "jak znika to zamykamy, ma byc logiczny system ekonomii, ze wszystko z czegos wynika" | SP:723-724, P174:30 | caly etap | wiazace |
| 08/09.10 | pytania Jeffa: (5) "naprawy - minimum surowca", (6) "rozklad produkcji wedlug krain (oliwki nie na Polnocy)" | RO:12-13, CH:4096 | 6.17, 6.9 | T3 i T8 w grze; reszta = 6.9, 6.10, 6.17 |
| 09.10 00:10 | D: za Murem bez zboza -> mysliwi i rybacy | SP:772 | 6.9 | w grze (WGRANIE 7) |
| 09.10 rano | "nie" - karawany NIE handluja strzalami | SP:752 | - | w grze (172b) |
| 09.10 02:35 | rycerz z koniem - najpierw pomiar; kon za awans AI do zbrojowni; Dothrakowie konni; Wyspy Letnie ZOSTAWIC jazde; Zelazne Wyspy - jazda tylko Harlaw; sprzet wedlug tieru | SP:785-793 | 6.13, 6.14 | 175 w grze; "rycerz" - decyzja 11.10 |
| 09.10 02:50 | wymogi sprzetu zostaja ("jak nie mam umiejetnosci ... nie moge zalozyc") | SP:794-797 | 6.14 | wiazace |
| 09.10 03:45 | (3) rzemieslnicy tam, gdzie ludzie - TAK; (4) 174 Q2a/Q2b/Q3 TAK "ale sprawdzimy to"; (5) zlom = przetapianie (Q4); (6) zaopatrzenie BK gracza jedna regula; (7) dorobek startowy zostaje; (20e) konie jedza zima; (24c) smoki jedza owce | SP:804-819 | 6.2, 6.8, 6.12 | w grze (174, 174b, E4/1) |
| 09.10 04:05 | stal valyrianska: material 6 = "castle-forged steel", nikt nie wytapia nowej, jedyna droga - przekucie w Qohorze | SP:834-837 | 6.19 | w grze (177) |
| 09.10 04:25 | H wytop przy kopalni tylko w nowej kampanii - TAK; I/J Dorne PIASKOWE RUMAKI, proporcje winnic/oliwek - "rzeczywiste proporcje ustal"; K rycerz z koniem - OK (liczby po tescie) | SP:841-847 | 6.1, 6.9, 6.14 | zdecyd. |
| 09.10 04:35 | kuznia: "zostawic filtr" (bez okienka wyboru rodzaju zbroi) | SP:848 | 8.16 | rozstrzygniete |
| 09.10 07:35 | "jesli nie ma sprzetu, to nie ma sprzetu, nic nie dostaje z kosmosu" | SP:876-878 | caly etap | w grze (sklad7b) |
| 09.10 07:45 | (177-2) wlasny miecz z Qohoru - TAK, osobna paczka pozniej; (174b) ostatnia zbroja kazdego rodzaju zostaje na straganie | SP:879-882 | 6.19 (tlo) | 177-2 niezrobione; ShopKeepsLastArmour w grze |
| 09.10 08:00 | ceny hurtu: kazda sztuka po swojej cenie, bez rabatu AI | SP:885-887 | 6.13, 6.14 | w grze |
| 09.10 08:05 | "bede gral, jak skonczymy projekt" -> rzeczy NK (wytop) mozna wlaczac domyslnie | SP:888-890 | 6.1, 6.9, 6.10, 6.12 | wiazace |
| 09.10 10:35 | "wykucie strzal to powinno byc grosze, strzal bylo mega duzo" | SP:905-906 | 6.2 | w grze (sklad8-s) |
| 09.10 12:15-12:40 | unikat w taborze zostaje unikatem; "wszystko wedlug wagi" (pancerz z wagi, ladry - Jazda) | SP:919-928 | 6.19 | w grze |
| 11.10 06:50 | pasza koni wariant (a): kazdy kon po glowie caly rok | SP:1171-1173 | 6.12, 6.16 | w zbiorze 31 (w grze) - zapasowe konie zwieksza pasze |
| 11.10 13:50-14:00 | ryby/mlyny wiosek pobocznych - etap 6 razem z 6.3/6.9 | SP:1182-1183, PRZ:797-798, E411:388 | 6.18 | zdecyd. |
| 11.10 17:20 | "dokoncz etap 4, 5 i 6 ... testy dopiero jak zakonczysz caly etap" | PRZ:838-839 | caly etap | wiazace |
| 11.10 17:30 | 6.1 wytop + przetop TAK; 6.14 rycerz z wlasnym koniem, jazda AI ok. 20% (Dothrakowie konni, Zelazne Wyspy malo) TAK; NOWE 6.19 kowale kuja wedlug kultury krainy, nikt nie kuje unikatow | SP:1190-1192, PRZ:845, PLAN:177, :190, :195 | 6.1, 6.14, 6.19 | zdecyd. |

---

## 2. Pozycja po pozycji (zrodlo, co zdecydowane, co juz jest, co zostaje)

| # | Zrodlo (PLAN:311) | Co juz jest w grze / w kodzie | Co zostaje wedlug dokumentow |
|---|---|---|---|
| 6.1 wytop + przetop | K13 133 (:213-234), H (SP:845) | - (ruda 100 kg za 8 d; 41.5 miast bez rudy w 40 dobach - SP:940) | lupa "Iron Bloom" 15 kg, wegiel z lasu okregu (5 ladunkow/lupe), narzedzia 1 ruda -> 3 lupy; liczyc sztuki przez stale 100 kg (K13:227-228); tylko nowa kampania |
| 6.2 rece i kopalnie | P174 3.5 S4/S5 (:629-630), wariant B | 174.3: rece wedlug ludnosci x2, kopalnie x1.5, las x1.6 | S4 (0.10->0.15, kopalnie x2) po wytopie i zaworze; S5 (0.25, kopalnie x3-4) po S4 + K13 132 |
| 6.3 x1.3 lnu, welny, skor; wsad wedlug wartosci | K13 131-132 (:173-212) | 148-149 rzemioslo miasta, 150 odziez wojska | receptury wedle wartosci i fizyki; krok x1.3 tylko dla towaru z odbiorca |
| 6.4 mniej towaru bez zbytu | K13 134 (:235-248), A03 P1-D | - | zawor skokowy raz na pore (martwa strefa 0.5-1.5) zamiast kasowania BK 2%/d powyzej 500 szt.; P1-D dopiero po zaworze (A03:365-369) |
| 6.5 warunkowo: chleb, piwo, wino, deski; srebro; rzemioslo wsi BK | K13 136-138 (:259-285) | 147 rzemieslnicy BK z wsadu | warunki z ksiegi towarow (146) |
| 6.6 ksiega uzbrojenia wedlug zrodla | K13:98 (trzecia linia) | swiadomie pominieta w 146 (paczki/w-toku/k13-1-ksiega-towarow.md:44) | linia: warsztaty, nadwyzki lupu, komplety, paser, dom rekruta |
| 6.7 dziury po 174 | P174 rozdz. 9 (:1028-1031) | - | narzedzia wedlug zuzycia (P7), ruda z rozbitych partii ok. 16/d, uprzaz bez kupca AI, przewoz bez partii (GarrisonCarts, DailyTrade) |
| 6.8 warunkowo przy brakach | P174 rozdz. 9 (:1032-1038) | - | tarczownicy (gdy tarcze < 35%), placa miasta w cenie, strzaly x8->5, dni helmu, "dobytek osady" |
| 6.9 warsztaty wedlug surowca i lore; smoki/mamuty/wielblady; blad "strawy Polnocy" | LWA A1-A3/B1/B3 (:399-475), LW:100-101, A02 C5 | T8: 17 wsi z towarem klimatu, papirus, las (CH:4007); mamuty/rydwany/egzotyki (CH:7828, :10660); za Murem mysliwi | A2 blad `Mends.cs:2134/:2140` (Bound zamiast TradeBound) - otwarty (A02:142); A3 bramka "przerobka tylko przy surowcu"; B1 kuznia przy kopalni; B3 bez martwych kopaln BK i miodosytni; Dorne piaskowe rumaki; wielblady w Lys/Tyrosh/Myr/Volantis watpliwe (A02:350) |
| 6.10 klimat wsi takze dla warsztatow | kod: T8 i NorthernFare biegna w `OnSessionLaunched`, PO `BuildWorkshopsAtGameStart` (LWA:337-344, CH:4007) | - | tabela klimatu PRZED rozdaniem warsztatow nowej gry (LWA:403-405) |
| 6.11 konie: linia + ginie tylko zabity | A05 W7 (:859-869), LW:102 | HorseCensus "Konie AI (175)" (awanse, zakupy); kon sztuka zbrojowni (sklad7b) | linia "Konie:" calego swiata (LWK:79); smierc konia w misji i w symulacji; ocalaly kon = lup zwyciezcy |
| 6.12 hodowla wedlug popytu + 71 stadnin | LWK:81-85, LW:102 | Stajnia AI 25% polki (CH:7580) | hodowla x nasycenie targu (albo wozy nie biora koni ponad polke); przeglad 71 stadnin (20 europe, 15 desert, 13 steppe, 10 vlandian, 7 sturgian, 6 battanian) wobec lore |
| 6.13 konie po poleglych ze zbrojowni AI na targ | P175 4.1 (:553), A14 | wolne konie zbrojowni najpierw na awanse (175) | nadwyzka koni w zbrojowniach AI na targ (MenPurse dzis koni nie sprzedaje - P175:547) |
| 6.14 rycerz z wlasnym koniem, jazda ok. 20% | A14 5.2 (:1794-1798), P175 4.3 | jazda AI 12.5% d1 -> 8.1% d120 (P175:589-590; ES:53) | kon kupiony przez rod (stado wsi lenna, targ), do zbrojowni jezdzca; Dothrakowie konni, Zelazne Wyspy tylko Harlaw, Wyspy Letnie z jazda (SP:789-790) |
| 6.15 kon z lupu nie niesie piechura | AUDYT-PELNY-LOGISTYKA N6 (:247-250) | - | MarchPace liczy tylko konie bez modyfikatora; konie w zbrojowni DTE ani luzaki, ani stado |
| 6.16 juczne z targu, zapasowe konie, udzwig | LW:102-106, LWK P3 (:89-91), 3.11 (CH:1448) | 36b: wedrowcy BK bez jucznych z niczego ("wcale", CH:759) | P3 juczne karawan i taborow z polki; srednia cena konia bez smokow/mamutow (ok. 11 000 -> 1 100); udzwig; reszta 3.11 (GiveMounts/GivePackAnimals BK) i konie nowej druzyny `GiveInitialItemsToParty` (E48:48, :554) |
| 6.17 naprawy | A03 P1-B, P2-E, P2-F (:278-315) | T3 metal wedlug rodzaju (CH:4093); 135 material z targu | skora/plotno/drewno wedlug rodzaju; wlasne rece gracza ta sama regula; AI naprawia tylko to, co nosi (A00:667 - "bez pytania") |
| 6.18 wioski poboczne wedlug rodzaju | A02 A4 (:275-278), E411:394 | E4/11 VillageBook (ludzie i czesc plonu kazdej wioski) | rybacka daje ryby, mlyn zboze - wymiana w mieszance okregu, nie dodatek (ok. +3 ryb, -3 zboza na okreg z rybakami) |
| 6.19 kultura krainy | Jeff 11.10 17:30 | pula warsztatu: kultura miasta albo neutralne (A05:318); unikaty poza warsztatami (CH:6251); RealmWard (CH:7083) | regula "kazda kraina swoj rodzaj zbroi"; bez unikatow; definicja kultury krainy (5.3) |

**"Skonczone, gdy" (PLAN:197):** cos na tulowiu >= 75% zolnierzy AI (zgodne z ES:195 i 174 Q2b "tylko jesli mniej niz 75%"); skor i plotna nie ubywa szybciej niz
10/d, lnu i welny nie przybywa 3 miesiace z rzedu; kazdy znikajacy kon w linii "Konie" ma przyczyne. Bez progow dla 6.19 i 6.14 (ok. 20% jazdy) - proponuje dopisac.

---

## 3. Co Jeff odrzucil albo co zastapiono (nie odbudowywac)

| Co | Kiedy, dowod | Zamiast tego |
|---|---|---|
| koszt naprawy wedlug godzin pracy (projekt cedac4d, `PROJEKT-KOSZT-NAPRAWY`) | 07.10 14:25, SP:416-418, LW:110-112 | robocizna n131b (`MendMaterial.LaborF` x `TownWage.Index`); godziny rzemieslnikow, limit AI, rabat 30% - tylko na slowo |
| wrak max 10% wartosci (26.08) | 07.10 15:35, SP:384-386 | wraki na zlom (158) |
| rekrut konny placi cene konia z targu (7a) | 08.10 08:00, SP:656-659 | 160: kon wlasnoscia zolnierza, konny x1.5 zoldu |
| staly odsetek strat koni (pierwsze P2) | 07.10, LW:102 | tylko zabite; AI-AI z proporcji z prawdziwych bitew |
| "rycerz z wlasnym koniem" bez konia z taboru | A14:1697-1700 (wycofane przez audyt wedlug 30.08) | 6.14 tylko z koniem kupionym przez rod |
| wielblady w Dorne (14.09) | 09.10 04:25, SP:845 | piaskowe rumaki |
| okienko wyboru rodzaju zbroi w Craft (PLAN-kuznia-1do1 krok 5) | 09.10 04:35, SP:848 | filtr ForgeView |
| nauczony unikat w wojsku (furtka 30.08) | 16.09, CH:7090 | tylko bohaterowie |
| karawany handlujace strzalami | 09.10, SP:752 | 172b |
| nowa stal valyrianska z wytopu / 7. tier | 09.10 04:05, SP:834-836 | castle-forged steel; przekucie w Qohorze |
| staly mnoznik cen na wojne | 04.10, MODEL-MATERIALOW:104-107 | wskaznik oczekiwan z popytu |
| mieszczanie kupuja 100% plotna i skor (C131) | K13:16-17 (odrzucil projekt, nie Jeff) | odziez wojska (150) |
| skracanie dni pracy / "szybsi kowale" | P174:646 (decyzja projektu); A05 "szybkosc kuzni" - PLAN:263 "zapytamy tylko przy prawdziwym wyborze" | wydajnosc historyczna |

---

## 4. Nieaktualne w dokumentach

1. **PLAN rozdz. 2 H, I/J, K, N/O (:255-260)** - odpowiedzi sa w SP:841-848 i SP:1190. PLAN:257 "liczby po tescie 1.4" - Jeff zdecydowal 11.10 bez czekania (pomiar 175
   zostaje do kalibracji). Statusy etapow 1-4 w PLAN (np. 1.2 "gotowe (autotest trwa)") z 09.10 - nieaktualne; etap 6 w PLAN poprawiony 11.10.
2. **Numeracja K13:** "127-139" w K13 to kroki planu, a paczki TOWARY 3 maja te same numery z innym znaczeniem (127 pokretla, 128 Spoils, ... 160 kon - SP:631-633).
   K13 127-130 = paczki 146-150; K13 139 = poprawka 135; K13 135 (zlom) = 174 Q4 / 174b.6 / 158. P174:1024 "133 Wytop" = K13 133, nie paczka 133 (Spoils najemnicy).
3. **SP "Otwarte sprawy" (:570-588, 05.10):** pkt 1 ruda (wariant B) - przejete przez 174; pkt 2 werbunek gracza - zdecydowany 05.10 (P174:1041); pkt 3 "konie wojska moga
   wracac do DTE po smierci" - potwierdzone w A05 W7 i zamykane przez 6.11; "przetop zwraca za malo metalu" (:577) - nadal bez rozstrzygniecia (5.1).
4. **Kalendarz 168:** AUDYT-SUROWCE.md:196-197, :216 ("rok Armoury = 168 dni", potrzeby dzienne x2.17 za wysokie); BILANS-ZUZYCIA.md:110; HISTORIA-ZBROJE-STRATY.md:282-283
   (rada przeliczenia 0.46 - nieaktualna, kalendarz 364); A05:924, :1085 (straty i nabor - poza etapem 6); AUDYT-PELNY-LOGISTYKA:175-177, :245 (stare komentarze 168
   w WorldPace/Rations/WinterBite - dotyka MarchPace przy 6.15/6.16). Zrodla z 07.10+ (K13, LW, P174, P175, A02, A03, A14, ES) licza 364.
5. **14.09 "wielblady tylko Dorne"** (CH:7830) i opis `MountLaw` - sprzeczne z 09.10.
6. **CH 3.11 (CH:1448)** mial byc dopisany do PLAN "przy scalaniu" - w PLAN go nie ma; 36b zrobil tylko wedrowcow; reszta (GiveMounts/GivePackAnimals) naturalnie nalezy do 6.16.
7. **AUDYT-PRODUKCJI-MODY:223** - `ProductionProfiles` BEE (sturgia iron x1.15, battania hardwood x1.3, khuzait horse x1.5) i `ScoreCultureArmoryFlavor` BEE uzywaja
   vanillowych znaczen id kultur (AUDYT-PRODUKCJI-MODY:441) - przy 6.19 nie opierac sie na nich bez sprawdzenia, czy BEE jest w ogole czynny w tym miejscu.

---

## 5. Pytania otwarte (tylko te, ktore zmieniaja gre Jeffa)

1. **6.1 "przetop" - co dokladnie?** Mozliwe: (a) przetop gracza w kuzni po wytopie (K13:218 "1 lupa -> 30 surowek + 1 wegiel") i stara usterka "przetop zwraca za
   malo metalu" (SP:577); (b) przetop zlomu/wrakow u kowali miasta (158, 174 Q4 - juz w grze); (c) przetop jako etap lancucha lupa -> zelazo kute -> stal (MODEL-MATERIALOW:37-38).
   Rada: (a)+(c) w jednym rachunku metalu z wytopem; (b) bez zmian.
2. **6.19 a kuznia gracza:** Jeff 30.08 pozwolil sobie uczyc sie unikatu przez przetop i kuc go bez limitu (CH:10520-10523, :10567-10570); 11.10 "nikt nie kuje unikatow".
   Pytanie: zakaz tylko dla warsztatow/kowali miast i AI (rada), czy tez dla gracza (wtedy usunac nauke unikatow i ich liste w CRAFT ForgeView)?
3. **6.19 - ktora kultura jest "kultura krainy":** kultura osady wedlug BK (zmienia sie z asymilacja i podbojem; ochotnicy ida wedlug kultury NOTABLA - ERRORS A3,
   CLAUDE.md 7) czy stala kultura osady z mapy ROT (`settlements.xml`)? Rada: stala z mapy ROT (lore krainy nie zmienia sie po podboju; T8 zostawil to "na pozniej").
4. **6.19 - stroje rodow:** spis unikatow z 04.10 swiadomie NIE liczy strojow oddzialow rodow (stark_, mormont_, dayne_ ...) jako unikatow (CH:6254). Czy "Polnoc nie kuje
   zbroi Lannisterow" = rodowe wzory tylko w krainie rodu (rada: tak, kraina rodu + neutralne)?
5. **Konsumpcja koni w zamkach i miastach** (ok. 55 tys. zl/dobe w nicosc wedlug "kon ginie jak ginie") i zawor panow zamkow - "pytanie do Jeffa razem z paczka koni" (CH:1770, PRZ:519).
6. **Wierzchowce kultury za awans AI** - "czy wielblad (i inne wierzchowce kultury) ma placic za awans jezdzca AI" (CH:2092 pkt 4) - otwarte od 09.10.
7. **Zdobycz wojenna NOSZONA - wyjatek od "unikat tylko u wlasciciela"?** (CH:2092 pkt 1) - otwarte; dotyka 6.19 (kopie unikatow).
8. Bez pytania (projekt dobiera): proporcje winnic i oliwek (J), liczba stadnin, krok x1.3, kopalnie, wegiel, stawki napraw, zawor, prog jazdy 20% wedlug krain.
9. Tylko na slowo Jeffa (PLAN:275, :270, :272, A03:331-338): tarczownicy, helmy, opal kuzni, konopie, strzyza, regale gornicze, rog i sciegno do lukow, zlom jako towar
   karawan, wykup konia i zbroi w turnieju, platnerz w taborze, stada jako lup, uboj jesienia; wlasny miecz z Qohoru (177-2 - "TAK, osobna paczka pozniej", SP:880).

---

## 6. Styki z innymi etapami (z dokumentow; szczegoly - rozpoznanie D)

- **Etap 4:** `MaterialLaw.ProdPostfix` (czynnik rak L z FreeHands) mnozy plon WSZYSTKICH towarow, takze rudy (PROJEKT-ETAP4:439) - "wiecej rak i kopaln" (6.2) i x1.3 (6.3)
  nie moga go liczyc drugi raz. E4/11 VillageBook = podstawa 6.18 (E411:394). E4/1 + pasza (a): kazdy kon je caly rok (SP:1171) - zapasowe i juczne (6.16) i hodowla (6.12)
  zwieksza pasze. E4/8: konie i zywnosc `GiveInitialItemsToParty` nowej druzyny z niczego - tylko licznik, "etap 6" (E48:48, :554). E4/10 weterani = zrodlo konia najemnika (SP:658).
- **Etap 3:** 3.11 (BK `GiveMounts`/`GivePackAnimals`/`GiveFood` z niczego) - niedokonczone poza wedrowcami (CH:1448, :759); 3.10 kopalnie BK sprawdzone "nie przeplyw z niczego" (CH:1304).
- **Etap 5:** kiesa ludu dostaje "place rzemiosla 35% wartosci wyrobu" (PROJEKT-ETAP5:160, R11) - zmiana tego, co i gdzie kuja warsztaty (6.1, 6.19), przesuwa dochod ludzi miast;
  regulator kas miast A1 placi za surowiec i wyrob.
- **Etap 8 (te same pliki):** 8.3 kuznia lordow, 8.5 popyt wojenny i cena konia liczone dwa razy, 8.6 dubel lupu i koni, 8.7 zalogi nie zuzywaja sprzetu, 8.8 naprawy AI,
  8.9 kowadlo gubi welne i skory, 8.10 kon bez uprzezy, 8.13 zbroja konia liczona jak pancerz, 8.15 dochod owiec (PLAN:228-240).

## 7. Zasady Jeffa obowiazujace caly etap 6

Nic z niczego i nic w nicosc - takze towary i konie (SP:723-724, SP:657, SP:876-878); jedna regula gracz i AI (P174:33; 175; K1); jedna zasada na zjawisko; wojska nie
zmniejszamy (09.10 C, SP:766-772); lore Westeros/Essos ("trzymajmy sie lore swiata", LW:101); kazda paczka z wylacznikiem MCM; rok 364; napisy w grze po angielsku;
parametry dobiera projekt (SP:471-473); NK domyslnie wlaczone (SP:888-890); kultury ROT (CLAUDE.md 2); test zbiorczy na koniec etapu (SP:1150-1153, PRZ:838).
