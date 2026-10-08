# 06. Nieumarli rosna za szybko - audyt 2026-10-09

Oznaczenia: [K] kod (plik:linia), [P] pomiar z logu, [H] historia / lore ze zrodlem, [S] szacunek.
Kod = wersja w grze: CrashScribe.dll md5 11fa0214 (wpis CHANGELOG 2026-10-04 (13), zgodny z repo audytu), Armoury c01a54ba.
Skroty sciezek: CS = `CrashScribe/src`, ROT = dekompilacja `ROT.CampaignBehaviors/ROTOthersCampaignBehavior.cs`,
LOG-ROK = `Documents/.../CrashScribe/session-2026-10-08_08-18-37.log` (autotest roczny, nowa kampania, 364 doby).

Uwaga o zakresie: ten temat (Nieumarli) przyszedl z listy zadan skryptu; w wiadomosci Jeffa z tej nocy go nie ma (Jeff pisal o
odleglosciach, obozach 24-6 i "rzeczach, na ktore nie wpadlismy"). Zrodlem jest zdanie Jeffa zapisane w `docs/PRZEKAZANIE-2026-10-08.md:132`.
Styk z obozami jest w punkcie 3.7.

---

## 0. Dla Jeffa

1. Szesc osad Innych po roku to w praktyce wina naszego wlasnego dodatku "Pochod Nocnego Krola" (20.09), a nie samego ROT. Sam ROT
   przez 193 dni nie zdobyl Innym nic. Nasz Pochod co dzien wskazuje hordzie cel i robi to bez kalendarza ROT. ROT ten kalendarz ma:
   Mrozny Brzeg i Rogowa Stopa dopiero po ok. 3,6 roku, Thenn i Kly Mrozu po ok. 4,8 roku, Hardhome po ok. 6 latach.
2. W rocznym autoteście Inni szli tak: w 6. dniu ruszyli na Rogowa Stope, w 62. dniu ja wzieli, w 81. dniu Twierdze Crastera, a w 132. dniu
   mieli 13 osad. W dniach 112-123 Nocny Krol oblegal sam Mur z ok. 3 000 trupow. Potem Wolni Ludzie i Straz odbili wszystko i w 251. dniu
   Inni nie mieli ani jednej bandy, a po kilku dniach odrodzili sie z niczego.
3. W ksiazkach Inni do konca "Tanca ze smokami" (ok. 300 AC, czyli rok 2 naszej gry) nie przekroczyli Muru. Byla bitwa na Piesci Pierwszych
   Ludzi (299 AC) i grozba pod Hardhome. W serialu Mur pada pod koniec 7. sezonu, ok. 304 AC, czyli w 5.-6. roku gry.
4. Proponuje kalendarz Innych. Rok 1-2: walcza w polu za Murem, ale nie zdobywaja zadnej warowni. Od 3. roku moga brac Piesc i Twierdze Crastera.
   Dalej obowiazuje kalendarz ROT. Mur najwczesniej w 6. roku, poludnie dopiero po Murze. Kazdy prog mozna zmienic w ustawieniach.
5. Druga sprawa to tempo przyrostu trupow. W roku przybylo ok. 7 tys. trupow z niczego: kazda nowa banda dostaje 154 trupy za darmo,
   a kazda banda 2 trupy dziennie. To jest juz zaprojektowane i zatwierdzone przez Ciebie (Inni rosna tylko z poleglych, jency Innych wstaja jako trupy).
   Czeka tylko na wykonanie.
6. Tej nocy proponuje mala zmiane w CrashScribe: kalendarz dla Pochodu i dla ROT, a Zew Nocnego Krola uspiony do 3. roku. Kazdy element ma
   wylacznik. Sprawdzi ja autotest 40 dob: zero rozkazow oblezenia i zero zdobytych osad, a dzis w tym czasie bylo 7 rozkazow oblezenia.
7. Nie wymaga nowej kampanii. W trwajacej grze Inni po prostu przestana brac nowe osady przed terminem. Osad, ktore juz trzymaja, nie odbieramy.

---

## 1. Stan dzis

### 1.1 Pomiar [P] - LOG-ROK (linie `UMARLI: Nocny Krol ...`, raz na dobe; doba z najblizszej linii `FABULA: dzien N`)

| Doba | Bandy | Trupy w polu | Osady Innych | Co sie stalo |
|---|---|---|---|---|
| 1 | 4 | 621 | 0 | start: 4 bandy x 154 (+ bohaterowie) |
| ~6 | 4 | ~720 | 0 | pierwszy rozkaz Pochodu: "Gavin (510 zdrowych) RUSZA NA Hornfoot, przewaga 2.3x" (LOG-ROK:2225) |
| 20 | 3 | 924 | 0 | |
| 40 | 4 | 1 535 | 0 | do doby 40: 7 rozkazow "RUSZA NA" |
| 60 | 4 | 2 398 | 0 | |
| 62 | 4 | 2 186 | **3** | Hornfoot (Rogowa Stopa) zdobyty (LOG-ROK:2943; Mends przestawia notabli Hornfoot l.2945-2948) |
| 81 | 4 | 1 948 | **6** | Craster's Keep (LOG-ROK:3174, notable l.3176-3179) |
| 112-123 | 4 | 2 181 -> 5 695 | 6 | **Nocny Krol oblega Mur** ("juz idzie na The Wall" l.~3584, "OBLEZENIE The Wall" 16 dob) z 2 300-3 000 trupow |
| 132 | 4 | 5 718 | **13** | + Fist of the First Men i Frozen Shore (LOG-ROK:3844) |
| 140 | 4 | 6 379 | 13 | szczyt |
| 202 / 219 | 4 | 5 099 / 686 | 6 / 3 | zywi odbijaja osady |
| 251 | **0** | 0 | 0 | "Inni nie maja zadnej bandy w polu" (LOG-ROK:5274) |
| 260 | 4 | 641 | 0 | odrodzenie 4 band (znow ok. 154 kazda) |
| 300 | 4 | 3 098 | 0 | |
| 360 | 4 | 2 268 | 0 | koniec roku; ostatni rozkaz "RUSZA NA Hardhome" |

- Rozkazow Pochodu "RUSZA NA" w roku: 54 (7 do doby 40, 47 pozniej) [P]. Cele: Hornfoot, Craster's Keep, Fist of the First Men, Frozen Shore, Hardhome.
- Przyrosty licznika band w roku (narodziny nowych band): 26 [P]; kazda narodzina = 53 z szablonu + 100 dosypane przez ROT + wodz [K, ponizej].
- Inne sesje: `session-2026-10-08_10-53-59.log` (inna kampania, doba 372): 4 bandy, 3 111 trupow, **3 osady** [P]. Zdanie Jeffa
  "po roku 6 osad" pasuje do LOG-ROK (6 osad w dobach 81-131 i 202-218). Starszych sesji nie ma (CrashScribe trzyma 15).
- Dla porownania, przed Pochodem: "0 zdobytych osad przez ~193 dni kampanii" (CHANGELOG 2026-09-20 (3), l.2372) oraz "0 z 25 od poczatku
  kampanii (dzien 840)" (CS NightKingCall.cs:17) [P, z naszych wczesniejszych pomiarow]. Pomiar 05.10: 0 osad w 5 sesjach (`docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md` 4.1).

### 1.2 Kod [K] - co decyduje o zdobywaniu osad

**ROT (sam z siebie):**
- Oblega tylko banda z >= 500 zdrowych (ROT:952), a potem przy (PartySizeRatio >= 0.8 albo > 2 000 ludzi) i przewadze 2.5x albo przy > 1 000 ludzi i 4.5x (ROT:1105).
- "Kajdany AI" (`OthersAIShackles` = true u Jeffa, `Configs/.../RealmOfThrones/RealmOfThrones_v1.json`): Frozen Shore i Hornfoot < 300 dni,
  Thenn i Frostfang's Camp < 400, Hardhome < 500 nie wolno (ROT:1066-1092). Nasz `Fabula.OthersTranspiler` mnozy te stale x4.33
  (CS Fabula.cs:288-306) -> **1 299 / 1 732 / 2 165 dni** (3,6 / 4,8 / 5,9 roku). Log potwierdza: "progi Innych x4.33 w 1 metodach
  (podmienionych stalych 3)" (LOG-ROK:209).
- **Craster's Keep i Fist of the First Men nie maja zadnej kajdany.** **Mur (ROT_castle60) tez nie** - filtr ROT przepuszcza Mur przed inwazja
  (ROT:1037: `!IsInvasionStarted && !flag && s != TheWall`), a Nocnego Krola zwalnia z wstepnego progu 2.5x (ROT:1060).
- Inwazja na poludnie: gdy WSZYSTKIE osady za Murem (procz Muru i Driftwood Hall) sa Innych (ROT:763-786). To ROT sprawdza sam.
- ROT od rodu bez krolestwa wykonuje tylko oblezenie; rajd/obrona/patrol (willGatherArmy) przepadaja (CHANGELOG 2026-09-20 (3) pkt 3).

**NASZE przyspieszacze (CrashScribe):**
- **Zew Nocnego Krola** (CS NightKingCall.cs:334-434): codziennie zlewa trupy mniejszych band do najsilniejszej, az ta ma 520 zdrowych
  (`NightKingCallTarget`, Config.cs:33), i ustawia premie limitu (`_partySizeGrowth`) tak, by horda byla pelna w 85% (NightKingCall.cs:397-419).
  Skutek: zawsze 15% wolnego miejsca na nekromancje po kazdej bitwie (ROT:1366 - nekromancja "do wolnego miejsca"), a limit rosnie razem z horda.
  To jest dodatnie sprzezenie zwrotne: im wieksza horda, tym wiekszy limit. Wlasna premia ROT rosla tylko o +3 na dobe (ROT:808).
- **Pochod Nocnego Krola** (CS NightKingCall.cs:247-332, cel `PickTarget` 188-225): gotowa horda (>= 500, Config.cs:40) dostaje
  `GetActionForBesiegingSettlement` na kazda fortyfikacje za Murem z przewaga >= **2.0x** (Config.cs:41; ROT wymaga 2.5x).
  **Filtr celu (NightKingCall.cs:194-216) nie sprawdza kajdan ROT ani zadnej daty.** Po nadaniu rozkazu ROT sam go podtrzymuje
  (ROT:952-972: horda >= 500 z rozkazem oblezenia dostaje 999 punktow na TEN SAM cel, z pominieciem filtra celu i kajdan).
- Wszystkie piec przelacznikow siedzi w `Modules/CrashScribe/ModuleData/CrashScribe.settings.xml` (CrashScribe nie ma MCM; Config.cs:48-93).

**Zrodla trupow (stan na dzis, bez zmian od sprawdzenia 05.10):**
- +100 przy narodzinach bandy (ROT:1299-1305) + szablon 53 = 154 z niczego.
- +2 na dobe na bande, gdy jest wolne miejsce (ROT:1307-1313).
- Nekromancja po bitwie (ROT:1315-1381): zdrowi strony przegranej na starcie x udzial bandy x `OthersNecromancyMultiplier` (1.0 u Jeffa),
  do wolnego miejsca. Liczy tez uciekinierow (zdrowi na starcie, nie polegli) i przegrane bandy Innych.
- Ochotnicy "z mapy" (TW RecruitmentCampaignBehavior, rody mniejsze) - szacunek z 05.10: ok. 10 na dobe.
- Nawracanie lordow (`OthersLordConversionRate` 0.05): w LOG-ROK po dobie 252 wodzem bandy Innych jest Tormund Giantsbane (l.5332).

### 1.3 Bilans roku [S] (LOG-ROK)
- Z niczego: ok. 30 narodzin (4 na starcie + 26) x 154 = ok. 4 600 oraz +2 na dobe przy ok. 4 bandach, gdy jest miejsce (do 2 900). Razem do ok. 7 500 na rok.
- Szczyt w polu: 6 379 (doba 140). Wzrost z 2 181 do 5 695 w dobach 100-120 (oblezenia Piesci i Muru: nekromancja wypelnia limit,
  ktory Zew trzyma 15% ponad horda).
- Z poleglych za Murem i przy Murze: tylko nekromancja (czesc liczy uciekinierow zamiast poleglych).

---

## 2. Jak bylo w lore [H]

Kalendarz gry: start 299 AC (CS/Armoury Calendar.cs:17-20, log "Day N of Summer, 299 AC"), rok = 364 dni.

| Rok AC | Ksiazki (ASOIAF) | Serial | Rok gry |
|---|---|---|---|
| 297-298 | Inni zabijaja zwiadowcow Royce'a (prolog AGOT); wighty Othor i Jafer w Czarnym Zamku (AGOT) | s1 | przed startem |
| 299 | **Bitwa na Piesci Pierwszych Ludzi** - setki wightow rozbijaja oboz 300 braci; Inni wygrywaja, ale zadnej warowni nie trzymaja; bunt u Crastera | s3 | 1 |
| 299-300 | Wolni Ludzie (Mance) uciekaja przed Innymi i szturmuja Mur - to zywi, nie Inni | s4 | 1-2 |
| 300 | **Hardhome**: tysiace uwiezionych dzikich, "martwe rzeczy" w lesie i w wodzie (listy Cottera Pyke'a); ataku nie pokazano; do konca "Tanca ze smokami" **Inni nie przekraczaja Muru** | s5 - rzez w Hardhome | 2 |
| ~303-304 | (nie napisane) | s7 finał: Nocny Krol burzy Mur pod Wschodnia Strażnica | 5-6 |

Zrodla:
- Bitwa na Piesci, 299 AC, wygrana Innych: https://awoiaf.westeros.org/index.php/Fight_at_the_Fist
- Konflikt za Murem (przeglad 297-300): https://awoiaf.westeros.org/index.php/Conflict_beyond_the_Wall
- Hardhome w ksiazkach (tylko listy, atak nieopisany): https://awoiaf.westeros.org/index.php/Hardhome ,
  https://awoiaf.westeros.org/index.php/Cotter_Pyke , https://awoiaf.westeros.org/index.php/A_Dance_with_Dragons-Chapter_58
- Wighty (kto wstaje - polegli, takze zwierzeta): https://awoiaf.westeros.org/index.php/Wights
- Upadek Muru w s7 i datowanie sezonow (s7 ok. 304 AC - szacunek fanowskich osi czasu, serial nie podaje roku):
  https://www.hollywoodreporter.com/live-feed/game-thrones-what-walls-destruction-means-final-season-1033289 ,
  https://kvia.com/entertainment/cnn-entertainment/2026/01/20/the-full-game-of-thrones-and-house-of-the-dragon-timeline/

Wnioski z lore:
- W roku 1-2 Inni wygrywaja bitwy w polu i zbieraja poległych (Piesc, Hardhome). Warowni nie trzymaja: ich armia rosnie z umarlych.
- Mur stoi co najmniej do 300 AC (ksiazki), a w serialu do ok. 304 AC.
- Kajdany ROT po x4.33 (3,6 / 4,8 / 5,9 roku) sa zgodne z tym duchem. Wylamuje sie z niego nasz Pochod oraz brak kajdan ROT dla Muru, Piesci i Crastera.

---

## 3. Luki i bledy logiki

**3.1 Pochod lamie kalendarz ROT (glowna przyczyna "6 osad po roku").** [K] NightKingCall.cs:194-216 nie sprawdza kajdan;
[P] pierwszy rozkaz na Hornfoot w dobie ~6 (LOG-ROK:2225), Hornfoot zdobyty w dobie 62. ROT pozwala na to dopiero od doby 1 299.
Frozen Shore (kajdany do 1 299) wziety do doby 132, a na koniec roku rozkaz na Hardhome (kajdany do 2 165). Z 13 osad szczytu 7 to cele,
ktorych ROT nie wolno jeszcze ruszyc (Hornfoot 3 + Frozen Shore ok. 4) [P/S].

**3.2 Pochod obniza prog przewagi z 2.5x do 2.0x** (Config.cs:41) i dziala od doby 1. Pozostale 6 osad (Craster's Keep, Fist) ROT
formalnie dopuszcza, ale sam w praktyce ich nie zdobywal (0 przez 193 i 840 dni, 1.1) [P].

**3.3 Zew robi z horde kule sniezna.** [K] NightKingCall.cs:409-415: `wantLimit = leadMen / 0.85` - limit idzie za horda, wiec kazda wygrana
bitwa moze ja powiekszyc o 15%, a nastepnego dnia znowu jest 15% miejsca. [P] Nocny Krol: 1 609 -> 3 001 zdrowych w dobach 108-123
(LOG-ROK:3474-3747). Bez Zewu wlasna premia ROT rosnie liniowo o +3 na dobe (ROT:808).

**3.4 Premia limitu Zewu skacze co dobe (blad liczenia).** [P] LOG-ROK:3350-3406: "premia 349 -> 701 (baza 1147)", nazajutrz "704 -> 348
(baza 1502)", i tak na zmiane. [K] NightKingCall.cs:407 liczy baze jako `limit - premia - 250`, a BannerKings mnozy premie (x1.5-2,
`docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md` 4.2 C). Znane jako R3, niezrobione.

**3.5 Mur bez zadnej daty.** [K] ROT:1037 i 1060 przepuszczaja Mur przed inwazja; [P] oblezenie Muru w dobach 112-123 (rozkaz ROT, nie
Pochodu: "juz idzie na The Wall - rozkazu nie ruszamy"). Zew sprawil, ze Nocny Krol mial > 2 000 ludzi, a to otwiera warunek ROT:1105.
W lore Mur stoi do roku 5-6 gry.

**3.6 Trupy z niczego (zasada Jeffa H2, zatwierdzona 07.10 - niezrobiona).** [K] ROT:1299-1313; [P] 26 narodzin band w roku, odrodzenie
4 band po calkowitej klesce w dobie 251-260. Projekt gotowy: `docs/REGULY-KRAIN-I-DLUGU-2026-10-06.md` rozdz. 4 (R2, R4, R3). W kodzie Armoury nie ma
jeszcze `UndeadLaw.cs` [K: brak pliku w Armoury/src].

**3.7 Styk z obozami (temat Jeffa z tej nocy).** [K] Armoury NightRest.cs:286 ("umarli nie obozuja") i :449 ("Inni maszeruja noca") -
Inni sa wylaczeni z nocnego postoju. To zgodne z lore (wight nie spi). Po wprowadzeniu obozow 24-6 dla zywych Inni zyskaja jednak ok. 25%
wiecej czasu marszu wzgledem zywych [S]. Nie zmieniac, ale wziac to pod uwage przy kalibracji progow Pochodu.

**3.8 Uboczne (do sprawdzenia, nie przyczyna).** [P] W osadach Innych Mends.LocalLevies co dobe przestawia notabli "byl whitewalker, jest freefolk"
(22 razy w LOG-ROK, np. l.2945, 3448, 4731). [K] Kod przestawia notabla na kulture OSADY (CS Mends.cs:4151-4164), a ROT robi osade
"whitewalker" przy zdobyciu (ROT:1527). Skoro osada wraca do freefolk, cos ja przestawia; podejrzewam kulture BannerKings wedlug ludnosci [S].
Mozliwy skutek: w osadach Innych zywi ochotnicy freefolk dla garnizonu i band Innych. Sprawdzic przy R4 (punkt "w bandzie Innych nie ma zywych").

---

## 4. Propozycje

### N1. Kalendarz Innych (CrashScribe) - P0, mala, TEJ NOCY
Co: (a) Pochod nie wybiera celu przed jego dniem; (b) ROT nie dostaje oblezenia przed dniem celu (postfiks na ROT `OnAiHourlyTick`);
(c) banda, ktora juz ma rozkaz na zamkniety cel, dostaje patrol przy swojej siedzibie; (d) Zew spi do pierwszego terminu.
Liczby:
- `NightKingSiegeFromDay` = **728** (poczatek 3. roku) dla Fist of the First Men i Craster's Keep oraz minimum dla kazdego celu.
  Uzasadnienie: do 300 AC (rok 2) Inni w ksiazkach wygrywaja bitwy, ale niczego nie trzymaja; Jeff: "podboj dopiero po kilku latach".
- `NightKingRespectShackles` = true: Hornfoot i Frozen Shore 300 x skala, Thenn i Frostfang 400 x skala, Hardhome 500 x skala, skala = `FabulaTimeScale`
  (4.33 -> 1 299 / 1 732 / 2 165). Uzasadnienie: to kalendarz samego ROT, juz przeliczony na nasz rok - nie wymyslamy nowego.
- `NightKingWallFromDay` = **2 184** (poczatek 7. roku, ok. 305 AC). Uzasadnienie: Mur pada w s7 (~304 AC), po Hardhome (s5);
  2 184 > 2 165 (Hardhome), wiec kolejnosc jak w serialu.
- `NightKingCallFromDay` = **728**. Uzasadnienie: jedynym celem Zewu jest prog oblezenia, a przed terminem oblezen nie ma; bez Zewu bandy
  zostaja po 150-300 (pomiar przed 16.09).
Gracza: tak (Inni pozniej). Nowa kampania: nie (w trwajacej dzialaja od razu wedlug dnia od startu kampanii; zdobytych osad nie odbiera).
Ryzyko: male - bez Zewu i Pochodu Inni zachowuja sie jak ROT przed 16.09 ("stoja"); po 728 wraca dzisiejsze zachowanie.
Zaleznosci: brak; nie koliduje z 169-171 ani z 162-168.

### N2. Naprawa liczenia premii Zewu (R3) - P1, mala, tej nocy albo zaraz po N1
Co: zgodnie z `REGULY-KRAIN-I-DLUGU-2026-10-06.md` 4.2 C: limit z zywego modelu (`PartySizeLimitModel.GetPartyMemberSizeLimit` przy
premii 0 i 100 -> nachylenie), transfer liczony przed premia, nie obnizac premii zajetej hordzie, `NightKingMarchMin` = `NightKingCallTarget` (520).
Liczby: bez nowych. Gracza: nie. Nowa kampania: nie. Ryzyko: male. Zaleznosci: N1 (przed dniem 728 Zew i tak spi, wiec N2 mozna dolozyc pozniej).

### N3. Granica wzrostu hordy z Zewu - P1, mala
Co: premia Zewu najwyzej do `max(premia ROT, 0)` + `NightKingCallMaxBonus` = **600**. Dzis nie ma gornej granicy (NightKingCall.cs:409).
Liczby: 600 = ok. 1 140 ludzi (520 / 0.85 + zapas), czyli jedna horda zdolna wziac zamek z garnizonem ok. 450 przy 2.5x. Dzis wyrasta do 5 000+.
Gracza: tak (mniejsze hordy). Nowa kampania: nie. Ryzyko: male. Zaleznosci: znika po R4 (wtedy wzrost i tak z puli cial).

### N4. R2 - koniec trupow z niczego (Armoury) - P0, srednia, NIE tej nocy (osobny etap)
Co: dokladnie `REGULY-KRAIN-I-DLUGU-2026-10-06.md` 4.2 A pkt 1-6 (prefiksy na ROT `OnMobilePartyCreated`, `OnDailyTickParty`, ochotnicy z mapy,
pula wyrzutkow, dezercja, niewola wightow; wyjatek 616 na start). Zatwierdzone przez Jeffa 07.10 (H2, "616 na start - TAK").
Liczby: z projektu. Gracza: tak. Nowa kampania: nie wymaga (dziala od teraz), ale bilans otwarcia 616 tylko w nowej.
Ryzyko: srednie (piec latek na ROT i TW). Zaleznosci: przed R4; z N1 sie nie gryzie.
Dlaczego nie tej nocy: piec latek w roznych miejscach; autotest 40 dob pokaze narodziny, ale nie pokaze skutku w roku.

### N5. R4 - pula cial za Murem i przy Murze; nekromancja z poleglych, nie z uciekinierow - P1, duza
Co: `REGULY...` 4.2 B pkt 7-14. Dopiero to realizuje "rosna tylko z poleglych". Wtedy N1 robi sie warunkiem "kalendarz I trupy":
Pochod rusza tylko, gdy horda zebrana z ciał ma przewage, a ciał przybywa tylko z bitew za Murem.
Gracza: tak. Nowa kampania: zalecana (pula na start). Ryzyko: srednie. Zaleznosci: R2, ksiega ludzi (cialo = polegly regionu, R13).

### N6. Licznik przyczyn wzrostu w logu UMARLI - P2, mala
Co: do dziennej linii UMARLI dopisac: narodziny band (+154), +2 dziennie, nekromancja, ochotnicy z mapy, nawroceni lordowie, zywi w bandach.
Bez tego bilans 1.3 to szacunek. Moze isc razem z paczka 169 (sam log obiegu), jesli ta ma miejsce na ludzi.

### N7. Notable freefolk w osadach Innych (3.8) - P2, mala
Co: Mends.LocalLevies pomija osady, ktorych wlascicielem jest klan Innych (`OwnerClan.Culture.StringId == "whitewalker"`) - nie przestawia
notabli i nie czysci ochotnikow. Najpierw potwierdzic, kto przestawia kulture osady. Gracza: nie. Ryzyko: male.

Harmonogram docelowy (rok = 364 doby, dzien od startu kampanii; po N1, potem N4/N5):

| Rok gry (AC) | Co moga Inni | Skad rosna |
|---|---|---|
| 1 (299) | bitwy w polu za Murem (Wolni Ludzie, zwiadowcy Strazy); zadnych oblezen; Zew spi | dzis: z niczego + nekromancja; po N4/N5: tylko polegli za Murem |
| 2 (300) | jw. (odpowiednik Hardhome to bitwy, nie zdobycie) | jw. |
| 3 (301), od doby 728 | Piesc Pierwszych Ludzi, Twierdza Crastera; Zew budzi sie | jw. |
| 4 (302), od doby 1 299 | + Rogowa Stopa, Mrozny Brzeg (kajdany ROT x4.33) | jw. |
| 5 (303), od doby 1 732 | + Thenn, Kly Mrozu | jw. |
| 6 (304), od doby 2 165 | + Hardhome | jw. |
| 7 (305), od doby 2 184 | Mur | jw. |
| po Murze | poludnie - **tylko** gdy wszystkie osady za Murem sa Innych (warunek ROT, ROT:763-786) | jw. |

Najwczesniejszy mozliwy podboj na poludnie od Muru: doba 2 184 + czas zdobycia Muru, czyli 7. rok (ok. 305 AC). Do tego musza miec
wszystkie osady za Murem, a po R4 tez dosc ciał z poleglych.

---

## 5. Do zrobienia tej nocy vs na pozniej

### TEJ NOCY (CrashScribe, osobny DLL; budowa, autotest 40 dob, wgranie tylko na "wgraj")

**A. `CrashScribe/src/Config.cs`**
- po linii 43 (blok Pochodu) dopisac:
  `public static int NightKingSiegeFromDay = 728;`  // Fist, Craster i minimum dla kazdego celu (rok 3)
  `public static bool NightKingRespectShackles = true;` // kajdany ROT 300/400/500 x FabulaTimeScale takze dla Pochodu i postfiksu
  `public static int NightKingWallFromDay = 2184;`  // Mur od 7. roku (s7 ~304 AC)
  `public static int NightKingCallFromDay = 728;`   // Zew spi do pierwszego terminu
  `public static bool NightKingCalendarEnabled = true;` // wylacznik calosci N1
- w `Load` (po linii 87) piec nowych `case` wzorem linii 83-87.

**B. `CrashScribe/src/NightKingCall.cs`**
1. Nowa metoda `static double Day()` = `Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow`. To ten sam zegar co ROT
   (ROT:1068-1088) i Fabula (Fabula.cs:88-95).
2. Nowa metoda `static int OpenDay(Settlement s)` po `StringId`: `ROT_castle60` (Mur) -> `NightKingWallFromDay`;
   `ROT_castle45`, `castle_N6` -> `300 x k`; `town_S6`, `town_S4` -> `400 x k`; `town_S7` -> `500 x k` (k jak `Fabula.ScaleK`, Fabula.cs:280-286,
   gdy `NightKingRespectShackles`); kazda inna -> `NightKingSiegeFromDay`; wynik = `max(wartosc, NightKingSiegeFromDay)`.
   Id z ROT `ROT.Misc/ROTSettlements.cs:16, 136, 132, 138, 308, 396`.
3. `PickTarget`, po linii 196: `if (Config.NightKingCalendarEnabled && Day() < OpenDay(s)) { table: ", zamknieta do dnia N"; continue; }`.
4. `Daily`, po linii 342 (`ww` znany):
   (a) jesli kalendarz wlaczony i `Day() < NightKingCallFromDay`: raz na 30 dob linia "Zew Nocnego Krola: spi do dnia 728 (dzis N)"; potem krok (c) i `return`;
   (b) przy pierwszym wywolaniu po przebudzeniu premia startuje od stanu ROT - bez zmian w kodzie Zewu;
   (c) dla kazdej bandy z `DefaultBehavior == AiBehavior.BesiegeSettlement`, ktora nie jest `Busy`, a `TargetSettlement` ma `Day() < OpenDay(...)`:
   `SetPartyAiAction.GetActionForPatrollingAroundSettlement(mp, ww.HomeSettlement, MobileParty.NavigationType.Default, false, false)`
   (sygnatura TW SetPartyAiAction.cs:151) + linia w logu "Kalendarz Innych: <wodz> zawrocony spod <osada> (otwarta od dnia N)".
5. NOWY postfiks (mozna w NightKingCall albo w Fabula.InstallWars, wzor Fabula.cs:323-349): na ROT
   `ROTOthersCampaignBehavior.OnAiHourlyTick(MobileParty, PartyThinkParams)` (prywatny nasluch wolany przez delegata, wiec patch zadziala):
   dla wpisow `__1.AIBehaviorScores` z `AiBehavior == BesiegeSettlement` i `Party is Settlement s` z `Day() < OpenDay(s)` ->
   `__1.SetBehaviorScore(dane, -1f)` (TW PartyThinkParams.cs:98). Wybor celu bierze tylko wynik scisle wiekszy od -1
   (`REGULY...` H1, TW AiPartyThinkBehavior.cs:76-86). Licznik "odrzucone oblezenia ROT przed terminem" dopisywany do dziennej linii Pochodu.
   Bez tego punktu Nocny Krol i tak mogl ruszyc na Mur sam (3.5).
6. Linia startowa Pochodu (NightKingCall.cs:257-259) dopisuje terminy: "kalendarz: Piesc/Craster od 728, Rogowa Stopa/Mrozny Brzeg od 1299, ...".

**C. `Modules/CrashScribe/ModuleData/CrashScribe.settings.xml`** (przy wgraniu, gra zamknieta, kopia .bak) - piec nowych kluczy z opisem.

**Sprawdzenie autotestem 40 dob (nowa kampania):**
- 0 linii "RUSZA NA" (dzis 7 do doby 40); tabela Pochodu pokazuje "zamknieta do dnia ...";
- "Zew Nocnego Krola: spi do dnia 728" zamiast transferow; trupy w polu w dobie 40 ponizej 1 535 (dzis) [P do porownania];
- 0 linii "OBLEZENIE", `UMARLI ... 0 osad`; licznik odrzuconych oblezen ROT = 0 albo mały (bandy < 500);
- linia "Fabula: ... progi Innych x4.33 ... (podmienionych stalych 3)" bez zmian;
- 0 raportow bledow CrashScribe z `NightKingCall.*`.
- Test wczytania zapisu z doby 360 (`Armoury-2026-10-08_11-32-39.log` / sesja 11-32-37): Pochod i Zew spia (360 < 728), osady trzymane zostaja.

### NA POZNIEJ
- N2 (naprawa premii Zewu, R3) - zaraz po N1, ma sens od doby 728.
- N3 (gorna granica premii) - razem z N2.
- N4 = R2 (Armoury): osobny etap, zatwierdzony przez Jeffa (H2); autotest 40 dob (narodziny 54 zamiast 154, brak +2) + rok.
- N5 = R4 (pula cial) - po R2 i ksiedze ludzi.
- N6 (licznik przyczyn w logu UMARLI) - mozna dolaczyc do 169, jesli tam pasuje; inaczej razem z R2.
- N7 (notable freefolk w osadach Innych) - po potwierdzeniu, kto przestawia kulture osady.

### Kolizje z paczkami w toku
- 169 (log obiegu): brak kolizji; N6 moze tam trafic.
- 170 (BetterEconomy): brak.
- 171 (zbrojenie zalog): zalogi osad Innych sa pomijane w Armoury (ArmyClothing.cs:212 `Undead.Party(garrison)`); 171 musi zachowac to pominiecie
  (trup nie kupuje w miescie). Do tego punkt 3.8: w osadach Innych notable sa freefolk, wiec rekrut "z tym co ma" moglby byc zywy.
- 162-168 (projekt ekonomii): Nieumarli sa poza obiegiem pieniadza (SoldierPay.cs:340, WarLedger.cs:40); N1-N3 niczego tu nie zmieniaja.

## Pytania do Jeffa (tylko kanon / rozgrywka)
1. Czy Inni w roku 1-2 maja w ogole nie oblegac (moja propozycja), czy wolno im zaatakowac Piesc juz w 1. roku, jak w ksiazkach (299 AC), ale bez trzymania osady?
2. Mur: 7. rok (po Hardhome, jak w serialu) - czy wolisz "nigdy, dopoki gracz nie pozwoli" (ksiazki nie doszly do upadku Muru)?
