# AUDYT 05 - zachowanie ekonomiczne lordow AI i brak fali bankructw (09.10.2026, noc)

Tylko odczyt: nic nie zmienione w kodzie, w grze ani w zapisach. Rok = 364 doby (Calendar.cs), 4 lata = 1456 dob.
Oznaczenia: [K] kod (plik:linia), [P] pomiar z logu / CSV, [H] historia albo lore ze zrodlem, [S] szacunek.
Kod = wersja w grze (Armoury c01a54ba = commit 2e235ea, kopia w `scratchpad\audyt\repo`). Dekompilacje: gra 1.4.8 `ore-supply\cs`,
BK `ore-supply\bk`, ROT `fa2fd7a6...\scratchpad\rot`. Pomiar: rok `Armoury-2026-10-08_08-18-38.log` + `economy-2026-10-08_08-20-02.csv`.
Skrypty tej analizy: `7016f733...\scratchpad\a05\bank.py`, `klasy.py`, `rodzina.py`.

Baza, ktorej NIE powtarzam (przeczytana): `docs/PROJEKT-EKONOMIA-OBIEG-2026-10-08.md` (rozdz. 6-8, paczki 162-168), badania B1-B3
(`3cf3e0ac...\dzien-6\ekonomia-obieg\B2-lordowie.md`, `B3-historia.md` - dochody panow, zold, Bardi i Peruzzi, Edward III), `docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md`
rozdz. 5, decyzje Jeffa w `docs/STAN-PRAC.md`. Ten raport dopisuje to, czego tam nie ma: **jak lord AI ZACHOWUJE sie w biedzie** (poza samym
budzetem wojska), co dzis robi z tym gra/BK, czego brakuje i jak zmienic projekt 166 i 168.

---

## 0. Dla Jeffa (prostym jezykiem)

1. Dzis lord AI w klopotach finansowych umie zrobic tylko dwie rzeczy: pozyczyc w Banku z Braavos i czekac, az mu ludzie uciekna. Nie odklada
   budow, nie zeni syna dla posagu, nie bierze okupu za jencow, nie szuka pokoju, kiedy skarbiec jest pusty. Prawdziwy pan sredniowieczny
   i lord z Westeros robil to wszystko.
2. Fala bankructw w tescie rocznym (121 bankructw, 104 rody, w tym krolowie Stark, Greyjoy i Mance Rayder) zrobil glownie Bank: pozycza
   temu, kto juz nie ma z czego oddac, a rate sciaga tylko z kiesy glowy rodu. W dniu bankructwa pozostali czlonkowie tych rodow mieli
   srednio ok. 45 tys. zlota - dosc, zeby zaplacic rate wiele razy.
3. Panowie samych zamkow to polowa bankrutow (68 ze 122 takich rodow). Krolowie i panowie miast bankrutuja rzadko.
4. Wielka naprawa (lord trzyma tyle wojska, na ile ma dochodu; korona placi z biezacych podatkow; Bank pozycza tylko temu, kto odda)
   jest juz zaprojektowana (paczki 165, 166, 168). Ten raport dodaje do niej "zachowania lorda": okupy za jencow miedzy lordami,
   posag przy slubach, pokoj z braku pieniedzy, oszczedzanie w dlugu i pomoc rodziny.
5. Na te noc proponuje tylko trzy male, bezpieczne rzeczy: rodzina pomaga splacac rate Banku (zamiast bankructwa), dezerterzy z niezaplaconych
   armii ida do lasu (dzis znikaja - 5 710 ludzi w roku), dluznik nie buduje. Do tego linie w logu o niewoli lordow i slubach (do paczki 169).
6. Trzy pytania do Ciebie (zmieniaja gre): czy krolestwa bez pieniedzy maja szybciej zawierac rozejm; czy wrogi lord ma wracac z niewoli,
   gdy jego rodzina zaplaci okup; czy "pomoc wasali na okup i posag" robic razem z Twoim odlozonym pomyslem o kasach miast.

---

## 1. Stan dzis

### 1.1 Co jest w kodzie (wszystkie mechanizmy, ktore dotykaja biedy lorda AI)

| Mechanizm | Co robi dzis | Gdzie [K] |
|---|---|---|
| **Bank Zelazny - kiedy AI pozycza** | codziennie kazdy rod krolestwa (bez gracza): gdy **kiesa glowy** < 10 dni zoldu partii (w wojnie 20), pozycza 1.5 x brakujacej kwoty, do limitu | `IronBank.cs:202-213` |
| Bank - limit | 60 dni **dochodu modelu brutto z dzisiejszego dnia** + 10 000 za miasto + 5 000 za zamek, x wiarygodnosc; nie sprawdza, czy rod ma nadwyzke na rate | `IronBank.cs:101-125` (`:110`, `:113`); `Settings.cs:625-627` |
| Bank - oprocentowanie | krol 20%, z lennem 30%, bez lenna 45%, +10 pp gdy dlug juz biegnie, +15 pp po bankructwie, +2 pp za kazde spoznienie | `IronBank.cs:127-139`, `:240`; `Settings.cs:629-633` |
| Bank - rata | dlug / dni do terminu (pol roku) **tylko z kiesy glowy**; 3 niezaplacone raty pod rzad = bankructwo: Bank zabiera 50% kiesy glowy od razu, potem 25% dziennie; **odsetki dalej rosna** | `IronBank.cs:223-256` (`:224`, `:230`, `:241-247`); `Settings.cs:622` |
| Bank - smierc rodu | dlug przepada (strata Banku) | `IronBank.cs:222, 275-280` |
| Zalegly zold (WarLedger) | po 2 dniach zwloki 0.5% ludzi dziennie x dni (AI polowa), elity pierwsze; ludzie **usuwani z listy bez zdarzenia dezercji** - nie trafiaja do puli wyrzutkow ani do ksiegi ludzi | `WarLedger.cs:52-99`, `:80`, `:113-139` (`:133`) |
| Zwrot korony | 50% zoldu w wojnie ze **stanu skarbca** (gdy pusty - nic) | `KingdomTreasury.cs:134-214` (`:178`) |
| Powinnosci do korony | 2% / 3% dochodu modelu | `KingdomTreasury.cs:47-88`; `Settings.cs:606-608` |
| Zakupy sprzetu AI | 25% (kiesa wodza - 2 000) na wizyte, bez wzgledu na dochod i dlug | `AiGear.cs:191-193`; `Settings.cs:421-422` |
| Budowy | 10% dochodu modelu dziennie, takze gdy rod jest w dlugu albo bankrutem | `BuildFunding.cs:144-158` |
| Limit zoldu partii BK | progi z **kiesy glowy** (112.9 tys. / 37.6 tys.), nie z dochodu | BK `EconomyPatches.cs:117-156` (B2 rozdz. 6) |
| Zapomoga gry | skarbiec -> rod z kiesa glowy < 30 000 (500-2 000 dziennie) | `DefaultClanFinanceModel.cs:155-158, 517-528` |
| Dlug wobec korony (trybut, reparacje) | gra sciaga co dobe z kiesy glowy w nicosc | `DefaultClanFinanceModel.cs:185, 219-230, 342-358` |
| **Korona daje ziemie biednym** | przy nadawaniu lenna: +do 30 pkt, gdy glowa ma < 30 000, +30 gdy rod nie ma lenna | `SettlementClaimantDecision.cs:223-228` (jedyna "pomoc korony" w grze poza zapomoga) |
| **Okupy lordow AI-AI** | **nie ma**. Wiezien-lord idzie do lochu (`PartiesSellPrisonerCampaignBehavior.cs:33`), wychodzi przez ucieczke (losowanie dzienne, `PrisonerReleaseCampaignBehavior.cs:257`) albo przy pokoju (`:117`, `:186`). Barter okupu jest tylko w rozmowie z graczem (`SetPrisonerFreeBarterBehavior.cs`). RC podnosi okup wylacznie dla lordow gracza (`RealisticCaptivity/FairRansom.cs:60-66, 75-77`) | - |
| **Posag przy slubach AI** | **nie ma**. ROT zastepuje swaty gry losowym dobieraniem par (`ROTRelationshipsBehavior.cs:16-76`: losowa para + szansa BK, `MarriageAction.Apply` bez zlota). BK ma wycene posagu (`BKMarriageModel.cs:361-445`: tier 1-5 = 20-180 tys. + 2 500 x poziom), ale placi ja tylko w kontrakcie z rozmowy gracza (`BKMarriageBehavior.cs:687-715`) | - |
| **Pokoj z braku pieniedzy** | **nie ma**. BK ocenia pokoj z sily i zmeczenia (straty / sila ludzka + 0.04 na rok wojny), bez pieniedzy (`BKDiplomacyModel.cs:1031-1045`, `BKWarModel.cs:458-480`). Diplomacy: zmeczenie 0.25 dziennie + straty, rabunki, oblezenia - tez bez pieniedzy (`Configs\...\Diplomacy\DiplomacySettings_v1.2.json`: `WarExhaustionPerDay 0.25`, `ScalingWarReparationsGoldCostMultiplier 50`) | - |
| **Podatki wedlug potrzeb** | AI BK wybiera wysoki/niski podatek tylko wedlug lojalnosci i hojnosci namiestnika (miasto) albo liczby domow (wies), nie wedlug finansow pana (`BKSettlementBehavior.cs:430-445`); wysoki = x1.15 podatku (`BKTaxModel.cs:63-69`). Uwaga: podatek miasta zerowany wpisem 49 (renta z ludnosci zamiast niego - `docs/ZRODLA-DOCHODU.md:23, 244`), wiec dzwignia BK dziala dzis prawie tylko na wsie | - |
| Inwestycje bogatych | BK: lord kupuje karawane / warsztat, gdy kiesa rodu >= 2 x cena i krolestwo bez wroga (`BKLordPropertyBehavior.cs:45, 148, 157`); **cena karawany idzie w nicosc** (`:78` `ChangeHeroGold(-cena)`, bez odbiorcy) - tego ujscia nie ma w tabeli 4.1 projektu ani w rozpoznaniu 169 | - |

Wniosek z kodu: jedyne reakcje lorda AI na biede to (1) pozyczka, (2) przyciecie przez BK druzyny dopiero ponizej 37.6 tys. w kiesie glowy,
(3) ucieczki ludzi. Nic po stronie "zarobic / ograniczyc / zawrzec umowe".

### 1.2 Pomiar roku (TOWARY 3, 364 doby) [P]

| Miara | Wartosc | Zrodlo |
|---|---|---|
| bankructwa Banku | **121 zdarzen, 104 rody** (17 rodow dwa albo trzy razy - po splacie Bank znow im pozycza z wiarygodnoscia 0.25) | `bank.py` na logu |
| bankruci wg kwartalu | 9 / **53** / 28 / 31 - fala w 2. kwartale, gdy Bank mial jeszcze kapital (0.13 mln juz w dobie 183) | `bank.py` |
| bankruci wg lenna (na starcie) | **zamek 68 z 122**, miasto 13 z 67, **krol 7 z 29** (Casat, Emeros, Greyjoy, Pono, Rayder, Stane, Stark), bez lenna 10 z 78, rody mniejsze 0 z 12 (Bank im nie pozycza) | `klasy.py` (log + CSV) |
| miasta bankrutow | Bolton, Manderly, Velaryon, bar Emmon, Corbray, Blacktyde, Harlaw, Farwynd, Botley, Merlyn, Dogshead, Jhago, Styr Magnar - Polnoc, Zelazne Wyspy, Dolina | `klasy.py` |
| kiedy | 109 ze 119 bankructw ze znanym terminem wypadlo **ponad 30 dni przed terminem** - nie "rata balonowa", tylko brak gotowki na zwykla rate 3 dni pod rzad | `bank.py` |
| **kiesy czlonkow rodu w dniu bankructwa** (rodzina minus glowa) | **mediana 45.5 tys.** (kwartyle 36.4 / 55.9 tys.); w 104 ze 121 bankructw czlonkowie mieli > 20 tys., w 42 > 50 tys. | `rodzina.py` (CSV `zloto_rodziny - zloto_glowy` z dnia bankructwa) |
| limit Banku skacze z dnia na dzien | Stark 09:03-09:06 (kilka dob): limit 36 090 -> 73 170 -> 127 800 -> 81 810 -> 203 220 (dochod modelu z jednego dnia) | log, linie `IronBank: Stark pozycza` |
| koniec roku | 73 bankrutow, 121 dluznikow, dlug 6.75 mln, kapital 0.72 mln; splat w calosci 184 | ostatnia linia `IronBank: dzien 109197` |
| Polnoc w 364. dobie | skarbiec 9 zl, zwrot zoldu 14 929 z naleznych 39 671; 22 z 26 rodow na minusie, 11 ponizej 1 000; Stark kiesa 61 835, -2 166/dobe | `Skarbce: dzien 109200 - The North` |
| korona razem | zwrot 252 827 z naleznych 335 925; 6 krolestw bez dosc zlota | `Korona: dzien 109200` |
| zalegly zold (WarLedger) | 4 417 wpisow, **5 710 ludzi usunietych w roku bez sladu** (nie ida do puli wyrzutkow) | `grep WarLedger ... traci` |
| liczba pozyczek | >= 3 054 wpisy (log ucina do 15 na dobe, `Settings.cs:635`), 182 rody | `bank.py` |

### 1.3 Dlaczego tak wychodzi (lancuch przyczyn)

1. **Wojsko placone z zapasu, nie z dochodu** (B2, projekt rozdz. 1.3) - pan zamku ma 20-300 zl dochodu z ziemi na 1 100-1 700 zl zoldu.
2. **Bank pozycza dokladnie tym, ktorzy nie odda**: warunek pozyczki to "malo zlota w kiesie glowy" (`IronBank.cs:206-210`), czyli objaw deficytu.
   Pozyczka 1.5 x braku wystarcza na kilka dni zoldu; kolejna podnosi procent o 10 pp; po wyczerpaniu limitu brak raty 3 dni = bankructwo.
3. **Rata tylko z kiesy glowy**, choc rodzina ma pieniadze (mediana 45 tys.) - przyczyna wiekszosci bankructw w krotkim terminie.
4. **Po bankructwie spirala**: Bank zabiera 25% kiesy glowy dziennie (`:230`), a od kiesy glowy zalezy limit BK druzyny i werbunek - glowa
   stoi na zerze, a odsetki dalej rosna (`:224`, sprzecznie z decyzja Jeffa 07.10 (c) "odsetki zamrozone w dniu zajecia").
5. **Brak hamulca politycznego**: wojna w 27-31 z 35 krolestw przez caly rok (projekt 1.1); nic w BK ani Diplomacy nie laczy decyzji o pokoju
   z pustym skarbcem.
6. **Brak dodatnich zdarzen dla biednych**: w grze nie ma okupow AI ani posagow AI - jedyne "duze" przeplywy miedzy rodami to trybut
   i Bank, oba lamia slabszych.

### 1.4 Co jest tylko zaprojektowane (nie w kodzie)

- **166 BUDZET RODU** (projekt 6.1-6.4): pulap zoldu 25% D w pokoju / 55% D + czesc zapasu w wojnie; zwolnienia 15% nadwyzki dziennie
  do wsi i karczm; kiesa rodziny (czlonek oddaje glowie brak dnia, nie ponizej 5 000); oszczedzanie przy G < R.
- **165 KORONA**: zwrot z biezacych wplywow, renty wedlug lenn, zapomoga tylko z biezacych wplywow, zapas 0.5 mln.
- **168 DLUG**: pozyczka tylko na zdolnosc splaty (15 dni D, rata <= 10% D), kolejnosc wierzycieli (skarbiec, bogaty rod, Bank), drabina
  D1-D4 zamiast bankructwa, odsetki zamrozone, dlug wymarlego rodu na nowego pana wsi; AI pozycza tylko w wojnie przy G < R, **na okup**
  i na trybut.

---

## 2. Jak bylo w sredniowieczu i w Westeros [H]

### 2.1 Co robil pan w klopotach (kolejnosc mniej wiecej taka, jak siegano po srodki)

| Srodek | Przyklad | Zrodlo |
|---|---|---|
| Mniejsza swita, wojna tylko za zold korony | Edward III i jego panowie: wojna na kontraktach z korona, honoraria swity ok. 20% dochodu (B3) | B3 rozdz. 3-4 (Lewis, Lancaster 1313-14) |
| Zatrzymanie budow | Roger Bigod, 5. hrabia Norfolku: dlugi z rozbudowy Framlingham i Chepstow | https://en.wikipedia.org/wiki/Roger_Bigod,_5th_Earl_of_Norfolk ; https://tudortimes.co.uk/places/framlingham-castle/the-bigods |
| Pozyczka u bankierow i kupcow | Bardi i Peruzzi dla Edwarda III, krach 1340-46 | B3 (Russell 1918, Hunt) |
| **Zastaw ziemi albo jej dochodu** | Robert Krotkoudy zastawil Normandie bratu za 10 000 marek na krucjate (1096), splata w 3-5 lat | https://www.dhi.ac.uk/crusaders/person?id=649 |
| Wierzyciel bierze dochod ziemi do splaty | Statut Westminster II (1285), writ of elegit: wierzyciel bierze ruchomosci, a gdy brak - **polowe ziemi dluznika i jej dochod do splaty**; Magna Carta 1215 kl. 9: najpierw ruchomosci, ziemia dopiero, gdy ich brak | https://en.wikipedia.org/wiki/Elegit ; https://magnacarta.cmp.uea.ac.uk/read/magna_carta_1215/Clause_09 |
| Ugoda z korona kosztem dziedzictwa | Bigod 1302: oddal krolowi hrabstwo i ziemie, dostal je z powrotem dozywotnio + ziemie za 1 000 L rocznie; po smierci (1306) wszystko wrocilo do korony | https://en.wikipedia.org/wiki/Roger_Bigod,_5th_Earl_of_Norfolk |
| **Okupy** | glowny przeplyw bogactwa miedzy szlachta w wojnie stuletniej; czesto szacowany na ok. roczny dochod jenca (tradycja, nie prawo); pomoc wasali i poddanych na okup pana | Ambuhl, *Prisoners of War in the Hundred Years War* (CUP 2013): https://www.medievalists.net/2013/01/ransoming-prisoners-of-war-became-widespread-in-the-hundred-years-war-new-book/ ; https://scholarworks.iu.edu/journals/index.php/tmr/article/view/18561 |
| **Pomoc wasali (aid)** | Magna Carta 1215 kl. 12: "rozsadna pomoc" bez zgody tylko na okup pana, pasowanie najstarszego syna i pierwszy slub najstarszej corki | https://magnacarta.cmp.uea.ac.uk/read/magna_carta_1215/Clause_12 |
| **Posag i malzenstwo** | posag corki szlachcica jako jednorazowy przeplyw ziemi i gotowki; np. testament Stonorow: kazda corka "co najmniej 200 marek" na slub; Bigod/Gaunt/Neville - ziemie przez dziedziczki | https://core-prod.cambridgecore.org/core/books/world-of-the-stonors/stonors-lords/E910580158B07BF28ACA8445B05DB708 ; Payling, "The Economics of Marriage in Late Medieval England" (EcHR 2001) - tylko bibliografia, liczb nie odczytalem |
| **Pokoj z braku pieniedzy** | rozejm w Esplechin 1340: Edward III bez pieniedzy na zold sojusznikow po Tournai | https://en.wikipedia.org/wiki/Truce_of_Espl%C3%A9chin ; https://en.wikipedia.org/wiki/Siege_of_Tournai_(1340) |

### 2.2 Jak korona ratowala wasali [H]

- Placila zold za sluzbe w polu (kontrakty, B3) i honoraria "za straz ziemi" (Percy, Marchie - B3); to jest juz w projekcie 165 (zwrot 50% i renty wedlug lenn).
- Nadawala ziemie i urzedy (w grze: `SettlementClaimantDecision.cs:223-228` juz premiuje biednych).
- Przejmowala dlugi albo rozkladala je na raty w zamian za ustepstwa (Bigod 1302 - kosztem dziedzictwa; to sprzeczne z decyzja Jeffa "lord nigdy
  nie traci lenna", wiec tylko jako wzor "korona staje sie wierzycielem", bez odbierania ziemi).
- Nie bylo "zapomogi z niczego": korona miala pieniadze tylko z podatkow i pozyczek (B3 rozdz. 4.2).

### 2.3 Westeros i Essos [H]

| Wzorzec | Lore | Zrodlo |
|---|---|---|
| Bank nie daruje i finansuje wrogow | Cersei wstrzymuje splaty -> Bank sciaga dlugi w calych Siedmiu Krolestwach, nie daje nowych pozyczek; Tycho Nestoris jedzie do Stannisa, ktory obieca splacic dlugi tronu | https://awoiaf.westeros.org/index.php/Iron_Bank_of_Braavos |
| Pan, ktory nie sciaga dlugow, traci posluch | Tytos Lannister pozyczal zloto wasalom, ci nie oddawali; Tywin zazadal splaty, a kto nie mogl od razu - **zakladnik w Casterly Rock do splaty**; Reyne'owie i Tarbeckowie odmowili -> bunt | https://awoiaf.westeros.org/index.php/Tytos_Lannister ; https://awoiaf.westeros.org/index.php/Reyne-Tarbeck_rebellion |
| Korona zyje z pozyczek | "czarodziejstwo" Littlefingera jako skarbnika = pozyczki u Banku i Lannisterow; tron w ogromnym dlugu | https://awoiaf.westeros.org/index.php/Master_of_coin |
| Malzenstwo jako cena i sojusz | Walder Frey: przeprawa przez Blizniaki za zareczyny Robba i Aryi z Freyami i wychowankow; zlamanie umowy = zdrada na Krwawych Godach | https://awoiaf.westeros.org/index.php/Walder_Frey ; https://awoiaf.westeros.org/index.php/The_Twins |
| Okup i wymiana jencow | Jaime w niewoli Robba jako karta przetargowa; zwyczaj okupu rycerzy (turniejowe konie i zbroje) | (powszechnie w ASOIAF; nie potwierdzalem osobno) |

Wniosek [H]: realny lord w biedzie (1) tnie swite i budowy, (2) zastawia dochod, nie ziemie, (3) zeni dzieci z bogatymi rodami, (4) zyje z okupow
i lupu, gdy wygrywa, (5) korzysta z pomocy wasali na okup i posag, (6) dostaje od korony zold za sluzbe, (7) gdy pieniadze sie koncza - rozejm.
W Westeros Bank nie daruje, a wierzyciel bierze zakladnika albo wspiera rywala.

---

## 3. Luki i bledy logiki (kazda z dowodem)

| # | Luka | Dowod |
|---|---|---|
| L1 | **Bank pozycza na objaw deficytu, bez badania zdolnosci splaty** - pozyczka jest wyzwalana tym, ze rod juz nie ma zlota | `IronBank.cs:206-211`; 121 bankructw [P]; B2 7 "nie sprawdza zdolnosci splaty" |
| L2 | **Rata i warunek pozyczki tylko z kiesy glowy** | `IronBank.cs:209, 225, 236`; mediana 45.5 tys. u czlonkow w dniu bankructwa, 104/121 > 20 tys. [P] |
| L3 | **Odsetki rosna po bankructwie** - sprzeczne z decyzja Jeffa 07.10 (c) | `IronBank.cs:223-224` (liczone przed galezia `Defaulted`) |
| L4 | **Limit z dochodu jednego dnia** - skacze x5 w kilka dob | `IronBank.cs:110`; Stark 36-203 tys. [P] |
| L5 | **Bankrut moze znow pozyczac po splacie** z wiarygodnoscia 0.25 i znow upasc (17 rodow 2-3 razy) | `IronBank.cs:268-272`, `:107`; [P] |
| L6 | **Dezerterzy z zaleglego zoldu znikaja** (5 710 ludzi w roku) - lamie "jak znika to zamykamy" (ludzie) i Z6 projektu | `WarLedger.cs:133` (`AddToCounts(c, -take)` bez `OnTroopsDeserted`); pula wyrzutkow i ksiega ludzi slucha tylko zdarzenia (`ArmouryBehavior.cs:530, 549`; `OutlawLaw.cs:329-338`) |
| L7 | **Dluznik buduje i kupuje sprzet jak bogacz** | `BuildFunding.cs:154-158` (10% dochodu bez warunku dlugu); `AiGear.cs:193` (25% kiesy, w tym pozyczonej) |
| L8 | **Brak okupow AI-AI** - zaden przeplyw "z przegranych do zwyciezcow", lord siedzi w lochu do ucieczki albo pokoju; **paczka 168 zaklada pozyczke "na okup glowy albo dziedzica", ktorej nie ma czym wywolac** | 1.1 (gra, BK, RC); projekt 8.2 pkt (2) |
| L9 | **Brak posagu w slubach AI** - BK ma wycene, ale tylko dla gracza; ROT losuje pary bez wzgledu na majatek | `ROTRelationshipsBehavior.cs:35-76`; `BKMarriageBehavior.cs:687-715`; `BKMarriageModel.cs:361-445` |
| L10 | **Pokoj i wojna bez pieniedzy** - wojna caly rok w 27-31 z 35 krolestw; puste skarbce Polnocy, Reach, Dorzecza, Doliny bez wplywu na decyzje | `BKDiplomacyModel.cs:1031-1045`; `BKWarModel.cs:458-480`; projekt 1.1-1.2 [P] |
| L11 | **Podatek BK wybierany bez patrzenia na finanse pana** | `BKSettlementBehavior.cs:430-445` |
| L12 | **Nowe ujscie w nicosc: zakup karawany przez lorda BK** | `BKLordPropertyBehavior.cs:78` - nie ma go w tabeli 4.1 projektu ani w `docs/rozpoznanie-2026-10-08/paczka-169-*` (grep "LordProperty" pusty). Dziala tylko w pokoju (`:45`), wiec male w roku wojny - ale w pokoju wazne dla 169 |
| L13 | **Dlug w kiesie, ktorej nie ma**: BK kupuje warsztat/karawane, gdy `Clan.Gold` >= 2 x cena - takze pieniedzmi z pozyczki | `BKLordPropertyBehavior.cs:148, 157` |
| L14 | Trybut/reparacje (-2.2 mln Stormlands) - hipoteza: reparacje Diplomacy przy wyczerpaniu wojna (`ScalingWarReparationsGoldCostMultiplier 50` w ustawieniach Jeffa). Projekt 13.13 to zostawia otwarte - nadal niepotwierdzone w dekompilacji (Diplomacy nie zdekompilowany) | ustawienia Diplomacy; B2 4 |

---

## 4. Propozycje

Kazda: co / liczby z uzasadnieniem / priorytet / wielkosc / czy zmienia gre gracza / nowa kampania / ryzyko / zaleznosci.

### A. Male, bezpieczne (kandydaci na te noc)

**A1. Rodzina pomaga splacac rate Banku i pozyczac mniej** (L2)
- Co: w `IronBank.Daily` dla rodow AI: (a) przed oznaczeniem spoznienia (`:236`) brakujaca czesc raty bierze sie od doroslych zywych czlonkow rodu
  (bez glowy), kazdy oddaje najwyzej to, co ma ponad prog; przelew `GiveGoldAction.ApplyBetweenCharacters(czlonek, glowa, kwota)` (przelew, nie
  z niczego); (b) przed pozyczka (`:209-211`) to samo do wysokosci `target` - pozyczka tylko na brak calej rodziny.
- Liczby: prog czlonka = max(5 000; 10 dni zoldu jego wlasnej partii) - 5 000 to `FamilyPurseFloor` z projektu 6.4, a 10 dni zoldu to ten sam zapas,
  ktory Bank wymaga od glowy (`IronBankWageDays` 10), zeby czlonek-wodz nie stracil progu werbunku gry (3 050 zl, B2 6).
- P0, mala, gracz: nie (rod gracza pominiety), nowa kampania: nie.
- Ryzyko: czlonkowie z partiami maja mniej na werbunek (dlatego prog z ich zoldu); przesuwa problem, nie leczy go (deficyt zostaje do 166).
  Kolizja z 166 (kiesa rodziny): 166 robi to ogolnie - A1 jest jego podzbiorem; po 166 zostawic jeden mechanizm (166), A1 wylaczyc.
- Oczekiwany skutek [S]: wiekszosc bankructw z L2 (104 ze 121 mialo > 20 tys. u czlonkow) odsunieta o miesiace; liczba pozyczek mniejsza.

**A2. Dezerterzy z niezaplaconych armii ida do puli wyrzutkow** (L6)
- Co: w `WarLedger.DesertElitesFirst` (`:113-139`) zbierac usunietych do `TroopRoster` i po petli wolac `OutlawLaw.OnTroopsDeserted(mp, roster)`
  oraz `PeopleLedger.OnTroopsDeserted(mp, roster)` (bezposrednio - nie strzelac zdarzeniem gry, zeby nie budzic cudzych sluchaczy).
- Liczby: brak - ta sama liczba ludzi, tylko z odbiorca.
- P0, mala, gracz: nie (ludzie gracza tez trafia do lasu - widac to tylko po bandytach w regionie), nowa kampania: nie.
- Ryzyko: pula wyrzutkow rosnie o ok. 16 ludzi dziennie [P: 5 710 / 364] - pomijalne wobec dezercji gry. Sprzet dezerterow (zbrojownia DTE)
  dalej zostaje w partii - osobna sprawa (ksiega ludzi 108).
- Zaleznosci: brak; zgodne z Z6 projektu.

**A3. Dluznik nie buduje, a w zaleglosci kupuje tylko braki** (L7)
- Co: `BuildFunding.cs:158` - budzet 0 dla rodu z dlugiem w Banku, ktory ma spoznienie albo jest bankrutem, i polowa budzetu przy biegnacym dlugu bez
  spoznien; `AiGear.cs:193` - budzet x0.5 dla rodu ze spoznieniem/bankruta. Potrzebny odczyt `IronBank` (np. `internal static int Stage(Clan)`:
  0 brak dlugu, 1 dlug, 2 spoznienie, 3 bankrut).
- Liczby: 0 / 0.5 - "w dlugu budowy stop, sprzet tylko braki" to szczebel S1 z projektu 8.2 i REGULY 5.2 (pan w dlugu oszczedza na dworze i budowach).
- P1, mala, gracz: nie, nowa kampania: nie. Ryzyko: budowy wojskowe w wojnie staja u dluznikow (zamierzone). Zalezy: brak; 168 przejmie warunki.

**A4. Linie pomiaru (tylko log) - dopisac do 169, jesli jeszcze otwarta** (L8-L10)
- "Niewola lordow: dzien D - w niewoli N (krolowie k, glowy rodow g), w lochach / w partiach, srednio dni, dzis: ucieczki, uwolnieni przy pokoju,
  sprzedani; okup hipotetyczny (cena RC wedlug pozycji) razem X; rodzina stac (G ponad 20 000) w Y przypadkach".
- "Sluby AI: dzien D - N, w tym biedny-bogaty (G < 20 000 i G > 60 dni dochodu), posag hipotetyczny razem X".
- "IronBank (na sucho 168): dzis pozyczek N, z tego przeszloby test zdolnosci splaty M; spoznienia, ktore pokrylaby rodzina".
- P0 (pomiar przed okupami i posagiem), mala, gracz: nie, kampania: nie. Kolizja: 169 = "sam log obiegu" - nie robic osobnej paczki.

### B. Zmiany do projektu 166 (BUDZET RODU)

| # | Zmiana | Dlaczego |
|---|---|---|
| B1 | **D bez jednorazowych wplywow**: okup otrzymany, posag otrzymany, wyprzedaz (D4), pozyczka - ida do G, nie do sredniej 28 dob | inaczej jeden okup (25-150 tys.) podnosi pulap zoldu na 4 tygodnie i rod werbuje za pieniadze, ktorych nie bedzie co miesiac |
| B2 | **Jednorazowe wydatki z G ponad R**: okup za swojego, posag za corke | historycznie z zapasu i pomocy wasali, nie z biezacego dochodu [H kl. 12]; budzet dnia ich nie widzi |
| B3 | **Zakupy zalog w miescie (paczka 171) wliczac do pulapu sprzetu** | 171 przenosi zakupy zalog zamku do miasta - po K5 (zawor zamku) pan zamku traci zwrot 2/3 z tego, co wydalby we wlasnym zamku; bez wliczenia zaloga kupuje poza budzetem |
| B4 | **Stan "w dlugu" wplywa na udzialy** (dwor -50%, budowy 0, sprzet tylko braki) - jedno zrodlo: `IronBank.Stage` z A3 | REGULY 5.2; dzis 166 ma tylko oszczedzanie przy G < R |
| B5 | **Zakup karawany / warsztatu BK z budzetu reszty** (prefiks `BKLordPropertyBehavior.ShouldHaveCaravan` / `ShouldHaveWorkshop`: false, gdy G < 60 x D albo rod ma dlug) | L13; inwestuje ten, kto ma nadwyzke, nie pozyczke |
| B6 | Rycerze BK bez ziemi - zakupy zaopatrzenia majatkow (BK `BKVillageSupplyAutoBehavior.cs:316-349`) w pulapie sprzetu (projekt 13.12 tylko "po pomiarze") | 94 rody, glowne zrodlo ich biedy bez druzyny (B2 3.2) |
| B7 | A1 wylaczyc po wejsciu 166 (jeden mechanizm "kiesa rodziny") | zasada "jedna zasada na jedno zjawisko" |

### C. Zmiany do projektu 168 (DLUG)

| # | Zmiana | Dlaczego |
|---|---|---|
| C1 | **Pozyczka "na okup" wymaga paczki okupow AI (D1 ponizej)** - albo wykreslic pkt (2) z 8.2 do czasu D1 | L8: w grze nie ma okupu AI, wiec warunek nigdy nie zajdzie |
| C2 | Odsetki zamrozone **juz od pierwszego dnia D3** (dzisiejszy kod liczy je takze bankrutom, L3); poprawka `:223-224` w ramach 168 | decyzja Jeffa (c) |
| C3 | Wiarygodnosc po splacie: rod, ktory byl w D3, dostaje kredyt dopiero po 182 dobach bez zaleglosci i z D > kosztow stalych | L5: 17 rodow upadlo 2-3 razy |
| C4 | **Zakladnik zamiast wyprzedazy** (opcja D3b, lore Tywina): rod w D3 moze oddac wierzycielowi-rodowi (nie Bankowi) czlonka jako "wychowanka" w jego siedzibie; dopoki tam jest, wierzyciel nie przechodzi do D4. Mechanicznie: bohater zostaje w osadzie wierzyciela (jak gosc), bez walki | lore Tywin/Tytos [H]; daje AI drugi sposob niz wyprzedaz; P2, tylko gdy D1-D3 dzialaja |
| C5 | **Korona przejmuje dlug wasala w D2** (P2): gdy skarbiec ma biezaca nadwyzke wplywow po zwrocie i zapomodze, splaca Bankowi zaleglosc wasala i staje sie wierzycielem na 10% (ta sama drabina) | korona jako wierzyciel pierwszej kolejnosci jest juz w D1; to tylko kolejnosc dla starego dlugu; bez odbierania ziemi (decyzja Jeffa) |
| C6 | Limit z D, nie z dochodu dnia (jest w 168) - dopisac w opisie dowod L4 (Stark 36-203 tys.) | uzasadnienie liczby |
| C7 | Rod w dlugu: zadnych zakupow BK (B5) i budow (A3) | L7, L13 |

### D. Nowe zachowania (nowe paczki)

**D1. OKUPY MIEDZY LORDAMI AI** (L8) - P1, srednia, zmienia gre: tak (lekko - wrogi lord wraca szybciej, gdy rodzina zaplaci), nowa kampania: nie.
- Co: raz na dobe, dla kazdego bohatera-lorda AI w niewoli AI (loch osady albo partia): cena = wycena pozycji RC (`FairRansom.LordPrice`:
  krol 100 000, glowa rodu 25 000, lord 8 000, x (1 + 0.25 x tier) + miasta 10 000 / zamki 5 000 - `RealisticCaptivity/Settings.cs:49-54`),
  ograniczona do 90 dni D rodu jenca. Rodzina jenca placi, gdy G - R >= cena (najpierw glowa, potem czlonkowie); odbiorca = glowa rodu, ktory
  trzyma jenca (loch: pan osady). Gdy nie stac: pozyczka 168 (pkt 2) albo czekanie (ucieczka i uwolnienie przy pokoju bez zmian).
  Uwolnienie `EndCaptivityAction.ApplyByRansom(jeniec, platnik)`.
- Liczby: limit 90 dni D [S] - historycznie okup ok. rocznego dochodu, ale z rokiem na zbiorke i pomoca wasali [H]; AI nie ma ani roku, ani
  pomocy, wiec cwierc roku, zeby okup nie lamal rodu. Prog G - R: okup nigdy nie schodzi ponizej rezerwy wojny.
- Ryzyko: przeplyw z przegranych do zwyciezcow wzmacnia bogatych (zwykle wygrywa silniejszy) - dlatego limit 90 D i tylko z nadwyzki ponad R;
  wiecej wolnych lordow = wiecej partii (sprawdzic linia A4 i "Wojsko"); Diplomacy liczy zmeczenie od uwiezien (`WarExhaustionPerImprisonment 1.0`) -
  krotsza niewola = wolniejsze zmeczenie. RC: nie dotykac wyceny dla gracza (`FairRansom.cs:75-77` - galaz gracza bez zmian).
- Zaleznosci: A4 (pomiar), 166 (R, D) albo tymczasowo R = 20 000 i D = dochod modelu x 1; potem 168 pkt (2).

**D2. POSAG PRZY SLUBACH AI** (L9) - P1, mala-srednia, zmienia gre: nie (sluby AI-AI), nowa kampania: nie.
- Co: postfiks na `MarriageAction.Apply` dla par, w ktorych zaden nie jest z rodu gracza i nie trwa kontrakt BK (flaga w prefiksie
  `BKMarriageBehavior.ApplyMarriageContract`, zeby nie placic dwa razy): rod panny mlodej (ten, z ktorego odchodzi) placi rodowi, do ktorego
  przechodzi, posag = min(wycena BK `GetDowryValue`, 25% G rodu panny, 90 dni D rodu panny), tylko z G ponad R; brak pieniedzy = slub bez posagu.
  Dodatkowo postfiks `NpcCoupleMarriageChance`: x1.5, gdy rod pana mlodego jest biedny (G < R), a rod panny bogaty (G > 60 D) i w tym samym
  krolestwie; x0.8 dla par "bogaty z biednym" w druga strone - srednia liczba slubow bez zmian.
- Liczby: 25% G i 90 D [S] - posag szlachcianki to jednorazowo ulamek majatku, rzad rocznego dochodu lub mniej (Stonor 200 marek na corke [H]);
  cwierc roku w grze, bo w ROT sluby sa czeste. x1.5 / x0.8 - rozsadne przesuniecie bez zmiany liczby slubow.
- Skutek: jedyny kanal, ktorym pieniadz plynie od bogatych do biednych rodow bez korony - przeciwwaga dla "bogaci bogatsi" (projekt 0.2).
- Ryzyko: ROT zastepuje swaty gry (`ROTRelationshipsBehavior.cs:18`) - latac `MarriageAction.Apply`, nie ROT; koszt CPU pomijalny (ROT malzenstwa
  x2.4 w pomiarze 08.10 to ich petla, nie nasza). Jeff: "optymalizacja na koniec" - nie dokladac petli po wszystkich bohaterach.

**D3. POKOJ Z BRAKU PIENIEDZY** (L10) - P1, srednia, zmienia gre: TAK (pytanie do Jeffa), nowa kampania: nie.
- Co: postfiks na `BKDiplomacyModel.GetScoreOfDeclaringPeace` i `GetScoreOfDeclaringWar`: F = udzial rodow krolestwa z G < R (po 166; do tego czasu:
  glowy < 5 000) + 0.5, gdy skarbiec < 0.25 mln od 28 dob (prog z projektu 10.2); pokoj += F x max(|wynik wojny|, 600) x 0.75 (ta sama skala, ktora
  BK uzywa dla zmeczenia), wypowiedzenie wojny x (1 - F). Pominac wojny fabularne ROT (Straz/Wolni Ludzie, Inni - decyzja H4 "nie ruszac").
- Liczby: skala BK, zeby nie zagluszyc wlasnych powodow BK; prog 0.25 mln jak w progach rownowagi projektu.
- Uzasadnienie [H]: Esplechin 1340; korona prowadzila wojne tak dlugo, jak miala z czego placic.
- Ryzyko: mniej wojen = mniej lupu i okupow, ale tez mniej wydatkow; projekt rozdz. 11 liczy "fale" (60% wojny) - wynik lepszy niz "wojna caly rok"
  (wojsko 77-79 tys., 4-6 biednych). Kolizja z Diplomacy (wlasne zmeczenie i reparacje) - postfiks na modelu BK, ktory Diplomacy tez czyta.

**D4. POMOC WASALI NA OKUP I POSAG / PODATEK NADZWYCZAJNY** - P2, srednia, zmienia gre: tak, **tylko na slowo Jeffa** (jego odlozony pomysl
"miasta z dlugami i podatkiem nadzwyczajnym" z 07.10 - nie zaczynac bez niego).
- Co: przy okupie albo posagu pana (D1, D2) jednorazowa "rozsadna pomoc" z kies jego wsi i nadwyzki kas jego osad ponad zapas kupcow (najwyzej 1/3 kwoty),
  kosztem lojalnosci (-5) - Magna Carta kl. 12 [H]. AI przy G < R wybiera wysoki podatek BK tam, gdzie lojalnosc > 50 (dzis tylko lojalnosc - L11).
- Zaleznosci: 165 (zawory), decyzja Jeffa o kasach miast.

**D5. HONORARIA DLA RYCERZY BEZ ZIEMI** - P2, srednia, gra: nie, kampania: nie. Rod bez lenna z G < R, ktorego partia jest w armii bogatego rodu
tego krolestwa, dostaje od wodza armii 1/3 swojego zoldu dziennie (honoraria swity Gaunta 20-50 marek rocznie, B3) - przeplyw od bogatych do 94 rodow
rycerzy. Dopiero po 165 (zwrot korony) i 166, zeby nie liczyc zoldu dwa razy.

### E. Co z tego razem daje "brak fali bankructw" w 4 lata [S]

- Rdzen (165 + 166 + 168, rachunek projektu 11.2): glowy < 5 000 2-6 z 324, zajecia 0-5 rocznie.
- Ten raport dodaje to, czego rachunek nie ma (projekt 11.4 "wstrzasy"): okup (D1) i posag (D2) jako jednorazowe przeplywy z G ponad R zamiast
  pozyczek; pokoj z biedy (D3) zamiast wojny do dna skarbca; rodzina (A1) zamiast bankructwa; dezerterzy w obiegu (A2).
- Bieda nadal ma skutki: biedny rod ma mniejsza druzyne (166), nie buduje (A3), jest w drabinie dlugu (168), zeni dzieci bez posagu, siedzi
  dluzej w niewoli; krolestwo z pustym skarbcem szuka rozejmu.

---

## 5. Do zrobienia tej nocy vs na pozniej

### 5.1 Tej nocy (male, bezpieczne, sprawdzalne autotestem 40 dob, nowa kampania niepotrzebna)

| Kol. | Zmiana | Miejsce | Co sprawdzic w logu po 40 dobach |
|---|---|---|---|
| 1 | **A2 dezerterzy WarLedger do puli wyrzutkow** | `Armoury/src/WarLedger.cs:113-139` (`DesertElitesFirst` zwraca tez `TroopRoster`), wolanie po `:87`: `OutlawLaw.OnTroopsDeserted(mp, r)`, `PeopleLedger.OnTroopsDeserted(mp, r)` | linie `WarLedger: ... traci N` = przyrost dezercji lordow w `PeopleLedger` (`_desLord`) i puli wyrzutkow regionu; 0 wyjatkow |
| 2 | **A1 rodzina placi rate i pozycza najpierw rodzinie** | `Armoury/src/IronBank.cs:209-211` (przed pozyczka) i `:236` (przed spoznieniem); prog czlonka max(5 000, 10 x zold jego partii); tylko rody AI | nowa linia w `IronBank: dzien` "z kies rodziny: N rat, X zl; pozyczek mniej o M"; spoznienia i bankructwa (w 40 dobach bylo 2 bankructwa w biegu 06:51) - porownac z biegiem 06:51; czlonkowie z partiami nie schodza ponizej progu |
| 3 | **A3 dluznik nie buduje** (opcjonalnie, po 1 i 2) | `IronBank` nowe `Stage(Clan)`; `BuildFunding.cs:158`; `AiGear.cs:193` | linia `Budowy oplacone` z licznikiem "wstrzymane: dlug N"; zakupy AI dluznikow |
| 4 | **A4 linie pomiaru** - dopisac do 169, jesli jeszcze otwarta; inaczej osobno, tylko log | 169 | "Niewola lordow", "Sluby AI", "IronBank (na sucho)" - pierwsze liczby do D1/D2 |

Kazda zmiana osobno (zasada "jedna zmiana naraz"), wylacznik MCM (`IronBankFamilyPays`, `WarLedgerToOutlaws`, `DebtorsStopBuilding`), po dodaniu
ustawien `python tools/gen_mcm.py`; sprawdzic `Armoury.json` Jeffa (nowe klucze - nie ma ich tam, wiec zadziala domyslne). Kolizje: 169 dotyka
`MoneyLedger` (A1 to przelew miedzy bohaterami - ksiega go nie liczy jako zrodla ani ujscia, sprawdzic, czy okno `IronBank` nie liczy go podwojnie);
170 i 171 nie dotykaja tych plikow (171 - `AiGear` przy zalogach: A3 dotyka `AiGear.cs:193` - skladac po 171 albo przed, nie razem).

### 5.2 Na pozniej

| Co | Kiedy / zalezy od |
|---|---|
| B1-B7 do opisu 166 | przy pisaniu 166 (po 169, 162, 165, 160) |
| C1-C7 do opisu 168 | przy pisaniu 168; C1 wymaga D1 |
| D1 okupy AI | po A4 (pomiar niewoli); najlepiej przed 168 |
| D2 posag AI | po A4 (pomiar slubow); niezalezne od 166 (tymczasowo R = 20 000) |
| D3 pokoj z biedy | po odpowiedzi Jeffa (pytanie 1); najlepiej po 165 (skarbiec z biezacych wplywow) |
| D4 pomoc wasali / podatek nadzwyczajny | tylko na slowo Jeffa (jego odlozony pomysl o kasach miast) |
| D5 honoraria rycerzy | po 165 + 166 |
| L12 zakup karawany BK w nicosc (`BKLordPropertyBehavior.cs:78`) | dopisac do tabeli 4.1 projektu i do 169/164 (odbiorca: kasa miasta, w ktorym kupiono) |
| L14 zrodlo trybutu -2.2 mln (Diplomacy) | dekompilacja Diplomacy przed 165 (projekt 13.13) |

### 5.3 Pytania do Jeffa (tylko to, co zmienia gre)

1. **Pokoj z braku pieniedzy:** czy krolestwo z pustym skarbcem i biednymi wasalami ma chetniej zawierac rozejm (a biedne rzadziej wypowiadac wojny)?
   Skutek: wojny krotsze, falami, mniej lupu i mniej bankructw. Wojny fabularne (Mur, Inni) bez zmian. Rekomendacja: **tak**.
2. **Okupy miedzy lordami AI:** czy pojmany lord AI ma wracac, gdy jego rodzina zaplaci okup temu, kto go trzyma (dzis siedzi do ucieczki albo pokoju)?
   Skutek: zwyciezcy zarabiaja na jencach, wrogowie wracaja szybciej. Rekomendacja: **tak**.
3. **Pomoc wasali i podatek nadzwyczajny** przy okupie i posagu pana: robic razem z Twoim odlozonym pomyslem o kasach miast (dlugi, podatek nadzwyczajny),
   czy osobno wczesniej? Rekomendacja: **razem, pozniej**.

---

## 6. Pliki i weryfikacja

- Ten raport (jedyny zapisany plik).
- Kod [K] sprawdzony: Armoury (2e235ea) `IronBank.cs:101-139, 202-280`, `WarLedger.cs:29-139`, `KingdomTreasury.cs:21-88, 134-214`, `AiGear.cs:185-197`,
  `BuildFunding.cs:138-158`, `Settings.cs:348-351, 414, 421-422, 606-608, 620-642`, `OutlawLaw.cs:329-338`, `PeopleLedger.cs:163-171`, `ArmouryBehavior.cs:530, 549`;
  RC `FairRansom.cs:60-120`, `Settings.cs:48-54`; gra `DefaultClanFinanceModel.cs:129-230, 504-528`, `SettlementClaimantDecision.cs:223-228`,
  `SetPrisonerFreeBarterBehavior.cs`, `PartiesSellPrisonerCampaignBehavior.cs:33`, `PrisonerReleaseCampaignBehavior.cs:117, 186, 257`, `SellPrisonersAction.cs:25-60`;
  BK `BKMarriageModel.cs:361-457`, `BKMarriageBehavior.cs:687-715`, `BKLordPropertyBehavior.cs:33-163`, `BKSettlementBehavior.cs:405-482`, `BKTaxModel.cs:55-69`,
  `BKDiplomacyModel.cs:1020-1045`, `BKWarModel.cs:458-480`; ROT `ROTRelationshipsBehavior.cs:16-76`.
- Pomiar [P]: skrypty `7016f733...\scratchpad\a05\bank.py` (bankructwa wzgledem terminu, kwartaly), `klasy.py` (rodzaj rodu bankruta), `rodzina.py`
  (kiesy czlonkow w dniu bankructwa) na `Armoury-2026-10-08_08-18-38.log` i `economy-2026-10-08_08-20-02.csv`; linie `Skarbce`, `Korona`, `WarLedger` z logu.
- Ustawienia: `Armoury.json` Jeffa nie ma kluczy Banku, korony, AiGear, BuildFunding (sprawdzone grep) - dzialaja domyslne z `Settings.cs`;
  Diplomacy `DiplomacySettings_v1.2.json` (zmeczenie wojna wlaczone).
