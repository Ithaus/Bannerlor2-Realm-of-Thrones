# Plan: pobor od ludnosci i sprzet ochotnikow (2026-10-04)

Zgloszenie Jeffa: "jade do wioski, tam chlopi, werbuje - musze ich doposazyc; a co, jak sa ludzie
wyzszego tieru, skad maja sprzet - przeciez jest zycie swiata, sa inni zbrojni nie w sluzbie, co moga
kupic sprzet i miec swoj"; "jak teraz beda pokazywac sie ludzie w werbunku - nie ma losowosci, pewnie
ci, co nie maja pracy, albo ci, co chca opuscic wioske - i jaka bedzie zasada werbunku?"
Zasada z docs/HISTORY.md: pobor zabiera ludzi z pola (mniej rak = mniejsza produkcja, polegli trwale,
powrot po demobilizacji).

## 1. Co juz jest (rozpoznanie dekompilacji, BK = BannerKings.Redux)

| Rzecz | Stan | Dowod |
|---|---|---|
| Model ochotnikow | BK `BKVolunteerModel` (ROT, RBL, BEE nie maja wlasnego) | bk/BannerKings/Main.cs:211 |
| Ludnosc | BK liczy prawdziwych ludzi w klasach (chlopi panszczyzniani, dzierzawcy, rzemieslnicy, szlachta, niewolnicy); wies = hearth x 4-6 | PopulationManager.cs:478-494 |
| Pula poboru | BK `MilitaryData.Manpowers` na klase = liczba x militarnosc (baza 0.1), +1% pulapu dziennie | MilitaryData.cs:179-195 |
| Werbunek zabiera czlowieka | TAK - gracz i AI: `DeduceManpower` zmniejsza pule i liczbe ludzi klasy | RecruitmentApplyInternalPatch.cs:13-22, RecruitmentOnDonePatch.cs:37 |
| Mniej chlopow = mniejsza produkcja | TAK - BEE mnozy produkcje wsi przez chlopi BK / (hearth x 3.5), 0.2-1.5 | BEE_VillageProductionCalculatorModel.cs:99-117 |
| Nadwyzka rak | BK `LandData.WorkforceExcess` = dostepni robotnicy - potrzebni na pola i pastwiska | LandData.cs:212-219 |
| Pojawianie sie ochotnikow | vanilla: kazdy pusty slot (6 na notabla) wypelnia sie z szansa `GetDraftEfficiency` ~0.5 DZIENNIE; slot awansuje (log2(moc/tier) x 1%) do tieru 4 - za darmo | RecruitmentCampaignBehavior.cs:215-289 |
| Sprzet rekruta | DTE wklada do zbrojowni pelny komplet szablonu (zbroja, kon, bron, amunicja) - za darmo, gracz i AI | dte RecruitmentPatch.cs:56-200, EveryoneCampaignBehavior.cs:870-892 |
| Zloto za werbunek | gracz -> notabl (BK); AI -> w nicosc (vanilla `ApplyInternal`) | RecruitmentOnDonePatch.cs:38; RecruitmentCampaignBehavior.cs:606-639 |
| Garnizon | auto-werbunek omija pule BK (nie zabiera ludzi) | GarrisonRecruitmentCampaignBehavior.cs:85-92 |
| Powrot z wojska | BK ma `UpdatePopFromSoldiers`, wolany tylko przy pokojowym rozwiazaniu (ta sama kultura) | BKClanBehavior.cs:1231 |
| Przyrost naturalny | BK `BKGrowthModel` liczy wzrost, ale nic go nie stosuje do klas codziennie (do potwierdzenia w logu) | PopulationData.cs:107 |

Wniosek: NIE budujemy drugiej ludnosci - podpinamy sie pod BK.

## 2. ZASADA WERBUNKU (propozycja)

1. **Kto sie zglasza.** Ochotnik pojawia sie w pustym miejscu u notabla z szansa:
   `baza BK x (nadwyzka rak + nedza)`:
   - nadwyzka rak = `WorkforceExcess / AvailableWorkForce` wsi (ludzie, dla ktorych nie ma pracy na roli -
     mlodsi synowie, bezrolni);
   - nedza = ta sama co u wyrzutkow (bieda miasta, wojna, spalone wsie, glod, bezprawie) - ci, co chca odejsc;
   - gdy wies potrzebuje kazdych rak (brak nadwyzki, spokoj, dobrobyt) - tylko nikla stala (`RecruitBaseWilling`).
   Zostaje warunek BK: pula poboru > 0.
2. **Chlop przychodzi z tym, co ma** (tier 1: odzienie, narzedzie/wlocznia) - to jego dobytek, nie z targu.
3. **Wyzszy tier = ktos, kto kupil sprzet.** Awans ochotnika w puli notabla (tier 2-4) nastepuje tylko,
   gdy notabl (z wlasnego zlota) kupi brakujace czesci kompletu na targu miasta (wies: miasto, do ktorego
   nalezy). Towar schodzi z polki, zloto do miasta. Nie ma towaru albo zlota - awans cofniety.
4. **Rekrut przynosi to, co kupil** - komplet DTE przy werbunku zostaje (to ten zakupiony sprzet).
5. **Zloto za werbunek AI idzie do notabla**, jak u gracza (dzis znika).
6. **Nowa partia AI nie dostaje darmowego kompletu dla calego skladu** (DTE OnMobilePartyCreated) - poza startem gry.
7. **Garnizon** - auto-werbunek zabiera ludzi z puli BK (do zrobienia).
8. **Demobilizacja** - rozwiazane partie oddaja ludzi do ludnosci osady (do zrobienia).
9. **Wyrzutki** (OutlawLaw) - zabierani i oddawani takze w ludnosci BK, nie tylko w hearth (do zrobienia).

## 3. Bledy logiki znalezione przy okazji (audyt 04.10)

- `PopulationLaw` (renty od ludnosci) - latka sie NIE wpinala (dwie wersje metody BK - AmbiguousMatch);
  a gdyby sie wpiela, renty bylyby zlotem z niczego (~7 mln/dzien). Do przebudowy: renty jako przeplyw
  z kasy wsi/miasta do pana.
- `PopulationLaw` liczy wlasna ludnosc (hearth x k, wies ~50 tys. jako symbol), BK liczy swoja (wies 2-3 tys.).
  Pobor idzie po BK; renty po symbolu - do ujednolicenia.
- `Rations` - ciecie jedzenia 3-4 razy (NAPRAWIONE, CHANGELOG 23).
- Statyczne stany (WorkshopLaw cache przedmiotow, IronBank, MarketGlut, ArmsPricing) nie zerowane przy
  nowej grze/wczytaniu - przeciek miedzy kampaniami w jednej sesji, ryzyko zepsucia save (stare ItemObject).

## 4. Kolejnosc wdrozenia (kazdy krok osobno: build, wgranie, CHANGELOG, commit)

1. Zerowanie stanow statycznych przy nowej grze/wczytaniu.
2. Renty od ludnosci jako przeplyw (z kasy osady), naprawa wpiecia.
3. Zasada "kto sie zglasza" (pkt 2.1).
4. Awans ochotnika tylko z kupionym sprzetem (pkt 2.3).
5. Zloto za werbunek AI do notabla + koniec darmowego kompletu nowej partii AI (2.5, 2.6).
6. Garnizon, demobilizacja, wyrzutki w ludnosci BK (2.7-2.9).
