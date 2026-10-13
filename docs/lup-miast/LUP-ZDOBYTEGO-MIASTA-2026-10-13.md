# LUP ZDOBYTEGO MIASTA - jak jest dzis i propozycja reguly (13.10.2026)

Status: PRACA PROJEKTOWA, TYLKO ODCZYT. Gry nie uruchamiano, nic nie budowano, kodu, ustawien i zapisow nie zmieniano, bez commita.
Pytanie Jeffa (13.10): "A co, jak miasto zostanie zdobyte - jak dziala pladrowanie? Bo powinno dac sie pladrowac to, co jest prawdziwym
bogactwem miasta. I jak to dziala teraz?" Tlo: prosba Jeffa z tego samego dnia o propozycje podatkow na czas wojny (prog 85 tys. wojska) -
odpowiedz, ile lup moze do tego dolozyc, jest w rozdz. 0 pkt 20 i rozdz. 5.2.

**Zrodla.** Trzy rozpoznania i ich weryfikacja (katalog roboczy `...\dbc3d3d1-...\scratchpad\lup-miast\`): `rozpoznanie-gra-i-mody.md` (R1),
`rozpoznanie-armoury.md` (R2), `pomiar-zdobycia.md` (R3), `weryfikacja.md` (W - gdzie W cos poprawil, obowiazuje jego wersja).
Skroty sciezek (pod `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad\`):
**G** = `dec32\cs_full\` (gra 1.4.8, dekompilacja), **BK** = `e4tools\bkfull\` (BannerKings), **ROT** = `dec37\rot\`,
**A** = `zb\int\Armoury\src\` (nasz kod, galaz `w-toku/zbior-e8`, HEAD 6d91156 = WGRANIE 37).
**LOG** = `D:\Backup-Bannerlord\logs\2026-10-13-noc34-35\Armoury-2026-10-13_06-24-10.log`, **TAB** = `...\noc34-35\Logs\2026-10-13_06-24-10\`
(bieg roczny: 364 doby nowej kampanii, sam swiat AI, doby gry 108836-109199).
Skrypty autora (do powtorzenia kazdej liczby z rozdz. 2-3; `python -I`): `...\lup-miast\autor\a1_zbiorniki.py`, `a2_regula.py`, `a3_linie.py`,
`a4_linie.py`, `a5_dni.py`.
**Oznaczenia:** [P] = pomiar z logu albo tabeli, [S] = szacunek / rachunek (napisane z czego), [K] = wprost z kodu (plik:linia).
"Nie wiem" = nie ma tego w zrodlach. Kwoty w zl (1 zl = 1 denar gry). Rok = 364 doby (kalendarz Armoury).

---

## 0. Dla Jeffa - prostym jezykiem

**CZESC A - jak to dziala DZIS**

1. Po wygranym szturmie zdobywca wybiera: laska, grabiez albo spustoszenie. AI losuje wedlug charakteru wodza; osade wlasnej kultury zawsze oszczedza.
2. Gra nie lupi miasta, tylko liczbe: zdejmuje kawalek "dobrobytu" i daje armii 15 zl za kazdy spalony punkt. W czystej grze to zloto z niczego. U nas placi je kasa kupcow zdobytej osady (z tego, co ma ponad swoj zapas) - zloto jest wiec prawdziwe, ale kwota nie zalezy od tego, czy miasto bylo bogate. Laska = zero.
3. Ile: w roku testu 14 zdobyc (3 miasta, 11 zamkow), caly lup swiata 64 728 zl [P]. Harrenhal, najwieksze: 19 661 zl na 10 429 ludzi - niecale 2 zl na glowe [P]. Siedem zdobyc skonczylo sie laska i zerem [P].
4. Wieksze pieniadze ida bokiem: zwyciezcy sprzedaja wzietych obroncow kasie wlasnie zdobytej osady - 399 tys. zl w roku [P], 7 razy wiecej niz lup. Nadwyzka kupcow jest wiec juz dzis wybierana do dna, tylko pod nazwa "jency".
5. Czego nikt nie rusza: pieniedzy mieszczan (w 14 osadach 633 tys. zl, w calym swiecie 44 mln [P]), zapasu kupcow, towaru i koni na polkach, zywnosci, warsztatow, zlota notabli, karawan. To tylko zmienia flage. Prawdziwy lup jest jedynie z bitwy o mury: jency, bron i kiesa zalogi.
6. Co miasto traci: dobrobyt (gra 8-33%, potem nasza "blizna" - kolejne 15% tego, co zostalo, takze przy lasce), poziomy budowli, lojalnosc, a Banner Kings zabija 10-24% mieszkancow. Nikt nic za to nie dostaje. Dobrobyt wraca latami: Saltpans ok. 230 dob, Harrenhal ok. 570 [S].
7. W pieniadzu zdobyte miasto ZYSKUJE: wojsko zostawia w nim zold odlozony przez cale oblezenie. Harrenhal: kasa kupcow w dobie zdobycia z 210 na 450 tys., pieniadze mieszczan z 52 na 589 tys. w miesiac [P]. Nowy pan i korona sciagneli potem z 3 miast 780 tys. zl w 30 dob zwykla renta [P].
8. Bledy i "z niczego": gra robi "grabiez" takze po wygranej bitwie pod murami BEZ zdobycia (5 razy w roku; zamek Vaes Efe zaplacil lup, stracil dobrobyt i nie padl wcale [P]); kiesa zalogi rozbitej w takiej bitwie znika (16 249 zl [P]); zabitych przez Banner Kings nie ma w naszej ksiedze ludzi; gracz dostaje 15% mniej lupu niz AI; zdarzenie gracza "Right of Pillage" pustoszy miasto i nie daje nikomu nic (dwa ostatnie - z kodu, nie z pomiaru).

**CZESC B - propozycja: "w lupionym miescie wojsko bierze to, co tam naprawde lezy"**

9. Jedna regula: zdobywcy biora czesc KAZDEJ monety, ktora jest w zdobytej osadzie - z kasy kupcow i ze skrzyn mieszczan jednakowo. Laska: nic. Grabiez: trzecia czesc. Spustoszenie: dwie trzecie. Kwota "od dobrobytu" znika.
10. Nic nie powstaje i nic nie znika: ile zeszlo miastu, tyle co do zlotowki dostaja zwyciezcy. Osada pusta albo zlupiona miesiac temu daje malo, bo malo w niej lezy.
11. Kto dostaje: lup dzieli sie miedzy druzyny wedlug liczby ludzi. W kazdej druzynie jak przy innym lupie ("prawo trzecich", Twoje decyzje z 05.10 i 09.10): zolnierze 2/3 do swoich sakiewek, wodz 1/3, a z jego trzeciej korona w wojnie bierze trzecia czesc (1/9 calosci).
12. Druga polowa tej samej reguly: w miescie, ktore wojsko lupi, zolnierze przez 3 doby nie zostawiaja zoldu w karczmach (biora, nie placa) - wydadza go w nastepnym miescie. Bez tego zlupione miasto jest nazajutrz bogatsze niz przed szturmem (pkt 7). Przy lasce wojsko placi jak zwykle i oszczedzone miasto na nim zarabia.
13. Dzieki temu wybor jest prawdziwy: grabiez = pieniadz dla armii od reki i miasto biedne na dlugo; laska = nic dla armii, ale nowy pan i korona dostaja miasto, ktore placi (renta, danina wojenna).
14. Co widzisz (napisy po angielsku, prawdziwe kwoty) przy kazdym z trzech wyborow, np.: "Merchants' coin: 120,386 - the army takes 40,129. Townsfolk's coin: 56,162 - the army takes 18,721. Your party: 20,560 (your men 13,707, you 6,853)." Po wyborze podsumowanie; gdy lupia Twoje miasto - wiadomosc, ile stracilo.
15. Jak wybiera AI: swoich (ta sama kultura) oszczedza jak dzis. Wodz, ktorego wojsko ma niezaplacony zold albo ktorego kiesa nie starcza na dwa tygodnie zoldu, bierze grabiez (okrutny - spustoszenie). Nikt nie lupi osady, w ktorej nie ma czego brac. Wodz z oplaconym wojskiem wybiera wedlug charakteru, jak dzis.
16. Co potem z miastem: kupcom i mieszczanom zostaje 2/3 albo 1/3 pieniedzy, wojsko nic nie dosypuje, wiec renta dla nowego pana i danina dla korony na tygodnie prawie znikaja. Dobrobyt, budowle, lojalnosc, zabici - jak dzis, z jedna zmiana: przy lasce nie ma naszej "blizny" (laska ma byc laska).
17. Trzy naprawy przy tej samej regule: bez zdobycia nie ma grabiezy; kiesa rozbitej zalogi idzie do zwyciezcy; "Right of Pillage" lupi ta sama regula (zolnierze naprawde cos dostaja).
18. Pozniej, osobno: towar i konie z polek (ta sama czesc, tyle, ile tabory uniosa; przy spustoszeniu reszta z tej czesci plonie - jak we wsiach) oraz zabici przez Banner Kings zdjeci takze z naszej ksiegi ludzi.
19. Ile to zlota: na 14 zdobyciach z testu 465 tys. zl lupu zamiast 55 tys. [S z P]; Harrenhal 175 tys. = 3,4 doby zoldu tej armii zamiast 0,4. Tamte osady padly wczesnie i biedne. Miasto z konca roku ma w monecie srednio 564 tys. zl (polowa miast ponizej 322 tys.) [P]: grabiez 107-188 tys., spustoszenie 215-376 tys. [S] - od kilku dni do dwoch tygodni zoldu armii 5 tys. ludzi.
20. Uczciwie o progu 85 tys. wojska i podatkach na wojne: lup NIE jest zrodlem. Nawet przy samych spustoszeniach to ok. 1 mln zl rocznie = 0,6% zoldu druzyn lordow (zold 161,8 mln, daniny wojenne juz dzis 77,5 mln [P]). Lup to nagroda dla armii, ktora wygrala, i prawdziwa strata miasta. Pieniadze na wojsko musza przyjsc z podatkow - to osobny projekt, ktory zamowiles (korona placi wedlug potrzeby, tarczowe, stawka wojenna). Ta regula mu nie przeszkadza: to te same skrzynie, ale lup bierze raz, w dniu szturmu, a danina co dobe z tego, co zostalo.

**CZESC C - czego nie wiemy**

21. Jak to wyglada u zywego gracza: test byl samym swiatem AI; okno wyboru i kwoty u gracza znam tylko z kodu.
22. Ile warte sa towar, konie i bron na polkach jednego miasta (szacunek: towar 33-36 tys. zl, konie rzad 50-130 tys. [S]) i ile karawan stoi w murach.
23. Jak czesto AI bedzie lupic przy nowym wyborze i o ile spadnie sprzedaz jencow w zlupionych osadach (kasa po lupie ma mniej na wykup): razem z jencami zwyciezcy wzieliby ok. 0,7 mln zamiast 0,45 mln [S].
24. Czy po spustoszeniu mieszczanom starczy na chleb (w trzech miastach mieli w chwili zdobycia zapas na 2-5 dni [S]) i jak zmienia sie ceny - nie mamy tabeli cen na miasto. Dlatego pierwszy krok to sam pomiar w logu, bez zmiany gry.

Pytania o kierunek - rozdz. 8.

---

## 1. Jak jest dzis - z dowodami

### 1.1 Kolejnosc zdarzen [K]

1. **Bitwa o mury** to zwykla bitwa - dzialaja zwykle reguly lupu z bitwy: jency, zloto wodzow pokonanych (5%, najwyzej 10 000), kiesy rozbitych partii,
   sakiewki ludzi, sprzet (G `TaleWorlds.CampaignSystem.MapEvents\MapEvent.cs:1411-1432`; nasze: A `BattlePlunder.cs`, `BeatenPurse.cs`, `MenPurse.cs:104-134`,
   `CasualtyLoot.cs:13-27`, `HorseFate.cs:796-807`). Towar z polek osady NIE jest lupem bitwy: dla pokonanej "partii" bedacej osada lista szans jest pusta
   (G `...GameComponents\DefaultBattleRewardModel.cs:289`, `MapEvent.cs:1636-1639`). Zwykli jency z lochu osady przechodza do zwyciezcow jako zolnierze
   (G `MapEvent.cs:1529-1593`, linie 1546, 1553, 1577 - poprawka W).
2. **Nastepstwa (aftermath)**: po wygranej oblegajacych w bitwie typu szturm, bitwa pod murami (SiegeOutside), wypad albo wypad z blokady
   (G `...CampaignBehaviors\SiegeAftermathCampaignBehavior.cs:79-80` - warunku "osada zdobyta" NIE ma). AI wybiera i wykonuje od razu (:112-116);
   gracz w cudzej armii - tez od razu, wybor AI (:98-110); gracz-wodz - z menu po bitwie (G `...Encounters\PlayerEncounter.cs:1748-1777`).
3. **Zmiana pana** zaraz potem, w tej samej metodzie konca bitwy (G `MapEvent.cs:2088-2095` -> `...CampaignSystem\KingdomManager.cs:239-278`): tylko dla szturmu
   i wypadow (:241), NIE dla bitwy pod murami. Nowym panem jest najpierw wladca krolestwa (:245); stara zaloga niszczona, nowa tworzona
   (G `...Actions\ChangeOwnerOfSettlementAction.cs:34-41`); wszystkie druzyny oblegajace wchodza do osady (`KingdomManager.cs:259-273`).
   Kto ostatecznie dostaje osade, liczy BannerKings swoim wzorem (BK `BannerKings.Patches.Government\FiefOwnerPatches.cs:52-60`,
   `BannerKings.Models.Vanilla\BKDiplomacyModel.cs:493`, :518 - poprawka W); w biegu 10 z 14 osad zostalo u rodu wladcy, 4 zmienily rod dobe pozniej [P].
4. **Kolejnosc u AI i u gracza jest rozna**: u AI nastepstwa sa PRZED zmiana pana, u gracza-wodza PO niej, czyli po naszej "bliznie" (pkt 1.4) -
   dlatego gracz dostaje 0,85 tego, co AI w tej samej sytuacji [K + S, W rozdz. 4 pkt 2].
5. Po zdobyciu: milicja osady +90..108 ludzi (G `...CampaignBehaviors\MilitiasCampaignBehavior.cs:46-52`; u nas zdejmowana z ludzi osady - A `ArmsFromPeople.cs:442-443`, :970).

### 1.2 Trzy wybory zdobywcy [K] (wszystko w G `SiegeAftermathCampaignBehavior.cs`)

"n" = zdrowi w druzynie wodza i w druzynach DOCZEPIONYCH do jego armii (:651-655; G `...CampaignSystem\Army.cs:175`) - nie wszyscy oblegajacy.
Baza B% = log2(n x 0,04 + 2) x 2,5 + 2,5 procent dobrobytu (:656): 12% przy 300 ludziach, 16% przy 1 000, 22% przy 5 000 [S ze wzoru].

| Skutek | Spustoszenie (Devastate) | Grabiez (Pillage) | Laska (Show Mercy) | Linie |
|---|---|---|---|---|
| Dobrobyt | -1,5 x B | -B | -0,5 x B | :131-132, :649-668 |
| Poziomy budowli (k = floor(log2(n x 0,02 + 2))) | ceil(1,5 x k) | k | 0 | :133-139, :670-689 |
| Lojalnosc | -30 | -15 | 0 | :140, :691-704 |
| Sila notabli (tylko miasto) | -50% | -25% | 0 | :141-147 |
| **Zloto armii** | 15 x utracony dobrobyt | 15 x utracony dobrobyt | **0** | :158, :742-751 |
| Morale druzyn zwycieskich | +20 | +10 | -15 (0 przy tej samej kulturze) | :171, :721-740 |
| Koszt wplywow (tylko w armii) | za litosciwych lordow | 0 | za okrutnych lordow | :801-822 |
| BannerKings: zabici mieszkancy | 16-24% ludnosci BK | 10-16% | 0 | BK `BannerKings.Behaviours\BKSettlementBehavior.cs:157-187` |
| BannerKings: stabilnosc | -0,45 | -0,25 | -0,10 | BK tamze :141-158, :190 |

Wybor AI (:205-270): wodz zabity albo ranny - inna kultura: spustoszenie, ta sama: laska (:187-194). Poza tym: laska dozwolona przy tej samej kulturze
albo przy wplywach i morale powyzej 60 (:215-218); grabiez i spustoszenie tylko wobec innej kultury (:219-226); wagi losu wedlug cechy Mercy wodza (:230-248).
**AI nie patrzy na swoje finanse** - tylko cecha, kultura, wplywy, morale. Gracz ma te sama blokade kultury w menu (:824-837).

### 1.3 Skad jest zloto lupu i kto je dostaje [K + P]

- Gra: pula = floor(15 x utracony dobrobyt) (:742-751); udzial druzyny obciety do calych procentow (:72), potem floor(pula x procent / 100) (:753-756) -
  przy duzej armii czesc puli przepada: Saltpans wyplacono 90% wzoru, Starfall 82%, Harrenhal 73% (20 / 33 / 55 druzyn) [P].
  Wyplata: `GiveGoldAction(nic -> wodz druzyny)` (:165) albo do kiesy druzyny bez wodza (:169). W czystej grze dajacego nie ma - zloto z niczego
  (G `...Actions\GiveGoldAction.cs:11-26`). Odbiorca to KIESA WODZA. Nie zolnierze, nie korona.
- Nasz mod (RealPayer): dajacym staje sie zdobyta osada, kwota przycieta do jej nadwyzki (A `RealPayer.cs:157-168` kontekst, :74-111 przyciecie, wpiecie :286).
  Nadwyzka = kasa minus zapas kupcow (A `Seal31.cs:104-116`, `TownPurse.cs:225-251`; miasto: zapas nie mniej niz 10 000 + 12 x dobrobyt; zamek:
  `CastlePurse.Spendable`). Wylacznik `TownPaysRewards` (A `Settings.cs:314`).
- Pomiar [P]: gra chciala 65 280, osady zaplacily 64 728, uciete 552, 197 wyplat. Pozycja "lup z oblezen" w zrodlach z niczego: 0 w 363 dobach.
  Okno O10 ksiegi (A `CirculationWindows.cs:619-625`, wpiecie :1822 - sam pomiar): 19 wywolan, 0 trafien z niczego.
- Uwaga [K]: gra tnie dobrobyt PRZED wyplata, wiec zapas kupcow spada o 12 zl za punkt, a lup to najwyzej 15 zl za punkt - 4/5 lupu to pieniadz
  "zwolniony" z zapasu samym zubozeniem miasta. Lup nie zalezy od tego, ile miasto naprawde ma.

### 1.4 Co jeszcze robi nasz mod przy zdobyciu [K]

| Miejsce | Co robi | Zloto / ludzie |
|---|---|---|
| A `ArmouryBehavior.cs:727` -> `WarLedger.cs:291-312` | "blizna po szturmie": przy kazdej zmianie pana przez oblezenie dobrobyt x (1 - 15%), lojalnosc -15 - takze przy lasce. Ustawienia A `Settings.cs:475-477`; w `Armoury.json` Jeffa te same wartosci | pali dobrobyt, nikt nic nie dostaje |
| A `SmallGold.cs:210-243`, :176-205 | kiesa starej zalogi zdjeta przed jej zniszczeniem i oddana NOWEJ zalodze tej osady; nowa zaloga bez 5 000 zl z niczego | stara zaloga -> nowa zaloga; 12 razy, 69 376 zl w roku [P] |
| A `RealPayer.cs:115-155` | zaplata za jencow sprzedanych osadzie - z jej nadwyzki; jency i tak znikaja z druzyny (kwota tylko przycieta :93-105) | kasa osady -> wodz; w 14 dobach zdobyc 399 411 zl [P] |
| A `MenPurse.cs:275-325` | zolnierze przy wyjezdzie z MIASTA wydaja wolna czesc sakiewki "na zycie": 75% do kasy kupcow (:317), 25% do kies ludu (:316; A `TownFolk.cs:62`) | sakiewki ludzi -> miasto; doba zdobycia Harrenhal 701 887 zl przy medianie 167 590 [P] |
| A `Sutlers.cs:14-33` | w polu ludzie wydaja co dobe 10% wolnej sakiewki; 85% do kasy najblizszego miasta niebedacego w wojnie z druzyna | sakiewki -> miasta i wsie |
| A `BesiegedPay.cs:12-49` | oblezona osada nie placi nic; po zdobyciu wszystko plynie od razu do NOWEGO pana i korony | wstrzymuje przelewy |
| A `KingdomBalance.cs:304-318` | licznik twierdz zdobytych / straconych | tylko log |

Reguly rabunku i spustoszenia (`Devastation.cs`, `DistrictRaid.cs`, `ScorchedEarth.cs`) dotycza WYLACZNIE wsi. Miasta i zamki nie maja u nas zadnej wlasnej reguly lupu.

### 1.5 Cudze mody [K]

- BannerKings: zabici i stabilnosc (tabela 1.2); warsztaty bohaterow strony przegranej zabierane bez zaplaty na rzecz notabla albo nowego pana, z kapitalem
  (BK `...Behaviours.Workshops\BKWorkshopBehavior.cs:68-74`, :88-130; to samo przy wypowiedzeniu wojny :76-86). Zlota przy zdobyciu BK nie rusza.
- Realm of Thrones: osady-blizniaki (Red Keep - King's Landing, Mur - Castle Black) i wydarzenia fabuly zmieniaja pana BEZ nastepstw, z cala zaloga, kasa i towarem
  (ROT `ROT.CampaignBehaviors\ROTOthersCampaignBehavior.cs:298-315`; ok. 25 wywolan w `ROT.Events\*`). Wzoru lupu ROT nie zmienia.
- Diplomacy: po podsumowaniu lupu pyta gracza o lenno. SandBox i StoryMode: powiadomienia, tabory, zadania - zlota i towaru nie ruszaja (W rozdz. 2).
- Na `OnSiegeAftermathApplied`, wzorach `GetSiegeAftermath*` i wyborze AI nikt obcy latki nie zaklada (W rozdz. 2, skan DLL).

### 1.6 Pomiar: 14 zdobyc w biegu rocznym [P] (R3 tab. 2.1, 6.1, 6.2, 7.2; liczby potwierdzone przez W)

| Osada | Zdobywca, ludzi | Wybor AI [S z lupu i dobrobytu] | Lup | Jency (doba zdobycia, swiat) | Kasa kupcow przed | w tym ponad zapas | Pieniadze mieszkancow przed |
|---|---|---|---:|---:|---:|---:|---:|
| Cape Wrath (zamek) | Stormlands, 1 661 | laska | 0 | 42 277 | 58 832 | 36 775 | 26 461 |
| Fellwood (zamek) | Stormlands, 1 808 | laska | 0 | 7 035 | 23 496 | 328 | 14 014 |
| **Saltpans (miasto)** | The Vale, 3 549 | grabiez | 8 499 | 22 191 | 120 386 | 52 629 | 56 162 |
| **Starfall (miasto)** | The Reach, 5 075 | grabiez | 11 739 | 35 938 | 120 225 | 49 972 | 80 178 |
| Demongate (zamek) | Volantis, 2 333 | laska (kultura) | 0 | 18 228 | 23 932 | 147 | 19 776 |
| Blackmont (zamek) | The Reach, 3 212 | laska | 0 | 10 101 | 25 293 | 279 | 17 663 |
| **Harrenhal (miasto)** | The North, 10 429 | spustoszenie | 19 661 | 146 334 | 209 861 | 134 438 | 51 945 |
| Sandstone (zamek) | The Reach, 2 355 | laska | 0 | 14 227 | 25 995 | 34 | 42 545 |
| Kingsgrave (zamek) | The Reach, 3 706 | grabiez | 3 941 | 29 485 | 46 718 | 18 150 | 33 890 |
| Sombazmion (zamek) | Tyrosh, 1 133 | grabiez | 2 593 | 4 090 | 27 250 | 140 | 30 325 |
| Sweetport (zamek) | Stormlands, 3 377 | laska | 0 | 32 491 | 50 791 | 20 658 | 56 453 |
| Bisa (zamek) | Braavos, 1 543 | grabiez | 3 675 | 22 624 | 58 656 | 29 061 | 63 223 |
| Stonedance (zamek) | Stormlands, 2 670 | laska | 0 | 11 206 | 35 602 | 2 484 | 68 625 |
| Willow Wood (zamek) | Baratheon KL, 4 526 | spustoszenie | 4 836 | 3 184 | 46 345 | 22 489 | 71 452 |
| **Razem** | | 7 / 5 / 2 | **54 944** | **399 411** | **873 382** | 367 584 | **632 712** |

- Lup roku razem 64 728 zl: przy zdobyciach 54 944 (miasta 39 899, zamki 15 045) i 9 784 po bitwach pod murami BEZ zdobycia.
- 7 lask: 1 z kultury (Demongate - Volantis i zamek kultury `volantine`), 6 z losu wedlug cechy wodza (poprawka W).
- Jency: linia logu to suma swiata na dobe; przypisanie do osady - [S] (zwykla doba to ok. 3 300 zl).
- Wszystkie 14 zdobyc szturmem. 98 oblezen w roku, w zadnym warownia nie glodowala [P].
- "Grabiez" bez zdobycia, 5 razy [P]: Cape Wrath 2 razy jednej doby (lup 5 058, dobrobyt 1 005 -> 639, ludnosc BK 2 633 -> 1 811), Fellwood (2 573),
  Vaes Efe 2 razy (0 i 2 153; nie zdobyte w calym roku).

Trzy miasta - przed i po [P] (TAB `kasy-miast.csv`, `kiesa-ludu.csv`, `ludnosc-bk.csv`):

| | Saltpans | Starfall | Harrenhal |
|---|---:|---:|---:|
| Dobrobyt: przed -> po karze gry -> po naszej bliznie | 4 813 -> 4 182 -> 3 554 | 5 021 -> 4 065 -> 3 455 | 5 452 -> 3 652 -> 3 104 |
| w tym nasza blizna, w punktach proc. stanu sprzed (poprawka W) | 13,0 | 12,1 | 10,1 |
| Ludnosc BK przed -> po | 11 967 -> 10 405 | 13 364 -> 11 641 | 14 818 -> 12 086 |
| Ludzie miasta w naszej ksiedze przed -> po | 29 366 -> 29 286 | 22 684 -> 22 569 | 31 716 -> 31 616 |
| Kasa kupcow: dobe przed -> doba zdobycia -> nastepna | 120 386 -> 89 006 -> 155 436 | 120 225 -> 76 730 -> 150 445 | 209 861 -> 450 394 -> 435 253 |
| Pieniadze mieszczan: przed -> ok. tydzien po -> ok. miesiac po | 56 162 -> 154 255 -> 295 947 | 80 178 -> 131 583 -> 246 484 | 51 945 -> 500 765 -> 588 623 |
| Zapas mieszczan w dniach pelnego koszyka, przed [S, a5_dni.py] | 3,0 | 5,5 | 2,0 |
| "Stac na jedzenie" przed i po | 1,00 / 1,00 | 1,00 / 1,00 | 1,00 / 1,00 |
| Zawor nowemu panu i koronie, 30 dob po (30 wierszy - poprawka W) | 90 849 | 176 503 | 512 722 |

Skad wzrost: "sakiewki ludzi - zycie w miastach" - mediana roku 167 590 zl na dobe, w dobie zdobycia Harrenhal 701 887 (najwyzsza doba roku), dobe po Saltpans
248 412, dobe po Starfall 284 457 [P]. Nadwyzka ponad zwykla dobe: ok. 534 + 81 + 117 = 732 tys. zl zostawione przez zwyciezcow w trzech zdobytych miastach [S].

### 1.7 Dziury dzis

| # | Dziura | Dowod |
|---|---|---|
| D1 | Kwota lupu liczona od dobrobytu, nie od tego, co w osadzie lezy; siega tylko do nadwyzki kasy kupcow | pkt 1.3 |
| D2 | "Grabiez" bez zdobycia: 5 razy, 9 784 zl, strata dobrobytu, ludzi BK i budowli w osadzie, ktora zostala u obroncy | G `SiegeAftermathCampaignBehavior.cs:79-80`; `KingdomManager.cs:241`; pkt 1.6 |
| D3 | Kiesa zalogi rozbitej w bitwie pod murami znika: 16 249 zl w 3 dobach | A `SmallGold.cs:215-218` (tylko okno zmiany pana); LOG pozycja ujsc "garnizony" [P] |
| D4 | Rzez BK poza nasza ksiega ludzi: BK -13..-18% ludnosci miasta, nasza ksiega -100 osob; "zabici" dalej zarabiaja i placa | pkt 1.6; A `TownFolk.cs` (glowy z ksiegi ludzi) |
| D5 | Nasza blizna -15% takze przy lasce i sumuje sie z kara gry (dwie kary za jedno zjawisko) | A `WarLedger.cs:299-304` |
| D6 | Gracz dostaje 0,85 lupu AI (kolejnosc: blizna przed rachunkiem gry) | pkt 1.1 pkt 4 |
| D7 | "Right of Pillage" (tylko gracz): nastepstwa grabiezy na osadzie gracza z PUSTA lista udzialow - strata bez zlotowki dla kogokolwiek | G `...CampaignBehaviors\IncidentsCampaignBehaviour.cs:677-712` (wywolanie :707) |
| D8 | Udzialy obciete do calych procentow: 10-27% puli nie jest wyplacane; druzyna z udzialem ponizej 1% dostaje 0 | G `SiegeAftermathCampaignBehavior.cs:72`, :753-756 |
| D9 | Podpowiedz w menu gracza liczy dobrobyt od ostatniej "spladrowanej" osady na swiecie (pole pamieci nigdy nie wraca do -1) | G tamze :34, :131, :656 |
| D10 | Zdobyte miasto bogaci sie w pieniadzu takze po spustoszeniu (wojsko zostawia w nim zold z calego oblezenia) | pkt 1.6 |
| D11 | Korona nie ma udzialu w lupie z oblezenia, choc ma 1/9 w kazdej innej zdobyczy | A `Ransom178.cs:38-42`; w `RealPayer.cs` brak wywolania |

Czego NIE ma jako dziury [P]: lupu zlota z niczego (0 w roku), kiesy startowej nowych zalog (85 000 zl nie powstalo), kopii sprzetu (zdjety pokonanym = dopisany zwyciezcom).

---

## 2. Spis bogactwa miasta i co sie z nim dzieje przy zdobyciu

Stan swiata na koniec biegu (doba 364): LOG:129205 "Pieniadz swiata: dzien 109199", LOG:129238 "Kiesa ludu", LOG:129244 "Nadwyzka kas", LOG:129221 "Konie (E6/0)",
LOG:129220 "Uzbrojenie wedlug zrodla", LOG:129284 "Zbrojownie zalog". Caly pieniadz swiata 208 335 095 zl [P]. "Typowe miasto" = srednia / mediana 97 miast z ostatniej
migawki `kiesa-ludu.csv` [P, a1_zbiorniki.py].

| # | Zbiornik | Gdzie (kod) | Swiat, koniec roku | Typowe miasto (srednia / mediana) | Przy zdobyciu DZIS | W propozycji |
|---|---|---|---|---|---|---|
| 1 | **Pieniadze mieszczan** (kiesy ludu: wyrobnicy, rzemieslnicy, rodziny kupieckie) | A `TownFolk.cs:101` (KW, KR, KP) | 44 032 079 zl w 97 miastach [P]; z tego ponad zapas 10 dni 29 485 864 | 453 939 / 224 487 | nietkniete | **lup: 1/3 albo 2/3** (krok 1) |
| 1a | Kiesa podzamcza (zamki) | A `TownFolk.cs:101` (KW zamku) | 3 314 164 zl w 130 zamkach [P] | zamek: 25 494 / 5 905 | nietknieta | **lup, ta sama czesc** |
| 2 | Kasa gminy | A `TownFolk.cs:101` (KG) | 139 853 zl [P] | ok. 1 400 | nietknieta | lup, ta sama czesc |
| 3 | **Kasa kupcow** (Town.Gold) | A `TownPurse.cs:225-251` | miasta 10 975 439 (zapas kupcow 8 466 483, ponad zapasem 2 753 534); zamki 5 405 157 (ponad zapasem 756 538) [P] | 110 367 / 97 383; ponad zapasem 26 579 / 6 910 | lup gry i jency z nadwyzki; reszta zostaje | **lup: 1/3 albo 2/3 calej kasy** (takze z zapasu - nowa decyzja, pytanie 1) |
| 4 | Towar na polkach (bez zywnosci) | `Settlement.ItemRoster`; A `GoodsLedger.cs` | miasta ok. 3,2-3,5 mln zl wedlug cen bazowych [S, R3 7.2] | 33-36 tys. zl [S] | zostaje | krok 4: ta sama czesc, do udzwigu taborow |
| 4a | Konie na polkach miast i zamkow | LOG "Konie (E6/0)" | 7 223 zwyklych, 1 863 bojowych, 337 szlachetnych, 4 288 jucznych [P]; ok. 11,6 mln zl po cenach, po ktorych mieszczanie brali konie tej doby [S] | rzad 50-130 tys. zl [S]; na osade nie wiem | zostaja | krok 4: ta sama czesc, konie ida same |
| 4b | Bron i zbroja na polkach i w schowkach osad | LOG "Uzbrojenie wedlug zrodla" | 307 077 szt. [P]; wartosci nie znam | nie wiem | zostaja | krok 4, po osobnym planie (zbrojownie druzyn) |
| 5 | Zywnosc warowni (liczba FoodStocks) | gra; A `FoodChain.cs`, `Famine.cs` | miasta 2 959 929, zamki 1 225 417 jednostek [P, R2] | 23-29 tys. jednostek | -8..-15% w 1-2 doby, potem rosnie [P] | NIE lupimy (rozdz. 3.1) |
| 6 | Kapital warsztatow | gra; A `WorkshopTrade.cs:711-717` | 2 568 293 zl, 291 warsztatow [P] | ok. 26 tys. | warsztaty lordow strony przegranej zabiera BK razem z kapitalem | bez zmian (rozdz. 3.1) |
| 7 | Zbrojownia zalogi | A `GarrisonArmory.cs:18-33` | 477 690 szt. w 227 zalogach [P] | - | sprzet pokonanej zalogi do zwyciezcow: 19 447 szt. w roku [P] | bez zmian (to lup z bitwy) |
| 7a | Kiesa zalogi | gra; A `SmallGold.cs:210-243` | 1 472 181 zl [P] | ok. 6 500 | do nowej zalogi; rozbita wczesniej w bitwie pod murami - znika (D3) | naprawa D3: do zwyciezcow bitwy |
| 8 | Zloto notabli | A `NotableGold.cs:17-43` | 21 849 654 zl miasta i wsie razem [P]; podzialu nie znam | ok. 6,5 tys. na notabla [S] | zostaje; gra tnie sile, nie zloto | NIE lupimy (rozdz. 3.1) |
| 9 | Karawany stojace w miescie | gra `Town.cs:425`; `PartyDiplomaticHandlerCampaignBehavior.cs:75-94` | kasy karawan 2 981 924 zl, 559 karawan [P]; ile stoi w danym miescie - nie wiem | nie wiem | nie walcza, nie sa lupione; wrogie nowemu panu wyjezdzaja z towarem | bez zmian teraz (lista "na slowo") |
| 10 | Skarbiec korony | `Kingdom.KingdomBudgetWallet` | 27 108 168 zl [P] | nie lezy w zadnym miescie | zdobycie stolicy nic z niego nie bierze | bez zmian teraz (pytanie 5) |
| 11 | Kiesa pana | `Hero.Gold` | glowy rodow 47 229 257, pozostali lordowie 10 436 306 [P] | idzie z bohaterem | w bitwie 5% (do 10 000); pojmany - okup | bez zmian (lup z bitwy) |
| 12 | Ludzie | ludnosc BK; nasza ksiega ludzi | ludzie miast w ksiedze 10 357 752 [P] | - | BK -10..-24%; nasza ksiega bez zmian (D4) | krok 3 pozniejszy: jedno liczydlo |
| 13 | Budowle i dobrobyt | gra | dobrobyt miast 624 711 [P, R2] | - | spalone, bez odbiorcy | bez zmian; blizna tylko przy grabiezy i spustoszeniu |
| 14 | Unikaty na polce (stal valyrianska: 6 kling na targach [P, R2]) | A `UniqueSpoils.cs` | - | - | zostaja na polce | krok 4: do wodza armii (do planu kodu) |

Dwa wnioski z liczb:
1. **Prawdziwe bogactwo miasta to pieniadze mieszczan, nie kasa kupcow**: 44,0 mln wobec 11,0 mln, a nadwyzka kupcow, do ktorej siega dzisiejszy lup, to 2,75 mln -
   6% monety miast [P]. Uwaga: po wejsciu daniny z oszczednosci (projekt obiegu kiesy, P1) kiesy miast spadna do ok. 15,6 mln [S z tamtego projektu] - nadal wiecej niz kasy kupcow.
2. **W oblezonym miescie mieszczanie nie maja "oszczednosci ponad zapas"**: w trzech zdobytych miastach cala kiesa wyrobnikow miescila sie w zapasie (2-5 dni pelnego
   koszyka [S]) - oblezenie zjada oszczednosci (Saltpans: 71 402 -> 56 162 w dwa tygodnie [P]). Regula "tylko ponad zapas" dalaby wiec z mieszczan zero dokladnie wtedy, gdy miasto pada.

---

## 3. Regula - szczegoly

### 3.1 Regula w jednym zdaniu

**W osadzie wzietej szturmem wojsko bierze czesc kazdej monety, ktora tam lezy, i przez czas lupienia samo w niej nie placi; czesc zalezy od wyboru zdobywcy:
laska 0, grabiez 1/3, spustoszenie 2/3. Nic nie powstaje i nic nie znika - ile zeszlo osadzie, tyle dostaja zwyciezcy.**

| Zbiornik | Laska | Grabiez | Spustoszenie | Krok |
|---|---|---|---|---|
| Kasa kupcow (cala, takze zapas) | 0 | 1/3 | 2/3 | 1 |
| Pieniadze mieszczan: kiesy W, R, P i gmina; zamek: podzamcze (cale, takze zapas na chleb) | 0 | 1/3 | 2/3 | 1 |
| Zold wojska zostawiany w tym miescie ("zycie", markietani) | jak zwykle | wstrzymany 3 doby | wstrzymany 3 doby | 2 |
| Towar z polek bez zywnosci, konie | 0 | 1/3, do udzwigu; reszta zostaje | 2/3, do udzwigu; reszta z tej czesci plonie | 4 |
| Zywnosc z polek | 0 | najwyzej 10 dni racji armii | najwyzej 10 dni racji armii | 4 |
| Nasza blizna (dobrobyt -15% reszty, lojalnosc -15) | **0** | jak dzis | jak dzis | 1 |
| Kara gry (dobrobyt, budowle, lojalnosc, sila notabli), zabici i stabilnosc BK | jak dzis | jak dzis | jak dzis | - |
| Kwota gry "15 x utracony dobrobyt" | - | **wylaczona** | **wylaczona** | 1 |

Czego NIE lupimy i dlaczego:
- **Zywnosc warowni** (liczba FoodStocks) - z niej zyje miasto i nowa zaloga; wojsko w osadzie i tak ja uszczupla (-8..-15% [P]). Spalenie jej to glod bez odbiorcy.
- **Zloto notabli** - jest spiete z kasa miasta pasmem 4 500-10 500 (A `NotableGold.cs:17-33`): zabrane notablowi zostaloby mu dosypane z nadwyzki tej samej kasy -
  drugi pobor z tego samego zbiornika.
- **Kapital warsztatow** - zostaje z warsztatem; o wlascicielu decyduje BK (pkt 1.5).
- **Skarbiec korony, kiesa pana** - nie leza w miescie. **Karawany** - nie biora udzialu w bitwie; wrogie wyjezdzaja (bez zmian; pomysl na liste "na slowo").
- **Zbrojownia i kiesa zalogi, jency, sakiewki obroncow** - to lup z bitwy, ma juz swoje reguly (bez zmian poza naprawa D3).

### 3.2 Parametry z rachunkiem (wybor autora; wszystkie do zmiany jednym kluczem)

**Dlaczego czesc CALEJ monety, a nie "nadwyzki ponad zapas".**
(a) Nadwyzka kupcow jest juz dzis wybierana do dna pod nazwa "jency": w 14 dobach zdobyc gra chciala 604 818 zl, nadwyzki daly 399 411, uciete 205 407 [P].
Nowa regula siegajaca tylko do tej samej nadwyzki nie dalaby ani zlotowki wiecej - zmienilaby kolejnosc (W rozdz. 4 pkt 7).
(b) Mieszczanie padajacego miasta nie maja nic ponad zapas (rozdz. 2 wniosek 2).
(c) Czesc calosci nie zalezy od dobrobytu ani od kolejnosci zdarzen: ten sam stan osady daje te sama kwote u AI (nastepstwa przed zmiana pana) i u gracza (po niej) - D6 znika z definicji.
(d) Jedna liczba na wybor - gracz widzi ja w oknie i rozumie.

**Dlaczego 1/3 i 2/3.**

| Sprawdzenie | Grabiez 1/3 | Spustoszenie 2/3 | Zrodlo |
|---|---|---|---|
| Kupcy trzech miast po lupie, wobec zapasu kupcow PO spadku dobrobytu | Saltpans 80 257 wobec 52 657; Starfall 80 150 wobec 51 499 - ponad zapasem | Harrenhal 69 954 wobec 47 250 - ponad zapasem | [S z P, a2_regula.py] |
| Kupcy mediany miast z konca roku (kasa 97 383, zapas 85 207) | zostaje ok. 65 tys. - przy nowym zapasie | zostaje ok. 32 tys. - ok. 25 tys. ponizej zapasu: renta i danina staja do odbudowy | [S] |
| Mieszczanie trzech miast (zapas 2,0-5,5 dnia pelnego koszyka) | zostaje 1,3-3,7 dnia | zostaje 0,7-1,8 dnia | [S, a5_dni.py] |
| Lup na czlowieka armii (dzis Harrenhal 1,9 zl) | Saltpans 16,6; Starfall 13,2; zamki 7-26 | Harrenhal 16,7; Willow Wood 17,4 | [S z P] |
| Lup w dobach zoldu armii (4,86 zl na czlowieka i dobe [S, R3]) | 1,5-5,4 doby | 3,4-3,6 doby | [S] |
| Miasto z konca roku (moneta srednio 564 306, mediana 321 870), armia 5 000 | 188 / 107 tys. = 7,7 / 4,4 doby zoldu | 376 / 215 tys. = 15,5 / 8,8 doby | [S z P] |
| To samo po daninie P1 (kiesy miast ok. 15,6 mln / 97) | ok. 90 tys. | ok. 181 tys. | [S] |

Wniosek: 1/3 i 2/3 daja armii od kilku dni do dwoch tygodni zoldu (historycznie lup splacal zaleglosci, nie finansowal wojny), nie zeruja miasta i nie schodza
kupcom ponizej zapasu w przypadkach zmierzonych. Skala jest zgodna z tym, co juz robi gra i BK (dobrobyt -12..-35%, zabici 10-24%).
Mniejsze czesci (np. 1/4 i 1/2) zmienilyby lup proporcjonalnie; wieksze niz 2/3 odradzam - mieszczanie zostaliby z mniej niz dniem chleba.

**Dlaczego 3 doby bez placenia.** Historyczny zwyczaj "trzech dni lupu". W rachunku: lump "zycia" przy wyjezdzie armii to w zmierzonych miastach 81-534 tys. zl [S] -
od 1,4 do 3 razy wiecej niz sam lup wedlug reguly (59-175 tys.). Bez wstrzymania miasto po spustoszeniu konczy dobe bogatsze niz przed szturmem (Harrenhal:
-175 tys. lupu, +534 tys. zoldu). Po 3 dobach wszystko wraca do zwyklych regul: druzyna, ktora zostala albo wrocila, placi jak zawsze; markietani znow moga kierowac
pieniadz do tego miasta. Czy 3 doby wystarcza, zeby pieniadz naprawde wyjechal - nie wiem; mierzy to krok 0 (ile "zycia" i markietanow trafilo do zdobytej osady w dobach 0-3 i 4-30).

**Blizna.** Zostaje 15% reszty dobrobytu i -15 lojalnosci, ale tylko przy grabiezy i spustoszeniu, nakladana w chwili wyboru (nie przy zmianie pana) - wtedy AI i gracz
maja ten sam wynik, a laska kosztuje miasto tylko kare gry (-0,5 x B, czyli 6-11% [S ze wzoru]).

### 3.3 Kto dostaje

1. **Pula** = suma zdjeta ze zbiornikow: z kazdego floor(czesc x stan w tej chwili).
2. **Odbiorcy** = druzyny ruchome strony zwycieskiej - te same, ktorym dzis placi gra (G `SiegeAftermathCampaignBehavior.cs:84-91`). Przy pustej liscie
   ("Right of Pillage") - druzyna, ktora lupi. Zwyciezcy Inni (White Walkers) nie biora monety - ta sama regula co przy kiesach rozbitych partii
   ("trup monety nie bierze", A `CasualtyLoot.cs:22-23`); wtedy lupu nie ma i moneta zostaje w osadzie.
3. **Podzial miedzy druzyny: wedlug liczby ludzi** (zdrowi i ranni w skladzie po bitwie). Reszta z zaokraglen - do najwiekszej druzyny (D8 znika: nic nie przepada).
4. **W druzynie - prawo trzecich**, jak przy sakiewce rozbitej partii i lupie z cial (A `MenPurse.cs:19-20`, :119; `CasualtyLoot.cs:110-117`):
   2/3 do sakiewki ludzi druzyny, 1/3 wodzowi (druzyna bez wodza - do kiesy druzyny, jak gra :169).
5. **Korona**: trzecia wodza wchodzi do podstawy prawa trzecich (A `Ransom178.cs:624-625`, :704) - korona krolestwa w wojnie bierze 1/3 tej trzeciej nastepnego ranka
   (nie od najemnikow i nie od krola-gracza: `Ransom178.cs:615-622`). D11 znika.
6. **Gracz - ta sama regula**: 2/3 do sakiewki jego ludzi, 1/3 do jego kiesy; jako wasal w wojnie oddaje koronie 1/3 swojej trzeciej.
7. **Co zolnierze robia z lupem** - to, co z zoldem (decyzja Jeffa 05.10, A `MenPurse.cs:13-28`): naprawy, braki w kompletach, odlozone na lepszy sprzet
   (A `Settings.cs:192-193`), reszta "na zycie" w miescie przy wyjezdzie (75% kupcy, 25% lud) i u markietanow w polu (A `Settings.cs:196-197`).
   Czyli lup z jednego miasta trafia po kilku dobach do kupcow i mieszczan innych miast - glownie w kraju zdobywcy.

Z 100 zl lupu: zolnierze 66,7, wodzowie 22,2, korony 11,1 (gdy krolestwo w wojnie). Na 14 zdobyciach z biegu: 310 tys. / 103 tys. / 52 tys. zl [S].

Uwaga o spojnosci: zloto z rabunku WSI idzie dzis w calosci do wodza napastnikow (A `SmallGold.cs:377-384`), nie wedlug trzecich. Dla miasta proponuje trzecie,
bo to lup z domow (bierze go zolnierz), tak jak sakiewki i lup z cial. Wyrownanie wsi do tej samej reguly - osobna mala sprawa, nie w tym projekcie.

### 3.4 Jak wybiera AI

Dzis: los wedlug cechy wodza (pkt 1.2). Proponowana kolejnosc (dopisana do wyboru gry, granice gry zostaja):

1. Osada tej samej kultury co krolestwo zdobywcy -> **laska** (bez zmian; gracz ma te sama blokade).
2. Wodz zabity albo ranny w bitwie -> jak w grze.
3. **Nie ma czego brac**: lup z grabiezy mniejszy niz jedna doba zoldu armii -> **laska** (nie niszczy sie osady za nic).
4. **Armia w potrzebie**: ktoras druzyna armii ma dzis niezaplacony zold (pole gry `MobileParty.HasUnpaidWages`, G `...GameComponents\DefaultClanFinanceModel.cs:966-980`)
   albo kiesa wodza armii nie starcza na 14 dob zoldu jego druzyny -> **grabiez**; wodz z ujemna cecha Mercy, jesli gra pozwala (wplywy, morale ponizej 90) -> **spustoszenie**.
5. W pozostalych przypadkach -> wybor gry (cecha, wplywy, morale).

To zachowanie "z biezacej potrzeby": kto nie ma czym zaplacic wojsku, placi mu miastem; kto ma - moze zostawic miasto cale dla nowego pana.
Jak czesto wyjdzie grabiez - nie wiem (w biegu 111 z 332 rodow bylo na pulapie zoldu, ale wodzow armii obleznicznych wsrod nich nie liczylem); zmierzy to krok 0.

### 3.5 Co widzi gracz (napisy po angielsku; kwoty z zywych zbiornikow w chwili otwarcia okna)

Okno gry z trzema opcjami zostaje (G `SiegeAftermathCampaignBehavior.cs:53-56`). W podpowiedzi kazdej opcji linie gry "Army Gold Gain" i "Party Gold Gain" (:588-601)
zastapione prawdziwymi:

- Pillage: `The sack: a third of all coin in {TOWN}.` / `Merchants' coin: 120,386 - the army takes 40,129.` / `Townsfolk's coin: 56,162 - the army takes 18,721.` /
  `Your party (1,240 of 3,549 men): 20,560 - your men 13,707, you 6,853 (the crown will claim 2,284 of your part).` /
  `Afterwards: the merchants keep 80,257, the townsfolk 37,441. For three days your men will take what they want here and pay for nothing.`
- Devastate: to samo z `two thirds`.
- Show Mercy: `No sack: the merchants keep 120,386 and the townsfolk 56,162. Your men get nothing from the town - they will spend their pay here as in any town.`
- Osada juz zlupiona: `Little is left here: {TOWN} was sacked 12 days ago.`

Po wyborze (podsumowanie gry :615-647, dopisana linia): `Your army took 58,850 coins from Saltpans: 40,129 from its merchants, 18,721 from its townsfolk. Your men's purses grew by 13,707; your own part is 6,853.`
Gracz w cudzej armii: `{LEADER} let the army sack {TOWN}. Your party's part: 4,120 coins (your men 2,747, you 1,373).` albo `{LEADER} spared {TOWN}. There is no plunder.`
Gracz-pan zlupionej osady: `{TOWN} has been sacked: its merchants lost 139,907 coins, its townsfolk 34,630.`
Wyjazd z lupionego miasta: `Your men are still gorged on the sack of {TOWN} - they spend nothing here.`
Okno mieszkancow ("People of this land"), przez 30 dob: `Sacked 12 days ago: the merchants lost 40,129 coins, the townsfolk 18,721.`
MCM (po `gen_mcm.py`): `Sack Takes Real Coin`, `Sack Share Pillage`, `Sack Share Devastate`, `Sack No Spending Days`, `No Sack Without Capture`, `Beaten Garrison Purse To Victor`,
`AI Sacks From Need`, `Sack Scar Only When Sacked`.
Przy okazji okna: pole pamieci dobrobytu gry (D9) ustawiane na -1 przed otwarciem menu - linia "Prosperity Lost" liczy wtedy od wlasciwej osady.

### 3.6 Co potem z miastem

| Co | Laska | Grabiez | Spustoszenie | Skad wiem |
|---|---|---|---|---|
| Kasa kupcow | cala; rosnie od zoldu wojska (dzis: +66..+241 tys. w dobe [P]) | 2/3; przez 3 doby bez zoldu wojska; reszte nadwyzki wybiora jency | 1/3; jak obok | regula; [P] dla laski to dzisiejszy stan po kazdym wyborze |
| Renta nowemu panu i koronie (zawor 7% nadwyzki dziennie, A `Settings.cs:745`) | wysoka (dzis 91-513 tys. w 30 dob na miasto [P]) | bliska zera, dopoki kasa nie odrosnie ponad zapas | zero do odbudowy zapasu | [S] |
| Danina wojenna z kasy (1% nadwyzki dziennie, A `Settings.cs:951-954`) i z oszczednosci ludu (projekt P1) | plynie do nowej korony od nastepnej doby | zero, dopoki nie ma nadwyzki / oszczednosci ponad zapas | zero | [K] regul; [S] czasu |
| Pieniadze mieszczan | cale; rosna od zoldu wojska (x3 do x11 w miesiac [P]) | 2/3 | 1/3; mozliwe dni niedojadania ("zjedzone bez zaplaty" - traca kupcy, nikt nie umiera z braku monety) | [S]; do pomiaru |
| Skup od karawan i taborow wsi | bez zmian | slabszy do odbudowy kasy | wyraznie slabszy - tabory okolicznych wsi moga wracac z towarem | [S]; do pomiaru (licznik "towar wsi niesprzedany" juz jest w LOG) |
| Ceny | nie wiem - brak tabeli cen na miasto | nie wiem | nie wiem | - |
| Dobrobyt | -0,5 x B (6-11%) | -B, potem -15% reszty | -1,5 x B, potem -15% reszty (Harrenhal -43% [P]) | [K], [P] |
| Lojalnosc | 0 | -15 gra, -15 blizna | -30 gra, -15 blizna | [K] |
| Zabici (BK), stabilnosc BK | 0; -0,10 | 10-16%; -0,25 | 16-24%; -0,45 | [K] |
| Czas odbudowy dobrobytu | tygodnie | Saltpans ok. 230 dob [S z tempa +5 pkt na dobe] | Harrenhal ok. 570 dob [S] | R3 rozdz. 8 |
| Czas odbudowy monety | - | nie wiem; zalezy od tego, czy wojsko zostaje w okolicy po 3 dobach | nie wiem | do pomiaru |

### 3.7 Dowod zamknietego obiegu

Kazdy ruch ma platnika i odbiorce. Nowych posiadaczy zlota nie ma. Kontrole bez zmiany definicji: "Pieniadz swiata (bilans)", "Kiesa ludu: zmiana kies = wplywy - wydatki",
okno O10 (ma dalej pokazywac 0 zlota z niczego), "Towary (bilans)".

| Ruch | Platnik (zdjete) | Odbiorca (dopisane) | Towar |
|---|---|---|---|
| Lup z kasy kupcow | kasa osady (Town.Gold): czesc x stan | pula lupu tej osady | - |
| Lup z pieniedzy mieszczan | kiesy W, R, P, gmina / podzamcze: czesc x stan (przez ksiege kies - kontrola zamknieta) | pula lupu | - |
| Pula -> zolnierze | pula | sakiewki ludzi druzyn (2/3 udzialu druzyny) | - |
| Pula -> wodzowie | pula | kiesa wodza (1/3 udzialu); druzyna bez wodza - jej kiesa | - |
| Reszta z zaokraglen | pula | najwieksza druzyna | - |
| Wodz -> korona | kiesa wodza, nastepnego ranka | skarbiec jego krolestwa: 1/3 trzeciej (istniejaca droga prawa trzecich) | - |
| Kwota gry 15 x dobrobyt | wylaczona - 0 | - | - |
| Zold "na zycie" w lupionym miescie | nikt - pieniadz ZOSTAJE w sakiewce ludzi | wydany pozniej wedlug zwyklych regul (inne miasto, markietani) | - |
| Kiesa zalogi rozbitej w bitwie pod murami (naprawa D3) | kiesa zalogi | zwyciezcy bitwy (regula kies rozbitych partii, A `BeatenPurse.cs`) | - |
| (krok 4) towar i konie | polka osady: czesc x sztuki | tabory druzyn do wolnego udzwigu | sztuka zdjeta z polki = sztuka w taborze; grabiez: nadmiar zostaje na polce; spustoszenie: nadmiar z tej czesci spalony z wpisem w ksiedze towarow (jak "spalone w rabunku" wsi, A `DistrictRaid.cs:41-43`) |
| Jency sprzedani osadzie | jak dzis: nadwyzka kasy ponad zapas | wodz sprzedajacego | ludzie -> "do domu" |

Moneta nigdy nie plonie i nigdy nie powstaje. Jedyne ujscie w nicosc w calej regule to towar spalony przy spustoszeniu (krok 4) - zjawisko fizyczne, ksiegowane
tak samo jak spalony towar wsi, zaakceptowany 07.10.

### 3.8 Ile to zlota w skali swiata

| Pozycja | Kwota | % rocznego zoldu druzyn lordow (161 794 836 zl [P]) | Znacznik |
|---|---:|---:|---|
| Dzis: lup z oblezen (rok) | 64 728 | 0,04% | [P] |
| Dzis: jency sprzedani w 14 dobach zdobyc (swiat) | 399 411 | 0,25% | [P] |
| Regula, te same 14 zdobyc i te same wybory AI (7 lask) | 465 406 (kupcy 295 216, mieszczanie 170 191) | 0,29% | [S z P] |
| Regula, gdyby wszystkie 14 grabiez | 502 031 | 0,31% | [S] |
| Regula, gdyby wszystkie 14 spustoszenie | 1 004 063 | 0,62% | [S] |
| Zwyciezcy razem (lup + jency): dzis -> po regule | ok. 454 tys. -> ok. 700 tys. | 0,28% -> 0,43% | [S] |
| Cala moneta 14 zdobytych osad | 1 506 094 | 0,93% | [P] |
| Zold wojska zostawiony w 3 zdobytych miastach ponad zwykla dobe | ok. 732 tys. | 0,45% | [S z P] |
| Renta nowym panom i koronom z 3 miast, 30 dob po zdobyciu | 780 074 | 0,48% | [P] |
| Daniny wojenne juz dzis (kasy osad i kiesy wsi 73 627 963 + podatek wojenny ludu 3 882 375) | 77 510 338 | 47,9% | [P, W] |

Rachunek "zwyciezcy razem" [S]: lup 465 tys. + jency w 7 osadach oszczedzonych 136 tys. (jak dzis) + jency w 7 zlupionych - tylko to, co zostanie ponad zapas po lupie
(miasta ok. 74 tys. zamiast 204 tys.; zamki ok. 23 tys. zamiast 59 tys.). Sprzedaz jencow w zlupionej osadzie spada, bo kasa ma mniej na wykup; jency ida wtedy "do domu" taniej albo za darmo.

Granica z gory: w roku pada ok. 6% osad (14 z 227). Nawet zabierajac CALA monete kazdej z nich, lup roku to ok. 1,5 mln zl (osady z tego biegu [P]) do ok. 2,4 mln [S: 3 miasta x 564 tys. + 11 zamkow x 67 tys.
przy stanach z konca roku] - 1-1,5% zoldu. Lup nie moze byc filarem finansow wojny w zadnym wariancie; jest zdarzeniem lokalnym.

---

## 4. Sto procent przypadkow

| Przypadek | Co sie dzieje wedlug reguly |
|---|---|
| Miasto wziete szturmem przez AI | wybor AI (3.4) -> lup z kasy kupcow, kies W / R / P i gminy -> podzial (3.3) -> zmiana pana -> 3 doby bez placenia (grabiez, spustoszenie) |
| Zamek | ta sama regula: kasa zamku i kiesa podzamcza; brak klas R / P i gminy; ciecia sily notabli nie ma (jak w grze :141). Wstrzymanie zoldu nie ma tu czego wstrzymywac: ani "zycie" (A `MenPurse.cs:279`), ani markietani (A `Sutlers.cs:22`) nie kieruja pieniadza do zamku |
| AI zdobywa osade AI | jak wyzej; nikt nic nie widzi poza logiem i wiadomoscia BK o zabitych |
| Gracz-wodz zdobywa | okno z trzema opcjami i prawdziwymi kwotami (3.5); regula liczona od stanu osady w chwili wyboru; ten sam wynik co AI (blizna w chwili wyboru) |
| Gracz w cudzej armii / gracz-pomocnik spoza armii | wybiera wodz AI; druzyna gracza dostaje udzial wedlug ludzi jak kazda inna (gra placi kazdej druzynie z listy - :159-170); wiadomosc z kwota |
| Gracz broni i przegrywa | regula ta sama; jego wlasna kiesa i bagaz - reguly bitwy (5% do 10 000, bez zmian); jako pan dostaje wiadomosc, ile miasto stracilo; jego sklad gra wyrzuca na targ (G `...CampaignBehaviors\SettlementClaimantCampaignBehavior.cs:60-68`) - w kroku 4 ten towar lezy na polce jak kazdy inny |
| Gracz-pan nieobecny, miasto pada | wiadomosc `{TOWN} has been sacked: ...`; po odbiciu znajdzie tyle monety, ile zostalo |
| Wygrana bitwa pod murami bez zdobycia (SiegeOutside) | **zadnych nastepstw**: ani kary gry, ani zabitych BK, ani lupu (naprawa D2). Kiesa rozbitej zalogi - do zwyciezcow bitwy (naprawa D3) |
| Przegrany wypad obroncow | liczy sie jak zdobycie (jak w grze :79-80) - pelna regula |
| Obrona odparta, oblezenie zwiniete | nic. Osada nie placila pana przez oblezenie (BesiegedPay) - pieniadz zostal w kasie |
| Pusta warownia (szturm bez walki) | zwykla sciezka gry (G `...CampaignSystem\EncounterManager.cs:254-268`) - regula z wyborem; w biegu 0 przypadkow |
| Miasto przejete bez szturmu: dar, wymiana, decyzja krola, pokoj, odejscie rodu, wydarzenia fabuly ROT, blizniaki (Red Keep -> King's Landing) | **bez lupu** - nie bylo szturmu; zaloga, kasa i kiesy zostaja (G `ChangeOwnerOfSettlementAction.cs:34-37, 97-143`); wojsko placi jak zwykle. King's Landing moze wiec zmienic pana bez lupu, gdy padnie Red Keep - zostawiam, bo to regula fabuly ROT |
| Bunt | bez lupu: buntownicy to mieszczanie. Przywodca dostaje do 50 000 zl z kasy zbuntowanej osady (juz zamkniete: A `RotLeaks37.cs:24-26`) |
| Miasto zbuntowane brane potem szturmem | zwykla regula |
| Miasto zlupione miesiac temu | ta sama czesc z tego, co JEST - czyli malo; AI przy lupie ponizej doby zoldu wybiera laske; gracz widzi `Little is left here` |
| Osada zdobyta drugi raz w tej samej dobie | druga czesc z reszty (po 2/3 zostaje 1/3, po kolejnych 2/3 - 1/9); wstrzymanie zoldu liczone od ostatniego lupu |
| "Right of Pillage" (gracz okazal laske, ludzie zadaja lupu) | opcja "sack the town" = grabiez wedlug tej samej reguly: 1/3 monety osady (juz gracza), odbiorca - druzyna gracza (2/3 ludzie, 1/3 gracz); kary gry jak dzis (naprawa D7) |
| Zdobywca to Inni (White Walkers) | monety nie biora; osada zmienia kulture wedlug ROT (`ROTOthersCampaignBehavior.cs:316-324`); pieniadz zostaje |
| Zdobywca bez krolestwa albo najemnik | lup jak wszyscy; korona nie bierze nic (prawo trzecich ich nie obejmuje) |
| Druzyna zwycieska bez wodza | jej czesc "wodza" do kiesy druzyny (jak gra :169) |
| Sakiewki ludzi wylaczone (`MenPurseEnabled`) | calosc udzialu druzyny wodzowi (stan jak w grze); wstrzymanie zoldu nie ma czego wstrzymywac |
| Kiesa ludu wylaczona (`TownFolkLive`) | lup tylko z kasy kupcow |
| Regula wylaczona kluczem | stan sprzed projektu co do znaku: kwota gry przez RealPayer, blizna przy zmianie pana |
| Wies | bez zmian - ma wlasna regule rabunku osada po osadzie (A `DistrictRaid.cs:22-63`): udzial z kiesy wsi i magazynu, do udzwigu, reszta spalona. Miasto dostaje TE SAMA zasade ("tylko z tego, co lezy; udzial; udzwig"), z udzialem ustalanym wyborem zdobywcy zamiast liczba trafionych |
| Stary zapis | regula dziala od pierwszego zdobycia po wczytaniu; nic do migracji (krok 1 bez nowych pol zapisu; krok 2 - jedno pole: do ktorej doby osada jest lupiona) |

---

## 5. Zgodnosc z innymi regulami

### 5.1 Tabela

| Regula | Stan | Zgodnosc |
|---|---|---|
| **Zapas kupcow nietykalny** dla zaworu, podatkow i naszych wyplat (decyzja 11.10, A `TownPurse.cs:221-239`) | wgrane | **Wyjatek do Twojej decyzji (pytanie 1).** Lup to akt wojny, nie podatek. Osobny licznik w logu ("kasa ponizej zapasu przez lup"), zeby kontrola RealPayer (prog 0, A `RealPayer.cs:108`) dalej pilnowala zwyklych wyplat |
| **Kiesa ludu - drabina nadwyzki** (`docs\kiesa-ludu\PROJEKT-OBIEG-KIESY-LUDU-2026-10-13.md` rozdz. 3.1: chleb -> zapas -> zbytki -> budowa -> danina) | projekt, P1 w kodzie na galezi | Te same skrzynie. Drabina dziala co dobe i NIE rusza zapasu; lup dziala raz, w dniu szturmu, i bierze czesc CALOSCI - to jest wlasnie "czarna godzina", na ktora zapas byl. Po lupie mieszczanie sa ponizej zapasu, wiec szczeble 2-4 (zbytki, budowa, danina) same stoja do odbudowy. Tej samej monety nikt nie bierze dwa razy: oblezona osada daniny nie placi (bramka), a po zdobyciu danina liczy sie od tego, co zostalo |
| **Danina wojenna z kas osad** (1% nadwyzki dziennie; A `KingdomTreasury.cs:399-412`) | wgrane | Podstawa ta sama (nadwyzka ponad zapas). Po lupie nadwyzki nie ma - korona zdobywcy dostaje swoje 1/9 od razu, a danine dopiero po odbudowie kasy. Przy lasce danina plynie od nastepnej doby |
| **Zawor kasy miasta** (7% nadwyzki dziennie, pan i korona) | wgrane | Bez zmian w kodzie; skutek w 3.6 |
| **Prawdziwy platnik (RealPayer)** - lup z oblezenia z nadwyzki osady | wgrane | Kontekst "lup z oblezen" przestaje miec co robic (kwota gry = 0). UWAGA do planu kodu: nasze wyplaty lupu nie moga isc zdarzeniem "nic -> wodz" wewnatrz tej metody gry, bo RealPayer przycialby je do nadwyzki (A `RealPayer.cs:80-105`) |
| **Okno "lup z oblezen" (O10)** | wgrane | Zostaje jako kontrola: wywolan n, trafien z niczego 0 |
| **Jency sprzedawani osadzie** z nadwyzki | wgrane | Bez zmian w regule. Skutek: w zlupionej osadzie wykup jencow maleje (3.8). Do rozstrzygniecia po pomiarze kroku 0: zostawic, czy dopisac "AI nie sprzedaje jencow osadzie bez nadwyzki" |
| **Lup z bitwy**: kiesy pokonanych, sakiewki ludzi, lup z cial, sprzet zalogi, unikaty | wgrane | Bez zmian. Lup miasta to osobne zjawisko (osada, nie partia) z ta sama zasada podzialu |
| **Prawo trzecich** (korona 1/9) | wgrane | Lup miasta wchodzi do tej samej podstawy - koniec wyjatku D11 |
| **Kiesa starej zalogi -> nowa zaloga** | wgrane | Bez zmian przy zdobyciu; dochodzi tylko przypadek zalogi rozbitej w bitwie pod murami (D3) |
| **Oblezona osada nie placi nic** | wgrane | Bez zmian. Pieniadz wstrzymany w oblezeniu lezy w kasie w chwili szturmu - i slusznie jest czescia lupu |
| **Blizna po szturmie** (decyzja 31.08) | wgrane | Zmiana: tylko przy grabiezy i spustoszeniu, w chwili wyboru (pytanie 4) |
| **BannerKings: ludnosc i stabilnosc** | cudzy kod | Bez zmian. Przy bitwie pod murami bez zdobycia BK juz nikogo nie zabije (nastepstw nie ma). Krok 3 pozniejszy: ta sama czesc ludzi schodzi z naszej ksiegi (D4) |
| **BannerKings: warsztaty** | cudzy kod | Bez zmian; brak linii w naszym logu - dopisac pomiar w kroku 0 |
| **Rabunek wsi osada po osadzie** | wgrane | Ta sama zasada; roznica w podziale zlota (3.3 uwaga) |
| **Zycie zolnierzy w miescie i markietani** (decyzja 05.10) | wgrane | Wstrzymanie na 3 doby tylko w osadzie lupionej i tylko dla wojska krolestwa zdobywcy; pieniadz zostaje w sakiewce |
| **Diplomacy** (pytanie o lenno po lupie), **ROT** (sluzba u lorda pomija menu) | cudzy kod | Bez kolizji: nie zmieniamy przelaczania menu, tylko tekst podpowiedzi i podsumowania (CLAUDE.md rozdz. 7: zadnego `SwitchToMenu` z naszego kodu) |

### 5.2 Lup a pieniadze na wojne (prosba Jeffa o podatki wojenne)

- Lup z oblezen dzis: 64 728 zl w roku przy zoldzie druzyn lordow 161,8 mln i zalog 66,6 mln [P]. Po regule: 0,5-1,0 mln [S]. To 0,3-0,6% zoldu - nie jest to zrodlo.
- Nawet cala moneta wszystkich zdobytych osad to 1-1,5% zoldu (3.8). Zadna regula lupu tego nie zmieni, bo w roku pada 14 z 227 osad.
- Co lup robi dla wojny naprawde: (a) armia, ktora wygrala, dostaje od kilku dni do dwoch tygodni zoldu - u zolnierzy, nie w budzecie pana; (b) wodzowie 22%, korona 11%;
  (c) AI w potrzebie lupi, zamiast tracic ludzi z niezaplaconego zoldu - maly, ale prawdziwy zawor bezpieczenstwa dla armii w polu.
- Gdzie naprawde leza pieniadze [P]: pieniadze mieszczan 44,0 mln (z tego ponad zapas 29,5 mln), kasy kupcow 11,0 mln (ponad zapasem 2,75 mln), skarbce krolestw 27,1 mln, glowy rodow 47,2 mln.
  Daniny wojenne juz dzis daja 77,5 mln rocznie. Odpowiedz na "gdzie zwiekszyc podatki na czas wojny" to projekt, ktory wybrales 13.10 (korona placi wedlug potrzeby, tarczowe,
  stawka wojenna podatku pana) razem z danina z oszczednosci ludu - nie lup.
- Jedno powiazanie warte zapamietania: **laska sie oplaca koronie**. Oszczedzone miasto placi nowej koronie danine i rente od nastepnej doby (3 miasta: 780 tys. zl w 30 dob [P]),
  zlupione - przez tygodnie nic. Im wiecej podatkow wojennych zalezy od miast, tym drozej kosztuje spustoszenie. AI nie liczy tego dzis i w tej propozycji tez nie (wodz patrzy na swoje wojsko, nie na skarbiec krola).

---

## 6. Paczki wdrozenia i progi testu

Jedna zmiana naraz (CLAUDE.md 8.2). Kazda paczka ma wlasny wylacznik przywracajacy stan sprzed niej. Kolejnosc wzgledem innych prac: po scaleniu paczki P1 daniny
(`w-toku/kiesa-p1-danina` rusza te same kiesy w A `TownFolk.Live.cs`) - nie rownolegle.

| # | Paczka | Tor | Tresc | Warunek wejscia |
|---|---|---|---|---|
| **L0** | Pomiar na sucho | **S** (sam log, zero zmian w grze) | Przy kazdych nastepstwach linia: osada, typ bitwy, wybor gry, stan zbiornikow (kasa, zapas, kiesy W / R / P / gmina, sztuki i wartosc polek, konie), ile wzielaby regula przy kazdym z trzech wyborow, ludzie druzyn, "armia w potrzebie tak / nie", wybor wedlug 3.4. Przez 30 dob po zdobyciu linia osady: "zycie" i markietani otrzymane w dobach 0-3 i 4-30, jency, zawor, kiesy, "stac jedzenie". Pomiar przejec warsztatow przez BK | - |
| **L1** | Bez zdobycia nie ma grabiezy | **S** | Nastepstwa pomijane po bitwie pod murami (D2); kiesa zalogi rozbitej w bitwie do zwyciezcow (D3). Dwa klucze | - |
| **L2** | Lup z monety | **M** (nowa regula pieniadza; 1 workflow z recenzja) | Rdzen 3.1-3.3 dla kasy kupcow, kies i gminy; kwota gry wylaczona; blizna wedlug wyboru i w chwili wyboru; "Right of Pillage" ta sama droga; wiadomosci tekstowe z kwotami; linia doby "Lup miast" | pytania 1, 2, 4; wynik L0 |
| **L3** | W lupionym miescie wojsko nie placi | **S** | 3 doby bez "zycia" i bez markietanow do tej osady dla wojska krolestwa zdobywcy; jedno pole zapisu | pytanie 3; pomiar L0 (ile pieniadza wraca) |
| **L4** | AI lupi z potrzeby | **S** | Kolejnosc 3.4 dopisana do wyboru gry | pytanie 4; rozklad z L0 |
| **L5** | Okno gracza | **M** (menu gry - ostroznie) | Podpowiedzi trzech opcji i podsumowanie z prawdziwymi kwotami (3.5), poprawka D9, linia w oknie mieszkancow. **Obowiazkowa przed pierwsza gra Jeffa** - bez niej okno pokazuje stare, nieprawdziwe "Army Gold Gain" | L2 |
| **L6** | Jedno liczydlo ludzi | **M** | Czesc ludnosci zabita przez BK schodzi z naszej ksiegi ludzi miasta jako zabici przy zdobyciu (D4); kiesy licza glowy po ubytku | osobne rozpoznanie: jak ksiega liczy ludzi samego miasta (droga `PeopleBook.Move` dziala dzis na wsi - A `PeopleBook.cs:849`) |
| **L7** | Towar i konie | **L** (pelny lancuch) | Czesc polek do udzwigu taborow, konie, zywnosc do 10 dni racji, ogien przy spustoszeniu, unikaty z polki, bron i zbroja do zbrojowni druzyn | pytanie 5; wyceny z L0 |
| na slowo | okup miasta zamiast szturmu (pomysl N3 z audytu 09.10); skarbiec korony w stolicy; zajecie karawan wroga w murach; lupienie osad wlasnej kultury przez armie bez zoldu; zloto z rabunku wsi wedlug trzecich | - | - | slowo Jeffa |

**Progi testu** (bieg 120 dob nowej kampanii po kazdej paczce; bieg roczny po L2-L4 razem). Progi sa niezmiennikami na zdarzenie - liczba zdobyc w biegu jest losowa (w biegu wzorcowym 7 w pierwszych 132 dobach).

| # | Co | Prog | Paczka |
|---|---|---|---|
| 1 | Linia pomiaru przy kazdych nastepstwach; liczba linii = liczba otwarc okna O10 | rowne; 0 ERROR | L0 |
| 2 | Nastepstwa po bitwie pod murami bez zdobycia | 0 (bylo 5 w roku) | L1 |
| 3 | Spadek dobrobytu i ludnosci BK w osadzie NIEzdobytej w dobie bitwy pod murami | 0 | L1 |
| 4 | Ujscie "kiesy partii bez wodza, ktore zniknely - garnizony" | 0 zl (bylo 16 249) | L1 |
| 5 | Kazdy lup: zdjete osadzie = dopisane odbiorcom | roznica 0 zl | L2 |
| 6 | Kazdy lup: zdjete z kazdego zbiornika <= czesc x stan; przy lasce 0 | bez wyjatku | L2 |
| 7 | "Lup z oblezen" w zrodlach z niczego; okno O10 trafien; wyplaty gry 15 x dobrobyt | 0; 0; 0 zl | L2 |
| 8 | "Pieniadz swiata" arytmetyka; kontrola kies ludu r | OK w kazdej dobie; 0 | L2 |
| 9 | Ten sam stan osady, kolejnosc AI i kolejnosc gracza (proba na prawdziwej DLL) | te same kwoty i ten sam dobrobyt koncowy | L2 |
| 10 | Blizna przy lasce | 0 | L2 |
| 11 | "Stac na jedzenie" w zlupionej osadzie, migawki 7 i 14 dob po | >= 0,95 (INFO w pierwszym biegu; ponizej - zmniejszyc czesc spustoszenia) | L2 |
| 12 | Kasa kupcow zlupionego miasta ponizej zapasu | INFO: ile dob; cel <= 30 | L2, L3 |
| 13 | "Zycie" i markietani do osady lupionej w dobach 0-3 od wojska zdobywcy | 0 zl | L3 |
| 14 | Sakiewki ludzi: wplynelo - wyszlo = zmiana stanu | roznica 0 | L3 |
| 15 | Osady tej samej kultury: laska; "nie ma czego brac": laska; wodz w potrzebie wobec innej kultury: grabiez albo spustoszenie | 100% / 100% / 100% | L4 |
| 16 | Napisy okna = kwoty w logu (test reczny: zdobyc zamek) | roznica 0 | L5 |
| 17 | Ubytek ludnosci BK w dobie zdobycia wobec ubytku ksiegi ludzi miasta (w procentach) | +-1 pkt proc. | L6 |
| 18 | Towar: zdjete z polek = dopisane taborom + spalone; spalone tylko przy spustoszeniu | roznica 0 szt. | L7 |
| 19 | Swiat (INFO, nie prog): wojsko lordow w wojnie, kasy miast, liczba zdobyc, rozklad wyborow | nie gorzej niz bieg bez paczki o wiecej niz rozrzut miedzy biegami | wszystkie |

Koszt dla szybkosci gry: pomijalny - kilkanascie zdarzen w roku; linia doby tylko w dobach ze zdarzeniem.

---

## 7. Ryzyka i czego nie wiem

**Ryzyka**

1. **Ta sama nadwyzka, trzech chetnych** (lup, jency, zawor). Po lupie wykup jencow w tej osadzie maleje; zwyciezcy lacznie zyskuja ok. 1,5 raza, nie 8 razy (3.8).
   Jesli pomiar L0 pokaze, ze jency ida "do domu" za darmo masowo - dopisac regule sprzedazy tylko tam, gdzie kasa moze zaplacic.
2. **Zapas kupcow naruszony** w miastach bez nadwyzki: slabszy skup od taborow wsi i karawan przez dni albo tygodnie; tabory moga wracac z towarem. Prog 12, licznik juz w logu.
3. **Chleb mieszczan po spustoszeniu**: zostaje 0,7-1,8 dnia pelnego koszyka [S]. Brak monety nie zabija (towar schodzi jako "zjedzone bez zaplaty", traci kupiec; zadowolenie BK spada), ale to
   moze przedluzyc odbudowe. Prog 11; w razie potrzeby czesc spustoszenia dla kies ludu 1/2 zamiast 2/3.
4. **AI z potrzeby lupi czesciej**: wiecej zabitych przez BK i dluzsze blizny na zdobytych ziemiach. Skala mala (kilkanascie zdobyc w roku), ale kierunek jest w dol dla dobrobytu swiata.
5. **3 doby bez placenia moga byc za krotkie**: po nich markietani znow kieruja 85% wydatkow do najblizszego miasta - czesto tego samego. Wtedy miasto odrasta w tydzien-dwa.
   To zmierzy L0 / L3; ewentualna zmiana to jedna liczba.
6. **Kolizje w kodzie do sprawdzenia przed L2 (CLAUDE.md 8.0)**: RealPayer na tej samej metodzie gry (5.1); okno O10; sluchacz BK tego samego zdarzenia; kontrola zamknieta kies
   ludu (zdjecie musi isc przez ksiege kies, nie obok); pulapka "osada placi wiecej, niz ma" (naglowek A `TownPurse.cs:41-49`, G24); blizna przenoszona z `WarLedger.OnOwnerChanged`
   na chwile wyboru - nie moze zadzialac dwa razy ani wcale; klucze `SackScar*` sa zapisane w `Armoury.json` Jeffa (CLAUDE.md 7 - zmiana domyslnej nie wystarczy);
   podstawa korony: w kodzie sa dzis dwie drogi dla "trzeciej wodza" (trzecie - korona 1/3, i zloto z kies - korona 1/9; A `Ransom178.cs:624-633`, `CasualtyLoot.cs:115-116`) -
   plan kodu ma wskazac jedna (proponuje trzecie, wtedy korona ma 1/9 calosci).
7. **Menu gry**: podpowiedzi i podsumowanie to prywatne metody gry z wlasna pamiecia (D9); kazdy blad tu to wywrotka u gracza w najwazniejszej chwili oblezenia.
   Dlatego L5 osobno, tor M, z testem recznym. Mozliwa wywrotka gry przy osadzie rodu bez glowy (G `SiegeAftermathCampaignBehavior.cs:148-156` - W rozdz. 4 pkt 12, nie sprawdzone do konca).
8. **Autotest nie widzi gracza**: bieg roczny to sam swiat AI. Sciezki gracza (okno, "Right of Pillage", gracz w cudzej armii, gracz-obronca) wymagaja testu recznego albo scenariusza z konsola.
9. **Trzy miasta to za malo na srednie**; Harrenhal to oblezenie skryptowe ROT z armia 55 druzyn - przypadek skrajny. Kazda liczba [S] w rozdz. 3 ma blad rzedu +-30%.
10. **Wyzysk przez gracza**: zdobyc - zlupic - oddac - zdobyc znowu daje za kazdym razem czesc reszty (malejaco), a kosztuje lojalnosc, dobrobyt i ludzi. Nie widze petli z zyskiem, ale nie testowalem.
11. **Krok 4 (towar)**: zbrojownie DTE, latka BK chowajaca towar zamku na czas lupu bitwy (BK `BannerKings.Patches\EconomyPatches.cs:439-466` - dotyczy lupu bitwy, nie nastepstw),
    unikaty na polce, udzwig i sprzedaz lupu przez AI. Dlatego tor L i dopiero po monecie.
12. **Paczka P1 daniny** zmieni stan kies ludu (mniej oszczednosci) - lup z mieszczan bedzie mniejszy niz w rachunku z konca tego biegu (3.2, ostatni wiersz).

**Czego nie wiem**

- Jak cala sciezka wyglada u zywego gracza (okno, kwoty, kolejnosc) - tylko z kodu.
- Wartosci towaru, koni i uzbrojenia na polkach pojedynczej osady; ile karawan i z jakim ladunkiem stoi w murach w chwili zdobycia.
- Cen w pojedynczym miescie przed i po zdobyciu; poziomow budowli i lojalnosci po zdobyciu (nie ma ich w tabelach biegu).
- Ile z "zycia" zolnierzy naprawde trafilo do zdobytej osady (linia logu to suma swiata; przypisanie do miasta jest szacunkiem).
- Jak czesto wodzowie armii oblezniczych maja niezaplacony zold - czyli jak zmieni sie rozklad wyborow AI.
- Ile razy BK przejal warsztaty przy zdobyciu (brak linii logu) i czy AIInfluence slucha zmiany pana.
- Jak nasza ksiega ludzi liczy mieszkancow samego miasta i jak ich zdjac zgodnie z ludnoscia BK (do rozpoznania przed L6).
- Czy wartosci `MenGearSavePercent` / `SutlerDailyShare` w pliku ustawien Jeffa sa takie jak w kodzie (50% / 10%) - od nich zalezy, ile lupu zolnierze wydadza od razu.
- Dlaczego przy Saltpans i Starfall gra policzyla mala armie (kara dobrobytu jak dla ok. 420 zdrowych) - wniosek o druzynach niedoczepionych do armii to szacunek W.
- Jak czesto u gracza trafia sie bitwa pod murami (5 przypadkow z jednego biegu AI).

---

## 8. Pytania do Jeffa - tylko kierunek

1. **Lup z tego, co lezy - takze z "nietykalnych" zapasow.** Czy zdobywcy maja brac czesc kazdej monety w osadzie (laska nic, grabiez trzecia czesc, spustoszenie dwie trzecie) -
   z kasy kupcow razem z ich zapasem i ze skrzyn mieszczan razem z pieniedzmi na chleb? Dotad zapas kupcow (decyzja 11.10) i zapas ludu byly nie do ruszenia przez zaden podatek.
   **Rada: TAK.** Lup to nie podatek, tylko wojna; bez tego w padajacym miescie nie ma czego brac (mieszczanie nie maja wtedy nic ponad zapas, a nadwyzke kupcow i tak wybieraja "jency").
   Glodu z braku monety nie bedzie - sprawdzimy to progiem w tescie.
2. **Kto dostaje.** Podzial miedzy druzyny wedlug liczby ludzi, a w druzynie "prawo trzecich": zolnierze 2/3 do sakiewek, wodz 1/3, z czego korona w wojnie trzecia czesc.
   Dzis caly lup miasta idzie do kiesy wodza. **Rada: TAK** - to ta sama zasada, ktora przyjales dla reszty lupu (05.10, 09.10); zolnierze wydadza to potem w miastach.
3. **W lupionym miescie wojsko bierze, nie placi.** Czy przez 3 doby po grabiezy albo spustoszeniu zolnierze zdobywcy maja nie zostawiac w tym miescie zoldu (wydadza go w nastepnym)?
   **Rada: TAK.** Bez tego zlupione miasto jest dobe pozniej bogatsze niz przed szturmem (Harrenhal: +241 tys. w kasie w dobie zdobycia), a wybor miedzy laska i grabieza nic nie znaczy.
4. **Laska ma byc laska, a AI lupi z potrzeby.** Przy lasce: zadnego lupu, bez naszej "blizny" -15% (zostaje tylko kara gry), wojsko placi w miescie jak zwykle. AI wybiera grabiez,
   gdy jego wojsko ma niezaplacony zold albo kiesa wodza nie starcza na dwa tygodnie; swoich (ta sama kultura) oszczedza jak dzis; pustej osady nie lupi. **Rada: TAK.**
5. **Co dalej, a co nie teraz.** (a) Towar i konie z polek ta sama czescia, do udzwigu taborow, z ogniem przy spustoszeniu - jak we wsiach: **rada TAK, ale jako drugi, osobny etap po monecie**
   (wieksze ryzyko, najpierw wyceny z pomiaru). (b) Skarbiec korony przy zdobyciu stolicy, okup miasta zamiast szturmu, zajecie karawan wroga w murach: **rada POZNIEJ, tylko na Twoje slowo** -
   kazdy z nich zmienia rownowage krolestw bardziej niz caly ten projekt.
