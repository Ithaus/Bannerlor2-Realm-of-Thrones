# 10. Czego nie mamy, a co moglibysmy wgrac - "cos, na co nie wpadlismy" (audyt 09.10.2026)

Tylko odczyt. Kod = wersja w grze (Armoury c01a54ba = commit 2e235ea, klon `SCRATCH\audyt\repo`), dekompilacje gry 1.4.8, BK, BEE, ROT.
Oznaczenia: [K] kod (plik:linia), [P] pomiar z logu, [H] historia / lore ze zrodlem, [S] szacunek.
Sciezki skrocone: `TW` = dekompilacja gry (`...\3cf3e0ac...\scratchpad\ore-supply\cs`), `BK` = `...\ore-supply\bk`, `BEE` = `...\ore-supply\be`,
`ROT` = `...\fa2fd7a6...\scratchpad\rot`, `A` = `Armoury\src` w klonie. Log roczny = `Modules\Armoury\Armoury-2026-10-08_08-18-38.log`.

---

## 0. Dla Jeffa

1. Wiekszosc rzeczy z listy "zycia sredniowiecza" juz jest albo jest zaprojektowana: dluga zima, glod i spichlerze, uchodzcy, banici z biedy,
   zaraza pod murami, okno zniw, Bank, najemnicy, uczty, sluby i dziedziczenie (BK). Nie dubluje ich - sa w tabeli w rozdz. 1.2.
2. **Najwazniejsze odkrycie: nikt jeszcze nie widzial zimy w grze.** Test roczny skonczyl sie w jesieni; pierwsza zima przychodzi w nowej kampanii
   po ok. 1.3 roku i trwa 3-5 lat z polowa plonow. Zanim do niej dojdziesz, trzeba ja raz przepuscic w autotescie - to da sie zrobic tej nocy
   bez zmiany niczego w Twojej grze.
3. **Jency sprzedani w Westeros zostaja dzis niewolnikami** - BannerKings ma taka domyslna regule w kazdym miescie i zamku swiata, a w kanonie
   niewolnictwo w Siedmiu Krolestwach jest zakazane od tysiecy lat. Proponuje regule wedle krainy: w Westeros jeniec wraca do domu (albo idzie
   na Mur - do Twojej decyzji), na Zelaznych Wyspach zostaje thrallem, w Zatoce Niewolniczej i w Volantis, Lys, Myr, Tyrosh - niewolnikiem.
4. **Freyowie nie pobieraja myta.** Most na Bliznakach zatrzymuje wrogow (to juz mamy), ale kupcy i obcy lordowie przechodza za darmo. Myto
   (kilka miedziakow od czlowieka i konia) szloby do kasy Bliznakow - stad bogactwo Freyow, jak w ksiazkach.
5. **Konie w zimie nic nie jedza** (poza miejscami, gdzie akurat pada snieg). Historycznie zimowa kampania konnicy stala na owsie i sianie - to byl
   glowny powod, dla ktorego zima nie walczono. Proponuje: w zimie kon zjada tyle, ile BannerKings liczy mu w sniegu.
6. **BetterEconomy co kilka dni losuje "susze", "zly zbior", "zaraze bydla" bez zadnej przyczyny** - i pokazuje je w swoim oknie, choc plony w grze
   sie nie zmieniaja. To wprowadza w blad; nalezy to wylaczyc w ramach trwajacej paczki domykajacej BetterEconomy.
7. Dalsze pomysly (jarmarki po zniwach, barki na rzekach, okup za oszczedzenie wsi, smoki jedzace owce, wyczerpane kopalnie zlota Lannisterow,
   wiesci z opoznieniem, dziesiecina sept, zakladnicy przy pokoju, dlugosc dnia zalezna od pory roku) sa w rozdz. 4 - wszystkie "na pozniej".
8. Pytania do Ciebie (tylko to, co zmienia Twoja gre lub kanon) sa na koncu rozdz. 4.

---

## 1. Stan dzis

### 1.1 Najwazniejsze fakty pod ten temat

| # | Fakt | Dowod |
|---|---|---|
| F1 | Pory roku Westeros: lato do konca, potem pierwsza jesien 300-450 dni, **pierwsza zima 3-5 lat**, kolejne zimy 2-6 lat. | [K] `A/Settings.cs:589-604`; `A/WesterosClimate.cs:80-120` (dlugosc pory, min. 7 dni `:120`) |
| F2 | W rocznym autotescie lato skonczylo sie w 143. dobie, jesien ma trwac 344 doby -> **zima od ok. 487. doby**; test skonczyl sie na 364. dobie. **Zaden test nie przeszedl przez zime.** | [P] log roczny: `Klimat: start - lato trwa juz 3640 dni, skonczy sie za 143 dni`, `Klimat: dzien 108980 - nowa pora Autumn, potrwa 344 dni` |
| F3 | Zima: plon wsi -50% x gradient polnocy, jedzenie partii +50% x gradient, jesienia AI trzyma 2x wiecej zapasow. Klucze w json Jeffa: 50 / 50 / 0.5 / 2.0. | [K] `A/WinterBite.cs:37-46, 49-56, 78-91, 95-104, 108`; `Armoury.json` linie 72, 114-117 |
| F4 | Przez caly rok testu: "warownie glodne 0", z ujemnym bilansem zywnosci 0-7 z 227 - ale tylko w lecie i jesieni. | [P] linia `Ludzie:` (powstaje w `A/PeopleLedger.cs:257-263, 312`) |
| F5 | Pojemnosc spichlerza warowni w BK = 2 x ludnosc BK + 500 (+ spichlerz zamku). | [K] `BK/BannerKings.Patches/SettlementPatches.cs` klasa `FoodStockPatch` (ok. :97-110); `BK/.../BKFoodModel.cs:31` |
| F6 | Ludnosc swiata w logu: 52.5 mln (doba 1) -> 90.4 mln (doba 363), +72% w rok. Wiecej geb przed pierwsza zima. | [P] log roczny, linie `Ludzie:` (pierwsza i ostatnia) |
| F7 | Jedzenie partii gry: tylko ludzie (`czlonkowie + jency/2`) / 20; konie nie jedza. BK dolicza zwierzeta tylko na pustyni (calosc) i przy pogodzie "snieg / zamiec" (polowa): kon 0.25, kon bojowy 0.5 jednostki na dobe (= 5 i 10 ludzi). | [K] `TW/.../DefaultMobilePartyFoodConsumptionModel.cs` (`CalculateDailyBaseFoodConsumptionf`); `BK/BannerKings.Models.Vanilla/BKPartyConsumptionModel.cs:42-44, 247-260` |
| F8 | Prawo przeprawy: Bliznaki (`ROT_town3`), Fosa Cailin (`castle_B1`), Krwawa Brama (`castle_EN2`) - zawracaja tylko partie lordow **w wojnie** z panem przeprawy; karawany, wozy wsi i neutralni ida bez oplaty. | [K] `A/CrossingLaw.cs:31-33, 125-134`; `A/Settings.cs:151-153` |
| F9 | Sprzedaz jencow w osadzie: BK (prefiks na obu wersjach `SellPrisonersAction`) dopisuje ich do ludnosci osady wedle "polityki karnej"; **domyslna polityka kazdej osady = Enslavement** -> niewolnicy. AI jej nigdy nie zmienia (zmienia ja tylko ekran gracza). Ta sama polityka decyduje, czy BK daje rodowi jencow z rabunku wsi. | [K] `BK/BannerKings.Patches/SettlementPatches.cs:21-40, 79-92`; `BK/BannerKings.Managers/PolicyManager.cs:200-211` (`_ => Enslavement`, :209); uzycia "criminal": tylko `EconomyVM.cs:563` (UI), `BKLoyaltyModel.cs:151`, `VanillaModelTweakPatches.cs:1223, 1310`, `RaidCapturePolicyManager.cs:67`; ROT i BKROTPatch jej nie ruszaja (grep 0) |
| F10 | BEE losuje zdarzenia gospodarcze **bez przyczyny**: szansa 0.14 na dobe, do 12 naraz (jarmark, zly zbior, susza, zaraza bydla, boom gorniczy, sezon karawan), typ losowy. Przy BK model produkcji BEE jest nieczynny, wiec "zly zbior" nie zmienia plonu gry; zmienia tylko wirtualne liczby BEE (podatek, clo, plynnosc, popyt, przeplyw wsi) i jest pokazywany graczowi w oknie BEE. | [K] `BEE/.../EconomicEventCampaignBehavior.cs:339-348` (los typu), `:144-192` (plon), uzycia `TownEconomyCampaignBehavior.cs:1205, 1264, 2436, 2982, 4292`, `VillageSupplyCampaignBehavior.cs:82`, `RouteDangerCampaignBehavior.cs:79`, `SettlementIntelCampaignBehavior.cs:247`; XML `BetterEconomy/ModuleData/better_economy_settings.xml:358-387`; nieczynne modele BEE przy BK: `docs/rozpoznanie-2026-10-08/paczka-170-wynik-2.md:32-34` |
| F11 | Smoki ROT to wierzchowce bitewne - caly kod smokow jest w misji (bitwie), na mapie smok niczego nie je. | [K] `ROT/ROT.Dragon/*.cs` (AIDragon, DragonMountComponent, PlayerDragon...), brak odwolan do jedzenia poza bitwa |
| F12 | Lup ze zdobytego miasta (spustoszenie / grabiez) = utracony dobrobyt x15 zlota z niczego. | [K] `TW/.../SiegeAftermathCampaignBehavior.cs:157-168, 739-748` - **juz w projekcie ekonomii (164a)**, nie dubluje |
| F13 | BK ma "listy z opoznieniem" do rozmowy z dalekim bohaterem (czas doreczenia w godzinach). Wiesci o wojnach i bitwach przychodza do gracza natychmiast. | [K] `BK/BannerKings.Behaviours/BKTelepathyBehavior.cs:77, 107, 112` |

### 1.2 Co juz jest albo jest zaprojektowane (nie proponuje od nowa)

| Temat z listy | Gdzie | Status |
|---|---|---|
| Zima, "Winter is coming", biale kruki | `WesterosClimate.cs` (wpis 12), `WinterBite.cs`, `WinterSource.cs` (wpis 30) | w grze, **niesprawdzone w zimie** (F2) |
| Zapasy na zime, glod po zlym zbiorze, spichlerz okregu | `docs/PROJEKT-GLOD-2026-10-07.md` (spichlerz do 2 lat potrzeby, krzywa zgonow 1315-17 / 1693-94, uchodzcy z glodu) | projekt, po ksiedze ludzi 108-113 |
| Uchodzcy po wojnie | paczka 113 (spustoszenie), PROJEKT-GLOD 2.6 | projekt |
| Banici z glodu i wojny | `OutlawLaw` (wpis 14), paser (106) | w grze |
| Zaraza | `CampFever.cs` (oblezenia), `PlagueWatch.cs`; decyzja Jeffa 05.10: "zaraza tylko z oblezen" | w grze; **nie proponuje szerzej** |
| Zniwa a pobor | decyzja Jeffa 05.10 (okno zniw ok. 8 tygodni, polowa puli), demografia krok 7 | projekt |
| Sezon wypraw / ostroznosc zima | StrategicCampaignAI145 i RBL czytaja nasza pore roku (transpiler `WesterosClimate.cs:318`) | w grze |
| Kompanie najemne, karczma z prawdziwych ludzi | 133, 157, 160, 167 WETERANI | czesc w grze, 167 projekt |
| Bank z Braavos | wpisy 10, 33, 41; 168 DLUG | w grze + projekt |
| Kontrybucje / lup z miasta | F12 -> 164a | projekt |
| Uczty | BK uczty zjadaja prawdziwe jedzenie z magazynu (`BK/.../Feasts/Feast.cs:153-238`) | w grze |
| Sluby, dziedziczenie, tytuly, cechy, wiara | BK (Marriage, Succession, Titles, Guilds, Religions), ROT, BirthAndDeath; warsztaty-cechy (wpisy 47, 48, 52) | w grze |
| Nocna Straz z wyrzutkow | decyzja Jeffa 05.10 (Krainy) | projekt (R-reguly) |
| Niewolni na Zelaznych Wyspach | `REGULY-KRAIN-I-DLUGU-2026-10-06.md` R12, rozdz. 9 pkt 2 | projekt |
| Myto jako polityka krolestwa ("Road Tolls") | `A/KingdomTreasury.cs:291, 350-356` (z kas miast krola) | w grze - to podatek krolestwa, nie myto przeprawy |

---

## 2. Jak bylo w sredniowieczu / w lore [H]

- **Pasza koni.** Kon bojowy dostawal ok. pol buszla owsa dziennie; na kampanie szkocka 1298 r. ok. 3400 koni bojowych potrzebowalo ok. 20 t ziarna
  dziennie (bez koni taborowych); zima konie w stajniach krolewskich zywiono prawie wylacznie owsem, sianem i sloma, a na zimowych kampaniach
  konie cierpialy z braku paszy. Zrodla: [Medievalists.net - jak armie dbaly o konie](https://www.medievalists.net/2024/12/how-medieval-armies-cared-for-their-warhorses/),
  [Exeter, Feeding England's royal horses in the 14th c.](https://medievalwarhorse.exeter.ac.uk/?p=396). [S]: pol buszla owsa ok. 6-7 kg wobec
  ok. 1 kg ziarna czlowieka -> kon na samym owsie = 5-7 "ludzi"; tyle samo liczy BK (kon 5, kon bojowy 10, F7).
- **Transport rzeczny.** Masschaele (EcHR 46/2, 1993): przewoz ladem w Anglii XIV w. srednio ok. 2 x drozszy niz rzeka; miasta nad splawnymi
  rzekami (1/5 grodow) mialy ponad polowe majatku miejskiego w 1334 r. Zrodla: [RePEc - Transport costs in medieval England](https://ideas.repec.org/a/bla/ehsrev/v46y1993i2p266-279.html),
  [ADS - general guide](https://archaeologydataservice.ac.uk/catalogue/adsdata/arch-3427-1/dissemination/pdf/general_guide.pdf),
  [Medievalists.net - Inland water transport](https://www.medievalists.net/2012/08/inland-water-transport-in-medieval-england-the-view-from-the-mills-a-response-to-jones/).
- **Myto mostowe (pontage).** Ok. 370 krolewskich nadan myta mostowego 1228-1440; przyklad stawki: 1397, Hulbrigge - pol pensa od czlowieka
  z koniem, cwierc pensa od kazdego innego zwierzecia. [Wikipedia - Pontage](https://en.wikipedia.org/wiki/Pontage).
  **Lore:** Freyowie od 600 lat trzymaja jedyna przeprawe przez Zielone Widly na setki mil i wzbogacili sie na "ciezkim mycie" od kazdego,
  kto przechodzi. [AWOIAF - The Twins](https://awoiaf.westeros.org/index.php/The_Twins), [AWOIAF - House Frey](https://awoiaf.westeros.org/index.php/House_Frey).
- **Niewolnictwo w lore.** W Westeros (lad) zakazane od tysiecy lat, wstretne Staremu Bostwu i Wierze Siedmiu; Zelazni Ludzie maja thralli
  (przymusowa praca, nie niewolnicy wprost); Braavos zakazuje niewoli Pierwszym Prawem; Pentos zniosl ja w 209 AC pod przymusem Braavos
  (obchodzi zakaz "sluzba za dlug"); Lys, Myr, Tyrosh i Zatoka Niewolnicza - niewola i handel niewolnikami.
  [AWOIAF - Slavery](https://awoiaf.westeros.org/index.php/Slavery), [AWOIAF - Pentoshi](https://awoiaf.westeros.org/index.php/Pentoshi).
- **Okup za oszczedzenie (patis / appatis).** W wojnie stuletniej zalogi i kompanie sciagaly z okolicznych wsi stale oplaty "za ochrone" zamiast
  palic - "pozyczona wladza" (N. Wright, M. Keen). [Wikipedia - Routiers](https://en.wikipedia.org/wiki/Routiers),
  [Conflitti Militari e Popolazioni Civili, s. 150](https://musei.difesa.it/allegati/Conflitti%20Militari%20e%20Popolazioni%20Civili%20-%20Tomo%20I/files/basic-html/page150.html).
- **Jarmarki.** Szesc jarmarkow Szampanii (Lagny, Bar-sur-Aube, 2 x Provins, 2 x Troyes) po 2-6 tygodni, rozlozone od marca do listopada
  z przerwami 40-50 dni, wyrosly z jarmarkow rolnych i bydlecych. [Wikipedia - Champagne fairs](https://en.wikipedia.org/wiki/Champagne_fairs),
  [Britannica - Champagne Fair](https://www.britannica.com/topic/Champagne-Fair).
- **Glod 1315-17.** Ceny zywnosci w Anglii x2 od wiosny do lata 1315; pszenica w Londynie 38-40 s za kwarte wobec 3 s w 1253 r.; w zle lata
  plon 2:1 wobec do 7:1 w dobre; publiczne spichlerze miast byly raczej wyjatkiem niz regula.
  [Wikipedia - Great Famine 1315-1317](https://en.wikipedia.org/wiki/Great_Famine_of_1315%E2%80%931317),
  [FrancoAngeli - Horrea](https://francoangeli.it/riviste/articolo/45695). (Liczby glodu sa juz w PROJEKT-GLOD 2.5.)
- **Kopalnie Lannisterow.** Zloto pod Casterly Rock bylo wyczerpane przed wydarzeniami sagi; bogactwo Tywina szlo z pozyczek dla Korony i inwestycji
  (fanowskie podsumowania, rozne co do daty). [TheWrap - Casterly Rock](https://www.thewrap.com/game-of-thrones-101-casterly-rock),
  [Going Concern - Lannister balance sheet](https://www.goingconcern.com/house-lannisters-balance-sheet-unaudited-isnt-looking-so-hot/). Pewnosc kanonu: srednia.
- **Kruki i pory roku.** Biale kruki Cytadeli oglaszaja zmiane pory; po wiesci o koncu lata Mala Rada liczy zapasy odlozone na zime.
  [AWOIAF - White raven](https://awoiaf.westeros.org/index.php/White_raven), [Wiki of Westeros - Conclave](https://gameofthrones.fandom.com/wiki/Conclave).
- **Smoki jedza.** W "Tancu ze smokami" Drogon porywa owce i kozy, a Daenerys placi pasterzom za straty [H, z pamieci tekstu - do potwierdzenia
  przed wdrozeniem].

---

## 3. Luki i bledy logiki (kazda z dowodem)

**L1. Zima nigdy nie byla sprawdzona, a jest najwiekszym pojedynczym szokiem w calym modelu.** [P] F2: rok testu konczy sie w jesieni.
[K] F1, F3: przez 3-5 lat plon wsi spada o polowe (polnoc bardziej), partie jedza +50%. Dzis nie wiadomo, czy warownie (F5), partie (HungerLaw),
ceny zboza, Bank i budzety rodow to przetrzymaja - a cel Jeffa to "nie moze byc tak, ze nagle wszyscy bankrutuja". Ludnosc rosnie +72% na rok (F6),
wiec zima trafi w wiecej geb niz na starcie. Kazda paczka 162-168 jest kalibrowana na latach lata i jesieni (PROJEKT-EKONOMIA rozdz. 11 - projekcja
4 lat nie mowi o zimie).

**L2. W Westeros sprzedany jeniec zostaje niewolnikiem.** [K] F9: kazda osada swiata ma polityke "Enslavement" (PolicyManager.cs:209), AI jej nie
zmienia. Sprzeczne z lore [H] (zakaz niewoli). Dwa skutki uboczne: (a) BK daje jencow-chlopow z rabunku kazdemu rodowi z siedziba w takim miescie
(REGULY-KRAIN pkt 3 - "drugie zrodlo ludzi z niczego"), bo warunek `ClanRealmAllowsSlavery` przechodzi przez polityke miasta; (b) **kolizja z 164b
i R12**: projekt mowi "jeniec sprzedany zostaje mieszczaninem / niewolnym" - BK juz dzis dopisuje go do ludnosci osady (prefiks w SettlementPatches.cs),
wiec dolozenie tego samego w naszej ksiedze policzy czlowieka dwa razy (jesli ksiega czyta ludnosc BK). Ponadto REGULY-KRAIN pkt 4 twierdzi, ze
"jeniec sprzedany w warowni znika" - w ludnosci BK nie znika (niewolnik osady); do sprostowania w opisie 164b / R12.

**L3. Most Freyow jest darmowy.** [K] F8: straz mostu patrzy tylko na partie lordow w wojnie (`CrossingLaw.cs:129-134`). Kupcy, wozy wsi i neutralni
lordowie przechodza bez oplaty; kasa Bliznakow nie ma z mostu nic. [H] Freyowie zyja z myta.

**L4. Konie nie jedza, gdy nie pada snieg.** [K] F7: w zimie Westeros (lata) kon je tylko w miejscu i godzinie, gdzie mapa ma pogode "snieg / zamiec";
na poludniu i w suche zimowe dni - nic. [H] zima bez wypasu kon zyje z owsa i siana; to glowny koszt zimowej kampanii konnej. Znane od audytu
logistyki (`docs/AUDYT-PELNY-LOGISTYKA.md:283-285`, pkt 4), niezrobione.

**L5. Zdarzenia BEE bez przyczyny i bez skutku w plonach.** [K] F10. Gracz widzi w oknie BEE "Drought" przy wsi, ktora produkuje normalnie; wirtualne
podatki / cla BEE dostaja premie "boom gorniczy" z losu. Sprzeczne z zasada "wszystko z czegos wynika".

**L6. Smok to darmowy wierzchowiec.** [K] F11. Na mapie nie kosztuje nic - ani jedzenia, ani strat w stadach wsi. (Drobne, ale "nie ma rzeczy darmowych".)

**L7. Kopalnie zlota sa niewyczerpalne.** [K] produkcja kopalni = mnoznik x wies, bez stanu zloza (`MineOutputMultiplier`, wpis 98; 105). Dla zelaza
w skali kampanii (lata) to bez znaczenia; dla zlota Lannisterow - sprzeczne z lore (patrz [H]). Do decyzji Jeffa (kanon).

**L8. Wiesci przychodza natychmiast.** [K] F13 - poza rozmowami BK. Gracz w Essos wie w tej samej godzinie o bitwie pod Winterfell. Czysto "growe",
nie psuje ekonomii; tylko immersja.

Uwaga do F12: "lup z miasta z niczego" jest w tabeli zrodel projektu ekonomii (poz. 6) i w paczce 164a - tu tylko odnotowany.

---

## 4. Propozycje

Kolejnosc wedlug wartosc / koszt. **Pierwsze piec (Z1-Z5) warto zrobic najpierw.** Kazda: wylacznik MCM, wpis CHANGELOG z kontrola wedlug zasady 0.

| # | Pomysl | Wartosc | Koszt | Priorytet | Zmienia gre gracza | Nowa kampania | Decyzja Jeffa |
|---|---|---|---|---|---|---|---|
| Z1 | Pomiar zimy przed zima (linia "zapas warowni w dniach" + autotest zimowy) | bardzo duza | mala | **P0** | nie | nie | nie |
| Z2 | Prawo jenca wedle krainy | duza | mala-srednia | **P1** | tak (lojalnosc BK, los jencow) | nie | **tak** (Westeros: dom czy Mur; lista Essos) |
| Z3 | Myto na Bliznakach | srednia | mala | **P1** | tak (gracz placi kilka zl) | nie | tak/nie jednym slowem |
| Z4 | Konie jedza w zimie | duza | mala | **P1** | tak (zimowa jazda drozsza) | nie | tak/nie jednym slowem |
| Z5 | Losowe zdarzenia BEE wylaczone (do paczki 170) | srednia | bardzo mala | **P1** | nie (znika falszywy napis) | nie | nie |
| Z6 | Rok urodzaju z przyczyny (zmiennosc plonu regionu) | duza | srednia | P2 | tak | nie | nie |
| Z7 | Jesien: uboj i sol przed zima | srednia | srednia | P2 | malo | nie | nie |
| Z8 | Jarmark po zniwach | srednia | srednia | P2 | tak | nie | nie |
| Z9 | Barki: tansza droga wzdluz rzek | srednia | srednia | P2 | malo | nie | nie |
| Z10 | Okup za oszczedzenie wsi (patis) | duza | duza | P2 | tak | nie | tak |
| Z11 | Smoki jedza | mala-srednia | mala | P2 | tak | nie | tak |
| Z12 | Wyczerpane zloto Casterly Rock | mala-srednia | mala | P2 | malo | zalecana | tak (kanon) |
| Z13 | Dlugosc dnia wedle pory roku i polnocy | srednia | mala (po obozach 24-6) | P2 | tak | nie | tak |
| Z14 | Wiesci z opoznieniem (kruki) | mala (immersja) | srednia | P2 | tak | nie | tak |
| Z15 | Sezon zeglugi (zimowe sztormy) | srednia | srednia | P2 | tak | nie | nie |
| Z16 | Wiara: dziesiecina i jalmuzna | srednia | duza | P2 | tak | zalecana | tak |
| Z17 | Zakladnicy / wychowankowie przy pokoju | mala (ekonomia 0) | duza | P2 | tak | nie | tak |

### Z1. Pomiar zimy przed zima - P0, mala

- **Przyczyna -> skutek:** zima 3-5 lat z polowa plonow -> spichlerze warowni i targi pustoszeja -> drozyzna, glod partii (HungerLaw kupuje
  za kazda cene), zgony zalog, bunty (`RebellionsCampaignBehavior` gry dziala przy niskiej lojalnosci), kasy rodow. Chcemy wiedziec, **czy i kiedy**
  to sie dzieje, zanim zbudujemy na tym 162-168.
- **Co zmienic:** (a) w `A/PeopleLedger.cs:257-263` (petla warowni juz czyta `Town.FoodChange`) doliczyc "zapas w dniach" = `Town.FoodStocks / -FoodChange`
  dla warowni z ujemnym bilansem; w linii `Ludzie:` (`:312`) dopisac: mediana i minimum dni, liczba warowni < 90 dni i < 30 dni, osobno polnoc / reszta
  (polnoc = ten sam prog Y co `Northness`, `A/WinterBite.cs:49-56`). Tylko log. (b) Autotest 40 dob na **probnym** DLL z innymi domyslnymi klimatu:
  `ClimateSummerDaysLeftMin = Max = 0`, `ClimateFirstAutumnDaysMin = Max = 7` (`A/Settings.cs:592-595`) -> zima od ok. 8. doby, ok. 32 doby zimy.
  Sprawdzone: `Armoury.json` Jeffa nie ma zadnego klucza `Climate*` (grep: 0), wiec domyslne z probnego DLL zadzialaja; po tescie skrypt i tak
  przywraca DLL Jeffa.
- **Liczby:** 7 dni jesieni - minimum wymuszone w kodzie (`WesterosClimate.cs:120`); 40 dob - standard autotestu (12 s/dobe, ok. 10 min).
- **Co odczytac:** "warownie glodne" (lato: 0 przez rok, F4), nowa linia dni zapasu, `HungerLaw`, `Towary:` (ceny zboza), `IronBank:`, `Werbunek:`,
  dezercje. Prog alarmowy [S]: jakakolwiek warownia glodna przed 30. doba zimy albo mediana zapasu < 364 dni (pierwsza zima trwa min. 3 lata - jesli
  mediana zapasu nie siega roku, glod jest pewny i trzeba go zaprojektowac, nie odkryc w grze Jeffa).
- **Ryzyko:** zadne dla gry Jeffa (log + probny DLL). Wynik dotyczy POCZATKU zimy; dlugi przebieg zimy - autotest roczny po paczce GLOD.
- **Zaleznosci:** brak; wynik zasila PROJEKT-GLOD (2.7) i kalibracje 166 (budzet rodu w zimie).

### Z2. Prawo jenca wedle krainy - P1, mala-srednia

- **Przyczyna -> skutek:** jeniec sprzedany w miescie Westeros -> (dzis) niewolnik osady + otwarta furtka "jency z rabunku BK z niczego" (L2) ->
  (po zmianie) jeniec wraca jako dzierzawca do osady swojej kultury (czlowiek nie znika i nie powstaje), a BK przestaje dawac jencow z rabunku
  rodom z siedziba w Westeros (warunek polityki miasta przestaje przechodzic - REGULY-KRAIN pkt 3). W krainach z niewola - niewolnik/thrall
  osady, jak dzis.
- **System pozwala:** polityka jest zwyklym obiektem BK na osade (`PolicyManager.GetPolicy(settlement, "criminal")` / `UpdateSettlementPolicy`);
  ustawic raz po starcie kampanii i przy zmianie wlasciciela (`OnSettlementOwnerChanged`) dla osad AI; osad gracza nie ruszac (wybiera w ekranie BK).
- **Regula (propozycja, kanon do potwierdzenia):**

  | Kraina (kultura ROT) | Polityka | Lore |
  |---|---|---|
  | Westeros lad (wszystkie poza Zelaznymi Wyspami) | Forgiveness (dom) - albo "na Mur" (patrz pytanie) | zakaz niewoli [H] |
  | Zelazne Wyspy (`sturgia`) | Enslavement (thralle) | thralle [H]; spojne z R12 |
  | Zatoka Niewolnicza (`ghiscari`), Volantis (`volantine`), Lys, Myr, Tyrosh (`lyseni`, `myrish`, `tyroshi`) | Enslavement | [H] |
  | Braavos, Pentos (`pentoshi`) | Forgiveness | Pierwsze Prawo; Pentos od 209 AC [H] |
  | Norvos, Qohor, Lorath, Qarth, Dothrakowie, Wyspy Letnie | do decyzji Jeffa | [S] - lore niejednoznaczne |

- **Uwaga Dorne:** REGULY-KRAIN pkt 3 notuje prawo krolestwa "Slavery" dla kultury `aserai` (Dorne) w BK (`DefaultDemesneLaws.cs:138-176`) - w lore
  Dorne nie zna niewoli; ta sama paczka powinna to prawo zdjac (do sprawdzenia w kodzie przed wdrozeniem).
- **Ryzyko:** polityka karna wplywa na lojalnosc w BK (`BKLoyaltyModel.cs:151`) i na jencow w `VanillaModelTweakPatches.cs:1223, 1310` - przed
  wdrozeniem przeczytac te miejsca (zmiana lojalnosci miast Westeros w gore lub w dol). Kolizja z 164b i R12 (L2b): jedna regula na zjawisko -
  albo BK dopisuje ludzi, albo nasza ksiega; 164b musi to rozstrzygnac.
- **Zaleznosci:** przed 164b i R12 (zmienia ich zalozenie); 161/169 (licznik "jency z rabunku BK" pokaze spadek).

### Z3. Myto na Bliznakach - P1, mala

- **Przyczyna -> skutek:** jedyna przeprawa na setki mil -> kazdy kupiec i obcy pan placi -> kasa Bliznakow rosnie -> renta (`TownRentShare`
  7%, `A/Settings.cs:545`) idzie do Freyow -> bogaty, chciwy rod jak w ksiazkach; karawany licza myto w zysku trasy (dluzsza droga bywa tansza).
- **System pozwala:** `CrossingLaw` juz co ok. 2 s przeglada partie w promieniu `CrossingRadius = 3` od Bliznakow (`:118-134`); myto = jeden
  przelew z kiesy partii (karawana / woz: `PartyTradeGold`, lord: `LeaderHero.Gold`, gracz: `Hero.MainHero.Gold`) do `Town.Gold` Bliznakow
  (`ROT_town3` to miasto) - zamkniety obieg (Z1 projektu). Raz na przejscie: zbior partii w strefie + 24 h przerwy.
- **Liczby:** 2 zl od czlowieka, 1 zl od zwierzecia (kon, mul, wol) - 4 x stawka z Hulbrigge 1397 (1/2 d czlowiek z koniem, 1/4 d zwierze), bo Freyowie
  braliby "ciezkie myto" [H]; 1 zl = 1 pens (wpis 42). Przyklad [S]: karawana 25 ludzi + 30 zwierzat = 80 zl; partia lorda 100 ludzi + 40 koni = 240 zl.
  Zwolnieni: partie krolestwa wlasciciela (powinnosc lenna), partie w wojnie (i tak zawracane). Kto nie ma - placi, ile ma (bez dlugu).
- **Ryzyko:** male; petla jest juz platna wydajnosciowo (optymalizacja na koniec - nie dokladac nowej petli, tylko zbior w istniejacej). Karawany BK
  wybieraja trase bez wiedzy o mycie - w pierwszej wersji tylko placa; omijanie mostu = osobny krok, jesli pomiar pokaze sens.
- **Zaleznosci:** brak; najpierw licznik ruchu (rozdz. 5, T2), zeby wiedziec, ile to da. Fosa Cailin i Krwawa Brama - bez myta (brak lore o mycie).

### Z4. Konie jedza w zimie - P1, mala

- **Przyczyna -> skutek:** zima -> brak wypasu -> kon je owies i siano z taboru -> konnica w zimie zjada kilka razy wiecej -> lordowie z duza jazda
  szybciej glodnieja albo kupuja zboze (HungerLaw), ceny zboza rosna zima same -> mniej zimowych wypraw konnych, bez sztucznego zakazu.
- **System pozwala:** `A/WinterBite.cs:95-104` (`FoodPostfix`) juz dodaje zimowy czynnik do jedzenia partii; model BK ma gotowa metode
  `CalculateAnimalFoodNeed(party, ignoreCamels)` (`BK/.../BKPartyConsumptionModel.cs:49`). Regula: w zimie (`WinterBite.WinterNow`) dodac
  `-0.5 x CalculateAnimalFoodNeed x Northness` **tylko tam, gdzie BK nie doliczyl juz pol paszy za snieg** (pogoda Snowy/Blizzard, `:252-258`)
  i nie na pustyni (`:247-250`) - jedna zasada, zero podwojnego liczenia.
- **Liczby:** pol paszy BK = kon 2.5 czlowieka, kon bojowy 5 ludzi [K] - zgodne z [H] (pol buszla owsa ok. 6-7 kg = 5-7 ludzi na pelnym owsie, czesc
  paszy z siana / sciolki, ktorych gra nie ma). Gradient polnocy jak dla ludzi (Dorne 0.75, Mur 1.25).
- **Ryzyko:** AI z duza jazda moze glodowac w zimie (glod = ranni, `HungerLaw`) - dlatego tylko po Z1 i w zimowym autotescie; uwaga na kolejnosc z
  `Rations` (ciecie -40% ma objac tez pasze, inaczej kon bylby "drozszy" od czlowieka o 1/0.6). Konie w zbrojowni DTE (wierzchowce w magazynie) - nie
  liczone, bo `CalculateAnimalFoodNeed` czyta tylko `ItemRoster` partii i jezdnych.
- **Zaleznosci:** Z1 (pomiar). Zmienia gre gracza (jego jazda w zimie kosztuje jedzenie) - pytanie do Jeffa.

### Z5. Losowe zdarzenia BEE wylaczone - P1, bardzo mala (przekazac paczce 170)

- **Co zmienic:** `EconomicEventDailySpawnChance` 0.14 -> 0 w `BetterEconomy/ModuleData/better_economy_settings.xml:361` (tym samym skryptem, co
  13 kluczy, z kopia). Skutek: koniec falszywych "susz" w oknie BEE i losowych premii podatku / cla w wirtualnych liczbach BEE (F10).
- **Dlaczego nie tej nocy osobno:** to domena trwajacej **paczki 170** (domkniecie BEE) - jeden plik, jedna kopia, jeden autor. Jesli 170 sie juz
  zamyka - dopisac jako klucz 14 w `tools/bee/zamknij-ujscia-bee.ps1`.
- **Na pozniej:** "zly rok" z przyczyna = Z6, nie z losu BEE.

### Z6. Rok urodzaju z przyczyny - P2, srednia

- **Przyczyna -> skutek:** pogoda roku w regionie -> plon x 0.8-1.2 (sasiednie regiony podobnie) -> spichlerze okregow (GLOD) -> ceny zboza, wozy,
  karawany wioza zboze tam, gdzie brak (103/117 juz wedle zysku) -> rzadki "rok glodu" (x0.6) [S, ok. raz na 20 lat] daje 1315 bez skryptu.
- **System:** ten sam postfiks produkcji wsi co zima (`A/WinterBite.cs:108, 159`); losowanie raz na okno zniw na region; zapis w SyncData (SaveText).
- **Liczby:** +-20% = "zwykly zly rok" (PROJEKT-GLOD 2.5); 0.6 = plon 1315 wobec sredniej [H: Wielki Glod, plon 2:1 wobec do 7:1 - zrodlo w rozdz. 2].
- **Zaleznosci:** po PROJEKT-GLOD (bez spichlerzy zly rok tylko podnosi ceny); zastepuje Z5.

### Z7. Jesien: uboj i sol przed zima - P2, srednia

- **Przyczyna -> skutek:** brak paszy na zime -> jesienia wsie ubijaja czesc stad (wiecej miesa i skor na targu, mniej zwierzat zima) -> popyt na sol
  rosnie (solenie) -> sol drozeje jesienia. Wiaze 144 (zywy inwentarz), 115 (cena soli od niedoboru) i zime.
- **System:** postfiks produkcji wsi (zwierzeta -> mieso/skory w porze Autumn), popyt miast na sol (115).
- **Zaleznosci:** Z1, GLOD; wymaga sprawdzenia, czy stado wsi BK ma stan (BK `VillageData`), inaczej uboj "z niczego".

### Z8. Jarmark po zniwach - P2, srednia

- **Przyczyna -> skutek:** po zniwach nadwyzka w regionie -> 14-dniowy jarmark w glownym miescie regionu -> karawany i wozy maja tam lepszy kurs
  (wiecej kupujacych naraz) -> oplata targowa (np. 1% obrotu od sprzedajacego) do kasy miasta; turniej GrandTourney w tym samym czasie.
- **System:** wybor celu karawan juz liczy zysk (103/117) - jarmark = dodatkowi kupujacy (popyt) w miescie, nie pieniadz z niczego; Z1 projektu:
  oplata przelewem.
- **Liczby:** 14 dni (2 tygodnie - dolny koniec Szampanii [H]), raz w oknie zniw na region (24 regiony = ok. 1 jarmark co 2 tygodnie na swiecie).
- **Ryzyko:** srednie - zmienia przeplywy towaru, ktore wlasnie ustabilizowano (ruda, drewno). Po 169.

### Z9. Barki: tansza droga wzdluz rzek - P2, srednia

- **Przyczyna -> skutek:** rzeka -> przewoz o polowe tanszy [H Masschaele] -> wsie i miasta nad Tridentem, Manderem, Czarnym Nurtem, Rhoyne
  sprzedaja dalej -> miasta rzeczne bogatsze (jak 1334). Barek na mapie nie bedzie (siatka mapy nie ma rzek dla statkow - NavalDLC plywa po morzu).
- **System:** tabela par osad na tej samej rzece (dane, recznie z mapy ROT) -> mniejszy koszt drogi w wyborze miasta przez woz wsi (119) i w zysku
  karawany (117) - bez zmiany ruchu.
- **Ryzyko:** dane recznie (bledna para = dziwny handel); srednie.

### Z10. Okup za oszczedzenie wsi (patis) - P2, duza

- **Przyczyna -> skutek:** armia w obcej ziemi zamiast palic bierze stala oplate z kiesy wsi -> wojna czesciowo zywi sie sama, wies traci zloto,
  nie ludzi i plon (mniej 113) -> dluzsze wojny bez wypalonej ziemi; gracz dostaje opcje w menu wsi wroga.
- **System:** wymaga kiesy wsi (112 K7 "utarg wsi zostaje we wsi"), spustoszenia (113) i budzetu rodu (166 - AI decyduje, czy mu sie oplaca).
- **Decyzja Jeffa:** tak (nowe zachowanie AI i nowa opcja gracza).

### Z11. Smoki jedza - P2, mala

- **Przyczyna -> skutek:** smok w partii -> codziennie zjada zwierzeta z taboru (owce, kozy, bydlo) -> bez nich poluje na stada najblizszej wsi
  (strata w produkcji zwierzat wsi, spadek relacji) -> smok kosztuje i szkodzi chlopom, jak u Daenerys.
- **System:** smok jest przedmiotem-wierzchowcem w ekwipunku bohatera (kod ROT tylko w bitwie, F11); dzienny tik partii + `ItemRoster`.
- **Liczby [S]:** 2 owce lub 1 krowa na dobe na doroslego smoka - do decyzji Jeffa (brak twardych liczb w kanonie).

### Z12. Wyczerpane zloto Casterly Rock - P2, mala

- **Przyczyna -> skutek:** zloza Zachodu wyczerpane -> mniej rudy zlota z wsi Zachodu -> Lannisterowie musza zyc z dochodu i pozyczek (168) - jak
  w kanonie. Dla zelaza i reszty: bez zmian (zloza na wieki, kampania trwa lata).
- **Decyzja Jeffa:** kanon (czy wierne ksiazkom "puste kopalnie", czy legenda "zloto Lannisterow").

### Z13. Dlugosc dnia wedle pory roku i polnocy - P2, mala (po obozach 24-6)

- **Przyczyna -> skutek:** w zimie na polnocy dzien trwa kilka godzin -> armia maszeruje krocej -> zimowa wyprawa na polnoc trwa dluzej i zjada
  wiecej. [H, rzad wielkosci] na 60 st. szerokosci dzien zimowy ok. 6 h, letni ok. 18 h.
- **Uwaga:** Jeff wlasnie zazadal stalego obozu 24:00-6:00 (inny raport tego audytu). To jest tylko **opcja na pozniej**, ktora rozszerzylaby te
  godziny o pore roku; bez jego slowa nie ruszac.

### Z14. Wiesci z opoznieniem (kruki) - P2, srednia

- Komunikaty o dalekich wojnach, bitwach i oblezeniach dla gracza z opoznieniem wedle odleglosci (kruk ok. 1 dzien na kilkaset km [S]). Tylko gracz;
  AI bez zmian (inaczej rozbijamy decyzje AI). BK ma juz mechanizm opoznionego doreczenia (`BKTelepathyBehavior.cs:112`). Immersja, zero ekonomii.

### Z15. Sezon zeglugi - P2, srednia

- Zima: czestsze sztormy na morzu (`MapWeatherModel.WeatherEvent.Storm` istnieje w grze), wolniejsze / ryzykowniejsze statki -> handel morski
  i wyspy (Pyke, Lys) zima trudniejsze. Najpierw sprawdzic, co NavalDLC juz robi ze sztormem (dekompilacja `NavalDLC.dll` - nie robiona).

### Z16. Wiara: dziesiecina i jalmuzna - P2, duza

- Septy jako nowy posiadacz pieniadza i zboza (dziesiecina z plonu wsi Wiary Siedmiu, nie Starych Bogow - Polnoc bez dziesieciny), jalmuzna w glodzie
  zmniejsza zgony (GLOD 2.5). Domyka obieg i daje roznice krain. Duza zmiana, nowy posiadacz - po 169 (pomiar) i GLOD.

### Z17. Zakladnicy / wychowankowie przy pokoju - P2, duza

- Pokoj po przegranej -> dziecko rodu pokonanego jako wychowanek u zwyciezcy (Theon w Winterfell) -> zlamanie pokoju kosztuje wychowanka. Zero
  ekonomii, duzo fabuly; Diplomacy i BK maja pokoj i relacje, brak mechaniki zakladnika (grep "Hostage": tylko casus belli BK).

### Pytania do Jeffa (tylko to, co zmienia gre albo kanon)

1. **Jency w Westeros** (Z2): sprzedany jeniec ma wracac do domu (zostaje chlopem swojej krainy) czy isc na Mur do Nocnej Strazy?
   W Essos: czy Norvos, Qohor, Lorath, Qarth trzymaja niewolnikow? (Braavos i Pentos - nie, Zatoka, Volantis, Lys, Myr, Tyrosh - tak.)
2. **Myto na Bliznakach** (Z3): gracz placi jak kazdy obcy (kilkadziesiat do kilkuset zl za przejscie z druzyna)? Lordowie krolestwa Freyow - za darmo?
3. **Konie w zimie** (Z4): Twoja konnica w zimie je (kon ok. 2.5 czlowieka, kon bojowy 5) - tak?
4. **Zloto Lannisterow** (Z12): kopalnie Casterly Rock puste jak w ksiazkach - tak/nie?
5. **Smoki** (Z11): maja jesc owce i krowy z taboru, a bez nich porywac stada z wiosek - tak/nie?

---

## 5. Do zrobienia tej nocy vs na pozniej

### Tej nocy (male, bezpieczne, sprawdzalne autotestem 40 dob; nic nie zmienia rozgrywki)

| # | Co | Gdzie | Jak sprawdzic | Kolizje |
|---|---|---|---|---|
| T1 | Linia "zapas warowni w dniach" (mediana, min, < 90 dni, < 30 dni; polnoc / reszta) dopisana do `Ludzie:` - tylko log | `A/PeopleLedger.cs:257-263` (petla juz czyta `FoodChange`), wypis `:312` | autotest 40 dob (lato): linia jest, liczby > 0, 0 potkniec | 169 (log obiegu) pisze w `MoneyLedger` - inny plik; uzgodnic tylko format linii, jesli `sprawdz_logi.py` czyta `Ludzie:` (parser) |
| T2 | Licznik ruchu przez Bliznaki - tylko log: raz na dobe "Przeprawa Bliznaki: karawany N (kiesy X zl), wozy wsi N, partie rodow N (wlasne / obce krolestwo), gracz tak/nie, myto gdyby bylo Z zl" | `A/CrossingLaw.cs` - zbior partii w promieniu `CrossingRadius` liczony w istniejacej petli `:125-134` (przed filtrem `IsLordParty`), wypis w tiku dobowym | autotest 40 dob: linia co dobe; jesli 0 karawan przez 40 dob - promien 3 nie lapie przeprawy (wtedy Z3 do przemyslenia) | brak; petla juz istnieje (optymalizacja na koniec - nie dodawac drugiej) |
| T3 | Autotest zimowy: probny DLL = T1 (+T2) z domyslnymi `ClimateSummerDaysLeftMin = Max = 0`, `ClimateFirstAutumnDaysMin = Max = 7` - **nie do wgrania** | `A/Settings.cs:592-595` (tylko w probnym buildzie) | NOWA kampania, 40 dob: `Klimat:` pokazuje zime ok. 8. doby; odczyt Z1 | json Jeffa bez kluczy `Climate*` (sprawdzone) - nic nie nadpisze; skrypt przywraca DLL Jeffa (md5) jak zawsze |

Kolejnosc: T1+T2 w jednym buildzie (log), autotest letni 40 dob (czy nic nie psuja), potem ten sam kod z domyslnymi klimatu z T3 - autotest zimowy.
Do wgrania Jeffowi tylko T1+T2 (sam log) i tylko na jego "wgraj". Z5 (BEE) - przekazac paczce 170, nie robic osobno.

### Na pozniej

- Po wyniku T3: Z4 (konie w zimie) - drugi autotest zimowy; jesli T3 pokaze glod warowni przed 30. doba zimy - najpierw PROJEKT-GLOD, potem reszta.
- Z2 (prawo jenca) - po odpowiedzi Jeffa, **przed** 164b i R12 (zmienia ich zalozenia).
- Z3 (myto) - po T2 (wiemy, ile ruchu) i odpowiedzi Jeffa.
- Z6, Z7 - po PROJEKT-GLOD; Z8, Z9 - po 169 (pomiar obiegu); Z10 - po 112, 113, 166; Z16 - po GLOD i 169.
- Z11-Z15, Z17 - tylko na slowo Jeffa.

### Kolizje z paczkami w toku

- **169** (log obiegu): T1/T2 to inne pliki (`PeopleLedger`, `CrossingLaw`), ale ten sam dzienny tik - kolejnosc linii w logu uzgodnic z parserem `sprawdz_logi.py`.
- **170** (domkniecie BEE): Z5 nalezy do niej (klucz 14: `EconomicEventDailySpawnChance = 0`).
- **171** (zbrojenie zalog): brak kolizji.
- **164b / R12** (ludzie z jencow, niewolni Zelaznych Wysp): L2 - BK juz dopisuje sprzedanych jencow do ludnosci osady jako niewolnikow (wszedzie);
  164b musi wybrac jedna droge, inaczej podwojne liczenie; opis REGULY-KRAIN pkt 4 ("jeniec znika") do sprostowania.
