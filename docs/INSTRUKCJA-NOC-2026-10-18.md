# Przewodnik gracza: co doszlo w nocy 17/18.10

Dla Jeffa. Opisuje wszystko, co tej nocy doszlo do Armoury i CrashScribe, tak jak to widzisz w grze.

**Oznaczenia:**
- **[78]** - juz masz w grze (WGRANIE 78, 18.10 ok. 09:17).
- **[79]** - gotowe w kodzie, ale jeszcze NIE w grze. Wejdzie jako WGRANIE 79 dopiero po testach: 160 dni Twojej sciezki, bieg zimy, potem 5 dni z bitwa. Wgram je tylko wtedy, gdy nie grasz.

**Przelaczniki:** prawie wszystkie sa w MCM -> "The Armoury" -> w grupie podanej przy kazdym systemie. Wyjatkiem sa wydarzenia ROT (plik CrashScribe). Zadnego nowego klucza nie ma w Twoim `Armoury.json`, wiec dzialaja wartosci domyslne z kodu. Jesli cos zmienisz w MCM, MCM zapisze to w `Armoury.json` - wtedy powiedz mi, co zmieniles. Wylaczenie zwykle dziala od nastepnego dnia gry i niczego nie psuje w zapisie. Wyjatki oznaczylem **UWAGA**. Dopisek "(z KS)" znaczy, ze system rusza dopiero po wlaczeniu nowej ekonomii.

---

## 1. Swiat

**Zajazdy przy szlakach [78]**
- **Co robi:** przy drogach miedzy miastami i zamkami stoi 196 zajazdow. Gesto tam, gdzie mieszka duzo ludzi, rzadko na pustkowiach, za Murem wcale.
  - Po najechaniu myszka widzisz nazwe (np. "The Laughing Kettle", "Crossroads Inn", "Caravanserai of the Silver Camel"), czyje to ziemie, jakie lozka i ceny.
  - Klik na zajazd = druzyna jedzie i wchodzi. Gdy staniesz tuz obok, gra pyta "Go in" / "Ride on".
  - Nocleg kosztuje tyle co w karczmie miasta: wspolna izba 1/1/3, porzadna karczma 2/2/8, dobry dom 4/3/20 (czlowiek / kon / Twoj pokoj). Dobry dom jest tylko w 3 zajazdach.
  - Ludzie placa za siebie, Ty za siebie i za konie bez jezdzca. Pieniadze dostaje kiesa wsi, na ktorej ziemi stoi zajazd, a pan bierze swoja czesc.
  - Spisz w namiocie obok i budzisz sie wypoczety do zmierzchu.
  - Zajazd jest zamkniety na ziemiach wroga ("The keeper bars the door to the enemies of his lord.") i w czasie najazdu ("Raiders are on these lands - the inn is barred and the keeper hides.").
- **MCM:** grupa "A night's rest" -> **"Roadside Inns"**, domyslnie ON. Nocleg wymaga tez "Town Inns" i "Night Rest Enabled".
- **Po wylaczeniu:** zajazdow nie ma. Bezpieczne, w zapisie nie zostaje slad.

**Plotki [78]**
- **Co robi:** opcja **"Listen to the gossip"** w zajezdzie i w karczmie miasta, za darmo.
  - Plotki biora sie z prawdziwego stanu swiata: bandy w okolicy (ilu ich, od kiedy rabuja, ile maja zlota), kryjowki i skarb w nich, wojska panow, karawany, ceny w pobliskich miastach, wiesci z wojen.
  - Im dalej i im starsza wiesc, tym mniej dokladna ("hundreds of men", "two days' ride to the north-east").
  - Herszt bandy ma stale przezwisko (np. "Bald Jon").
- **MCM:** "A night's rest" -> **"Inn Gossip"**, ON.
- **Po wylaczeniu:** opcja znika. Bezpieczne.

**Drogi ROT +15% [78]**
- **Co robi:** kazda partia ladowa idzie o 15% szybciej po namalowanej drodze ROT - Ty, lordowie, karawany, wozy, bandy. Sufit predkosci kolumny marszowej rosnie o tyle samo. Statki bez zmian.
  - W podpowiedzi predkosci jest linia **"Road"**.
  - AI rzadko trzyma sie drog (ok. 5% marszu). Zyskujesz glownie Ty, gdy klikasz punkty wzdluz traktu.
- **MCM:** "The marching column" -> **"Map Road Speed Bonus"**, ON. Obok "Map Road Speed Percent" 15 (najwyzej 50) i "Map Road Margin" 0.40.
- **Po wylaczeniu:** bezpieczne. **UWAGA:** jesli gra wystartuje z tym przelacznikiem wylaczonym, ponowne wlaczenie zadziala dopiero po restarcie gry.

**Przysiolki na pustkowiach (uklad wiosek v6) [78]**
- **Co robi:** 215 wiosek z najciasniejszych skupisk przenioslo sie na puste ziemie swojego okregu jako male przysiolki (1 250 - 2 000 ludzi): pasterze, drwale, weglarze, traperzy, rybacy.
  - W kazdym okregu jest tyle samo wiosek i ludzi co przedtem.
  - W Westeros przysiolki maja nowe nazwy (Woolfold, Goatfell, Cinderhurst, Shell Point...).
  - W dymku "Hear more" jest ich zajecie i historia, a w menu okregu miejsce ("in the hills", "deep in the forest", "out on the steppe").
  - Twoj zapis przechodzi sam: spalone wioski, puste domy i pamiec wojny ida za wioska.
- **MCM:** "The map and its crossings (stage 7)" -> **"Village Layout V6"**, ON, oraz "Village Hamlet Ks Land", ON.
- **Po wylaczeniu:** wraca stary uklad v5 przy nastepnym wczytaniu. **UWAGA:** gdy nowa ekonomia (KS) juz dziala, nie przelaczaj zadnego z tych dwoch - KS zgubi swoj stan i zacznie liczyc od nowa.

**Herby wiosek [78]**
- **Co robi:** proporczyki wiosek uzywaja mniejszych obrazkow, a dalekie sa zwalniane z pamieci karty graficznej.
  - Po dlugiej jezdzie kazda wioska dalej ma herb swojego rodu. Dotad po ok. 160 herbach nowe wioski dostawaly herb pana.
  - Na mapie wygladaja tak samo.
- **MCM:** "The map and its crossings (stage 7)" -> **"Village Pennant Small Texture"**, ON.
- **Po wylaczeniu:** wracaja stare duze obrazki i stary limit. Bezpieczne.

## 2. Ludzie i wojna

**Uchodzcy z najazdu w miastach [78]; limit, ktory naprawde dziala [79] (z KS)**
- **Co robi:** ludzie wypedzeni przez najazd ida do najblizszego miasta swojego krolestwa. Jesli go nie ma, ida do miasta krolestwa, ktore z nimi nie walczy.
  - W miescie sa biedota: pracuja za dniowke, kupuja chleb na targu i przynosza swoja czesc kiesy wsi.
  - Wracaja do domu po 30 dniach spokoju w regionie.
  - Gdy ich miasto jest oblegane, siedza w murach i gloduja razem z mieszczanami.
  - Miasto przyjmuje ich dopiero, gdy jego ludzie kupuja pelny koszyk KS. Do tego czasu siedza w swoim regionie i jedza ze spichlerzy, tak jak dzis.
  - Limit: miasto przyjmuje dziennie najwyzej 1 osobe na 100 swoich mieszkancow. W [78] limit przecieka (Sharp Point urosl w tescie z 9 tys. do 73 tys. w tydzien). W [79] liczy wszystkich przybylych danego dnia, reszta czeka w regionie.
  - Gdy w jeden dzien do Twojego miasta (albo miasta, w ktorym stoisz) przyjdzie co najmniej 500 uchodzcow, dostajesz komunikat: "N refugees from the burnt lands around X have reached Y and found shelter among its labourers. They will go home when their land is quiet again."
  - W oknie "People of this land" jest linia "Refugees from raided lands living in this town: N".
- **MCM:** grupa "Iron Bank" -> **"Raid Refugees To Towns"**, ON; "Raid Refugees Town Intake" 0.01; "Raid Refugees Message Min" 500. W [79] dochodza **"Raid Refugees Intake Meter"**, ON, oraz "Famine Shared Village Refugees", ON.
- **Po wylaczeniu:** od nastepnego dnia jak dawniej - uchodzcy zostaja w regionie, a ich pieniadze wracaja do kies wsi. **UWAGA:** gdy wielu czeka na wpuszczenie, nie cofaj DLL do starszej wersji, bo stara wpusci wszystkich jednego dnia. Zamiast tego wylacz przelacznik.

**Glod w miescie -> niepokoj -> bunt [78] (z KS)**
- **Co robi:** gdy na targu jest mniej jedzenia niz mieszczanie zjadaja w jeden dzien albo spichlerz jest pusty, spada lojalnosc i bezpieczenstwo.
  - W podpowiedziach lojalnosci i bezpieczenstwa pojawia sie pozycja **"Hunger"**. Im wiecej ludzi bez jedzenia, tym mocniej spada. Do 10% braku nic sie nie dzieje.
  - Bunt robi sama gra, nie dodalismy nowego.
  - Premia "Well fed populace" nie dziala, gdy ludzie gloduja.
  - Pan miasta doplaca kupcom za jedzenie, ktore naprawde przywioza - tylko ze zlota ponad rezerwe wojenna. Twoja kiesa nigdy nie jest uzywana.
  - [79]: pan splaca rachunki za jedzenie swoich miast, gdy maja 20 dni, zanim zablokuja dowoz.
- **MCM:** "Hunger and the town lord (KS round 4)" -> **"Ks Hunger Unrest"**, ON; "Ks Hunger Tolerance" 0.1; **"Ks Lord Funds Food"**, ON.
- **Po wylaczeniu:** dzialaja tylko zasady jedzenia z samej gry. Bezpieczne.

**Mur i Wolni Ludzie [78]; poprawki [79] (z KS)**
- **Co robi:**
  - Lordowie Polnocy oddaja Nocnej Strazy zboze w naturze, wozami. Biora tylko to, co maja ponad rok zapasu, nigdy z glodnej wioski i nigdy z Twojego rodu.
  - Glodni Wolni Ludzie wysylaja przez Mur na poludnie bandy po 12-40 ludzi, najwyzej 6 naraz. Na mapie to bandy "Wildling Renegades" - mozesz je atakowac. To prawdziwi ludzie z wiosek. Po 30 dniach wracaja do domu z lupem.
  - [79]: Straz dostaje cale zboze, ktorego jej brakuje. Wolni Ludzie nie bija juz wlasnych band. Banda wspina sie na Mur z dala od wiez, ale w Darze moze ja dopasc Straz albo Polnoc.
- **MCM:** "The Wall: grain tribute and Free Folk hunger (r4-mur, 17.10)" -> **"Wall Grain Tribute"**, ON, oraz **"Free Folk Hunger Raids"**, ON. W [79] grupa "The Wall: fixes after the first test (fix-mur, 18.10)" - 3 przelaczniki, ON.
- **Po wylaczeniu:** nie ma nowych danin ani najazdow. Wozy, ktore juz jada, dojezdzaja, a bandy wracaja do domu. Bezpieczne.

**Awans, wyzwolenie, blizny wojny [78] (Twoj "punkt 14")**
- **Co robi:**
  - Raz w tygodniu ludzie awansuja albo spadaja miedzy stanami, zaleznie od zamoznosci i oszczednosci (chlop -> rzemieslnik -> kupiec -> szlachta).
  - W zdobytej warowni krainy niewolniczej, gdy nowy wladca nie jest z kultury niewolniczej, co 28 dni 1/10 niewolnikow regionu wychodzi na wolnosc.
  - Zlupione miasto i najechane wsie traca kupcow i rzemieslnikow. W pokoju powoli to odrabiaja.
  - Stany widac w oknach "People of this land" i "Look over the district". Raz dziennie dostajesz komunikat o ziemi kupionej albo odsprzedanej w Twoich lennach.
- **MCM:** "Daily life - rise, fall and war" -> **"Daily Life Mobility"**, **"Daily Life Freedom"**, **"Daily Life War Scar"** - wszystkie ON. "Daily Life Prosperity Link" zostaje OFF.
- **Po wylaczeniu:** stany stoja w miejscu. Bezpieczne.

## 3. Ekonomia

**Nowa ekonomia - kalibracja startu (KS) - i to, ze wlacza sie sama [78]**
- **Co robi:** wsie produkuja to, co daja ich rece i ziemia. Ludzie kupuja codzienny koszyk za wlasne pieniadze. Ceny ida za tym, co ludzie naprawde zaplacili, a kupcy rozwoza jedzenie.
  - W Twoim zapisie KS wlacza sie sama (kalendarz w rozdz. 4). Odblokowala to poprawka konwojow DTE: dostawaly konie juczne z niczego, a zabezpieczenie "nic z niczego" przez to nie przepuszczalo KS.
  - **Bramka krainy:** jesli w jakiejs krainie wsie zaczna glodowac z winy KS albo spichlerze za mocno spadna, do starych zasad wraca TYLKO ta kraina. Reszta swiata zostaje z KS.
  - **"Najpierw wyzywic siebie":** glodna wies najpierw stawia rece przy jedzeniu na swojej ziemi. W [78] tylko w zimnych krainach (Polnoc, za Murem, Straz, Skagos, Zelazne Wyspy, Ib). W [79] w calym swiecie.
  - Uczciwie: pod KS wioski same nie zmieniaja zajec. Przez pierwsze tygodnie ceny jedzenia w czesci miast sa 1,4-2 razy wyzsze od startowych.
- **Komunikaty w grze:**
  - na poczatku: "The markets of the realm are being surveyed: for about five weeks the stewards count what every district grows, makes and eats. Nothing changes yet."
  - przy wlaczeniu: "The survey of the realm is done. Over the next two weeks the villages turn to what their own hands and land yield; over the two weeks after that the people's daily basket comes to the markets."
  - gdy kraina wraca do starych zasad: "<kraina>: villages are running out of bread under the new reckoning, so this land returns to the old ways from tomorrow; the rest of the realm keeps the new reckoning."
- **MCM:**
  - "Start calibration (KS)" -> **"Start Calibration"**, ON; **"Start Calibration Auto Arm"**, ON; "Ks Land Gate", ON; "Ks Hard Gates", ON.
  - "Crafts, raw materials and horses (stage 6)" -> "Dte Convoy No Free Packs", ON.
  - "KS round 4: feed yourself first and the yields of cold land (night 17/18.10)" -> "Ks Self Feed First", ON; "Ks Self Feed Everywhere" - w [78] OFF, w [79] ON.
- **Po wylaczeniu:**
  - Zanim KS sie wlaczy: "Start Calibration Auto Arm" OFF = KS tylko liczy i sama sie nie wlaczy.
  - Gdy KS juz dziala: "Start Calibration" OFF = od nastepnego dnia produkcja i popyt jak dawniej, a stan zostaje w zapisie. Ponowne wlaczenie = caly swiat przechodzi od nowa z lagodnym startem.
  - Bramki krainy nie da sie wylaczyc osobno.
  - Nie wylaczaj "Dte Convoy No Free Packs" ani "Ks Hard Gates".

**Kupcy zywnosci na kredyt [78]; zadatek i wczesniejsza splata [79] (z KS)**
- **Co robi:**
  - Woz z jedzeniem wyjezdza od razu. Miasto kupujace placi przy dostawie tyle, ile moze, a reszta to rachunek splacany codziennie, od najstarszego.
  - Miasto z rachunkiem starszym niz 30 dni nie dostaje nowych wozow, dopoki go nie splaci.
  - Statek plynie, gdy jest szybszy. Glodne miasta i Polnoc sa obslugiwane pierwsze.
  - Zamowien zablokowanych z braku pieniedzy jest 0,4% zamiast 38%.
  - [79]: kupcy miasta kupujacego placa zadatek, gdy sprzedawca nie ma juz kredytu. Pan splaca rachunki po 20 dniach. Zawrocony woz oddaje towar sprzedawcy, nic nie przepada.
  - Nie ma osobnego okna - widac to w cenach i na straganach.
- **MCM:** "Start calibration (KS)" -> **"Ks Food Carts Consign"**, ON; "Ks Consign Overdue Days" 30; "Ks Ship By Days", ON; "Ks Famine Priority", ON. W [79] dochodza grupy "Food merchants: earnest money (KS round 5)" i "Town purses and food merchants (economy review 18.10)".
- **Po wylaczeniu:** miasto placi wszystko z gory, jak dawniej. Bezpieczne.

**Tanszy koszyk i place [78]; place wedlug ceny koszyka [79] (z KS)**
- **Co robi:**
  - Kogo nie stac na caly koszyk, ten najpierw kupuje najtansze kalorie (ziarno, chleb, placek albo ryby), a mieso, wino i ser dopiero z reszty. Tak jest wszedzie, bez osobnego prawa dla jednej krainy.
  - Tam, gdzie pracodawcom brakuje rak, dniowka rosnie o 3% na tydzien, najwyzej o 30%. Placi kasa miasta.
  - [79]: place wyrobnikow ustala sie wedlug ceny ich koszyka. Kto ma zapas na 15 dni lub mniej, dostaje pelna stawke; kto ma wiecej, dostaje mniej. Gdzie wolnych rak jest duzo, place moga spasc do 60% - ale tylko razem z kwartalnym przegladem oplat miast. Danina od oszczednosci wyrobnikow schodzi z 3% do 1%.
  - Panowie traca na tym przejsciowo do ok. 80 tys. dziennie, a po ok. 4 miesiacach maja tyle, co przedtem.
- **MCM:** "Start calibration: cheaper basket and wages by hands (r4-koszyk)" -> **"Ks Cheap Basket"**, ON, oraz **"Ks Wage By Hands"**, ON. W [79] grupa "Start calibration: labourers' wages follow their basket (18.10)" -> **"Ks Labour To Basket"**, ON, oraz "Ks Wage Hands Down", ON.
- **Po wylaczeniu:** zakupy i place jak dawniej. Bezpieczne.

**Danina od majatku [78]**
- **Co robi:** to odpowiedz na Twoje "zeby nikt nie zarabial, nie mogac wydac".
  - Kto w miescie albo na podzamczu ma wiecej niz 3 razy swoj zapas, oddaje panu 3% tej nadwyzki dziennie: 2/3 dla pana, 1/3 do kasy miasta, korona nic.
  - Wsie, ktore nie maja na co wydac pieniedzy, placa tak samo.
  - Do tego: mieszczanie z oszczednosciami kupuja lepsze jedzenie, dopoki cena nie przekroczy 1,5 normalnej. Jednorazowy duzy wplyw korony (np. okup) trafia do skarbca, a nie od razu do rodow.
  - Wynik w tescie: kiesy wyrobnikow rosna o 19-35 tys. dziennie zamiast 186 tys.
  - Danina wchodzi stopniowo przez 28 dni. W miastach rusza dopiero, gdy oplaty miast dla pana i krola (DS) sa juz ustalone.
- **MCM:** "Money that cannot be spent (18.10)" -> **"Ds Hoard Due"**, ON; "Ds Hoard Due Rate" 0.03; **"Ds Hoard Due Villages"**, ON (decyzja nocy); "Folk Better Table", ON; "Crown Windfall To Reserve", ON.
- **Po wylaczeniu:** pieniadze znow zbieraja sie u ludzi, ktorzy nie maja ich na co wydac. Bezpieczne.

**Roboty gminy [78]**
- **Co robi:** wies robi wspolne roboty wlasnymi rekami. Placi tylko za prawdziwy material z targu miasta (drewno, wapien, glina, narzedzia, deski) i za najetych rzemieslnikow. Gdy materialu nie ma, robot nie ma, a pieniadze zostaja we wsi. Septy dzialaja tak samo.
- **MCM:** "Money that cannot be spent (18.10)" -> **"Ds Works Real Material"**, ON.
- **Po wylaczeniu:** jak dawniej (place za roboty bez materialu). Wylacza tez danine od majatku wsi.

**Rynek wsi [78]**
- **Co robi:**
  - Targ wiejski Banner Kings kupuje to, czego wiesniacy naprawde potrzebuja: sol, narzedzia, plotno, sukno, garnki - najwyzej na 7 dni.
  - Wino, przyprawy, klejnoty, aksamit i futra kupuja tylko bogate wsie.
  - Naprawiona sol: wsie nie wykupuja juz calej polki, wiec cena nie siedzi na suficie.
  - Wiesniacy nie kupuja juz broni, zbroi ani tarcz, ktore potem znikaly.
- **MCM:** "Village needs and the Banner Kings village market (KS round 4, 17.10)" -> **"Ks Bk Village Needs"**, ON, oraz "Ds Surplus Need Limit", ON (sol). Grupa "Banner Kings coin (stage 3)" -> "Bk Village Market No Arms", ON.
- **Po wylaczeniu:** jak dawniej, wraca problem z sola. Bezpieczne.

**Opal [79] (z KS)**
- **Co robi:** pod KS mieszczanie chcieli palic 29 razy wiecej drewna, takze latem. Przez to drewno bylo 2,5 razy drozsze, a ruda znikala z polowy miast (Twoja kuznia placila 3-4 razy wiecej).
  - Teraz latem pala polowe, zima wiecej.
  - Chca tylko tyle drewna, ile wsie naprawde przywoza.
  - Dymarki kopaln dostaja drewno pierwsze.
- **MCM:** "Goods for the townsfolk (night 16.10, part 2)" -> **"Firewood Ks Season"**, "Firewood Ks Supply Cap", "Firewood Ks Price Demand", "Firewood Mine Wood Keep" - wszystkie ON.
- **Po wylaczeniu:** pelna porcja opalu przez caly rok, jak dawniej. Bezpieczne.

**Skory [79] (z KS)**
- **Co robi:** jedna tabela skor z uboju w miescie i we wsi: krowa daje 1,6 skory, swinia 0,6, owca 0,4. Dotad owca w miescie dawala cala skore, a krowa u rzeznika 5. Dzieki temu mniej skor lezy niesprzedanych na polkach.
- **MCM:** "Start calibration: goods ledger and slaughter hides (night 17/18.10)" -> **"Ks Hides By Table"**, ON.
- **Po wylaczeniu:** jak dawniej. Bezpieczne.

**Warsztaty [79]**
- **Co robi:** warsztat notabla, ktory traci pieniadze od 2 miesiecy, zmienia branze. Teraz wybiera ja wedlug tego, ile miasto naprawde zaplaci, i wedlug surowca, ktory naprawde jest w okolicy.
  - Koniec kuzni srebra bez srebra.
  - Browary, garbarnie i tkalnie przestaja znikac.
  - Przez pierwszy tydzien po wczytaniu regula tylko mierzy i niczego nie przestawia.
- **MCM:** "Production follows price and people (night 16.10, part 3, second half)" -> **"Workshop Retool Fair Score"**, ON.
- **Po wylaczeniu:** wybor branzy jak dawniej. Bezpieczne.

**Oplaty miast dla pana i krola [79]**
- **Co robi:** stala roczna oplata miast i zamkow jest teraz przegladana co kwartal, w gore albo w dol, najwyzej o 15%.
  - Miasto z dlugiem nie dostaje podwyzki.
  - Miasto, ktore zalega dwa kwartaly z rzedu, dostaje obnizke o 15%.
  - Dotad biednym miastom w tescie narastaly dlugi: 1,7 mln po 100 dniach.
- **MCM:** "Town purses and food merchants (economy review 18.10)" -> **"Ds Farm Review Days"** 91.
- **Po wylaczeniu (wartosc 0):** stary przeglad raz na rok. Wylacza tez spadek plac.

**Inwestycje w okreg i specjalizacja wiosek [79]**
- **Co robi:**
  - Kazda wioska poboczna robi to, co pasuje do jej klimatu i terenu (rybacy, zboze, gory, las). Pierwszego dnia swiat ma tyle samo kazdego towaru co dzien wczesniej.
  - Bez KS wioska zmienia zajecie powoli: po 60 dniach o 25% lepszej oplacalnosci, najwyzej jedna wioska w okregu naraz.
  - W menu wsi jest **"Invest in the district's works"**: placisz za sprzet jednej wioski (lodzie, stado, plugi...). Wioska robi wiecej - o 30% wioska poboczna, o 20% wies glowna. Polowa wartosci tego dodatku jest Twoja przy kazdym powrocie wozow okregu.
  - Najezdzcy pala Twoje prace. Odbudowa kosztuje pol ceny.
  - Opcje BetterEconomy 5 000 / 15 000 / 30 000 sa zamkniete.
  - W podpowiedzi wsi: "Primary production: cows (whole district: cattle 85%, grain 8%, fish 7%; villages: ...)".
  - Lordowie AI na razie nie inwestuja.
- **MCM:** "District works and side-village crafts (18.10)" -> **"Side Village Crafts"**, ON; **"District Works"**, ON; "District Icon From Sum", ON; "District Works Ai", OFF (Twoja decyzja).
- **Po wylaczeniu:** "District Works" OFF = nie ma nowych prac ani wyplat, a kupione zostaja zamrozone. Bezpieczne.

## 4. Inne

**Zbrojownia [78]**
- **Co robi:**
  - Twoja sztuka "Fine" albo "Masterwork" nie zamienia sie przy porzadkowaniu na gorszy egzemplarz.
  - Zapasowe konie Twoich ludzi mozna od nich kupic.
  - W bitwie nikt nie dostaje w zastepstwie broni legendarnej.
  - Jezdzcy w kryjowce i przy szturmie na mury walcza pieszo, z dlugimi lukami i pikami.
  - Po przespaniu nocy nie wyskakuje juz "Night Falls - make camp?".
- **MCM:** "The finished piece" - 7 nowych przelacznikow, ON (glowny: "Armoury Book Per Modifier").
- **Po wylaczeniu:** jak dawniej. Bezpieczne.

**Wydarzenia fabuly ROT [78] (CrashScribe)**
- **Co robi:**
  - Smierc postaci z wydarzenia fabuly nie czeka juz do konca oblezenia (Joffrey zyl 4 dni po Purpurowych Godach).
  - Nastepca wskazany przez ROT dostaje tytuly z korona - Cersei, a nie ser Meryn Trant.
  - Ktos z obcego krolestwa nie przejmuje tronu.
- **Przelacznik:** nie ma go w MCM. Jest w pliku `Modules\CrashScribe\ModuleData\CrashScribe.settings.xml`: `RotEventFx` (glowny), `RotEventDeathNow`, `RotEventHeirKeepsTitles`, `RotEventRulerInKingdom` - wszystkie true.
- **Po wylaczeniu (`RotEventFx` = false):** jak przed zmiana. Plik zmieniaj tylko przy zamknietej grze, a najlepiej daj to mnie.

**Szybkosc doby [79]**
- **Co robi:** gra wykonuje mniej pracy, a wynik zostaje ten sam co do monety. Cotygodniowe przyciecie gry przy nowym planie KS spada z ok. 30 do ok. 10 ms.
- **MCM:** "Speed of the day (night 17/18.10)" - 6 przelacznikow "Perf ...", ON. Grupa "Start calibration: day cost" -> "Ks Cost Fast", ON, oraz "Ks Town Fields Every2", OFF (zmienia zachowanie, decyzja Twoja).
- **Po wylaczeniu:** wolniej, ale te same liczby. Bezpieczne.

**Zapis [79]**
- **Co robi:** nasze dane to ok. 1,75 MB z ok. 37 MB pliku zapisu. Reszta to sama gra, BK, DTE i ROT. Po kazdym zapisie w logu jest linia z rozmiarami.
- **MCM:** "Save files" -> "Save Size Log", ON; **"Save Compact"**, OFF.
- **UWAGA:** "Save Compact" zostaw wylaczone, dopoki WGRANIE 79 sie u Ciebie nie sprawdzi. Gdyby bylo wlaczone, a trzeba bylo wrocic do starszej wersji: najpierw wylacz, zapisz gre, dopiero potem podmien DLL.

**Drobne poprawki [78]**
- Partie Banner Kings nie dostaja koni z niczego.
- Bandy nie zabieraja slawnych zbroi i koni - zostaja do zdobycia jako lup.
- Stocznie buduja statki z drewna z targu.
- Ludzie upadlego rodu wracaja do domu, zamiast znikac.
- Lord dwa razy zawrocony na moscie (Bliznieta, Fosa Cailin, Krwawa Brama) wybiera inny cel.
- **MCM:** "Old loose ends (A17)" i inne grupy - wszystko ON. Wylaczenie bezpieczne.

---

## 5. Pierwsze dni po wgraniu

- **Pierwsze wczytanie z WGRANIEM 78:** od razu pojawiaja sie zajazdy, przysiolki (zapis przechodzi sam), herby, drogi +15% i zmiany w zbrojowni. Ruszaja awanse i blizny (raz w tygodniu) oraz danina od majatku wsi (stopniowo przez 28 dni). KS uzbraja sie sama - komunikat "The markets of the realm are being surveyed..." W ekonomii jeszcze nic sie nie zmienia.
- **Dni 1-35:** KS tylko liczy: 28 dni pomiaru (zapamietane w zapisie) i 7 dni gotowego planu. W WGRANIU 78 wlacza sie po ok. 33 dniach, od WGRANIA 79 po 35. Przy bardzo krotkich sesjach (krotszych niz ok. 9 dni gry) wlaczenie sie opozni. KS nie wlacza sie w zimie.
- **Ok. dzien 35:** "The survey of the realm is done..." Przez 14 dni wsie przechodza na produkcje z rak i ziemi, przez kolejne 14 dni na targi wchodzi codzienny koszyk. Od teraz dzialaja kupcy na kredyt, tanszy koszyk, "Hunger", Mur i Wolni Ludzie oraz bramka krainy. W czesci miast jedzenie bedzie przez pewien czas drozsze.
- **Ok. dzien 63:** miasta zaczynaja przyjmowac uchodzcow, najwyzej 1% mieszkancow dziennie. Wczesniej "0 z 97 miast" w logu jest zamierzone.
- **Po WGRANIU 79 dodatkowo:** specjalizacja wiosek startuje neutralnie, warsztaty przez tydzien tylko mierza, pojawia sie menu inwestycji. Jesli KS byl juz uzbrojony w 78, jej wlaczenie moze sie przesunac o ok. 2 dni.

## 6. Jesli cos jest nie tak

Nie cofaj DLL sam. Wylacz przelacznik danego systemu (dziala od nastepnego dnia), graj dalej i napisz mi.

| Objaw | Co wylaczyc w MCM |
|---|---|
| Po wlaczeniu KS glod albo ceny rozjechane w calym swiecie | "Start Calibration" |
| Nie chcesz, zeby KS wlaczyla sie sama | "Start Calibration Auto Arm" |
| KS nie wlacza sie po 40 dniach | nic - przyslij log |
| Miasta zalane uchodzcami | "Raid Refugees To Towns" albo nizsze "Raid Refugees Town Intake" (nie cofaj DLL) |
| Bunty miast z glodu | "Ks Hunger Unrest" albo wyzsze "Ks Hunger Tolerance" |
| Panowie albo korona nagle biedni | "Ds Hoard Due", "Ds Hoard Due Villages"; w [79] tez "Ks Wage Hands Down" |
| Klopot przy zajezdzie (okno, wywrotka) | "Roadside Inns" (plotki: "Inn Gossip") |
| Dziwna predkosc na mapie | "Map Road Speed Bonus" |
| Wioski albo przysiolki w zlych miejscach | "Village Layout V6" (nie po wlaczeniu KS) |
| Zle herby wiosek | "Village Pennant Small Texture" |
| Zbrojownia podmienia rzeczy | "Armoury Book Per Modifier" |
| Bandy Wolnych Ludzi albo danina Muru | "Free Folk Hunger Raids" / "Wall Grain Tribute" |
| Drewno albo ruda drozeja [79] | grupa "Firewood Ks ..." |
| Warsztaty dziwnie zmieniaja branze [79] | "Workshop Retool Fair Score" |
| Fabula ROT dziala dziwnie | daj mi znac (przelacznik w pliku CrashScribe) |

Nigdy nie wylaczaj "Dte Convoy No Free Packs" ani "Ks Hard Gates".

**Jak zglosic:** napisz dzien gry, miejsce i tresc komunikatu (najlepiej zrzut ekranu) oraz to, co zmieniles w MCM. Przyslij:
- najnowszy `...\Mount & Blade II Bannerlord\Modules\Armoury\Armoury-<data>.log` - gra trzyma tylko 12 ostatnich sesji, wiec wyslij od razu,
- `Documents\Mount and Blade II Bannerlord\CrashScribe\session-<data>.log` - przy wywrotce najwazniejszy.

## 7. Gdzie patrzec w logu

**Bledy:** w logu Armoury linie "ERROR in" - ma ich byc 0. W logu CrashScribe bloki zaczynajace sie od "#####" z liniami "WHERE   :" i "BLAME   :".

| System | Linie w logu Armoury |
|---|---|
| KS | "KS: uzbrojony zapis doba", "KS: pomiar przed wlaczeniem dzien N" (pisze, na co czeka), "KS: samo wlaczenie doba", "KS: bramka krainy", "KS: powrot krainy" |
| Kupcy, glod | "Kupcy zywnosci (KS-H) dzien", "Pan miasta placi za dowoz (KS)", "Glod miast (KS)", "Spichlerze (E4/6)" |
| Uchodzcy | "Uchodzcy do miast (RT): dzien N" |
| Mur | "Danina Muru (r4-mur)", "Najazdy Wolnych Ludzi (r4-mur)" |
| Koszyk i place | "Tanszy koszyk (r4-koszyk)", "Place za brak rak (r4-koszyk)", [79] "Place W wedlug koszyka (f4-place-ludu)" |
| Danina od majatku, roboty gminy | "Akumulator kies (ds-akumulator)" |
| Rynek wsi, oplaty miast | "Potrzeby wsi (B1)", "Daniny wsi (DS)", "Firmy miast i zamkow (DS)" |
| [79] opal, skory, warsztaty, okreg | "Opal mieszczan (N2-E4)", "Skory z uboju (KS-S)", "Warsztaty za cena (E3-D3)", "Zajecia wiosek (ZW)", "Prace okregu (PO)" |
| Awans, blizny | "Awans (ZC6)", "Blizna wojny (ZC7)" |
| Swiat | "Zajazdy (ZJ)", "Plotki (PL)", "Drogi (DR)", "Przysiolki (sw-przysiolki)", "Proporczyki - tekstury (E7/W5)" |
| Zbrojownia | "Zbrojownia gracza (ZB)" |
| [79] szybkosc, zapis | "Szybkosc doby (perf-d2d3)" ("rozjazdow 0"), "Zapis (D4)" |
| ROT (log CrashScribe) | "ROT wydarzenie: ... wykonany / odroczony / zablokowany przez ..." |