<!-- Rozpoznanie wf_25247c07-d96 (20.10 noc), tylko odczyt; rozpoznania z dowodami: rozpoznanie-tablica-ogloszen-2026-10-21.json -->

# Tablica ogłoszeń (Notice board): projekt m99

- **Dla kogo:** Jeff (część 1–5) oraz programista, czyli drugie konto Claude (część 6).
- **Na czym oparte:** trzy rozpoznania z 20.10 (zadania gry, mody z sieci, nasze mody). Ważne twierdzenia sprawdziłem sam: kod gry 1.4.8, `n27-calosc`, pliki ROT. Lista sprawdzeń jest w części 7, a rzeczy niesprawdzone w części 8.
- **Kolejność prac:** E1, potem E3 (opis i rozmowa), potem E2 (kapitan straży), potem E2b (sołtys).
- **Tory:** E1 M, E3 M, E2 L, E2b M.
- **Zasady:** wgranie dopiero po udanym autoteście i nie wtedy, gdy Jeff gra.

---

## 0. W skrócie (dla Jeffa)

1. **Tak, da się.** W menu miasta pojawi się opcja **"Read the notice board"**. Pokaże listę ludzi, którzy mają dla Ciebie robotę: kupców, rzemieślników, hersztów gangów i lordów w mieście, sołtysów i bogaczy z wsi tego miasta oraz **kapitana straży**.
2. **Klik w osobę otwiera kartę ogłoszenia.** Na karcie jest: co trzeba zrobić, za ile, kto płaci i czy go na to stać, ile zostało czasu, gdzie ta osoba jest. Pod spodem są przyciski:
   - **"Go and see"**: wchodzisz do sceny miasta i stajesz przy tej osobie, czyli Twoje "visit";
   - **"Talk now"**: rozmowa bez chodzenia;
   - **"Mark on the map"**: flaga na mapie.

   W rozmowie osoba sama opowiada o zadaniu zwykłymi kwestiami z gry. Ty przyjmujesz, wysyłasz towarzysza z ludźmi albo odmawiasz.
3. **Kapitana straży w grze nie ma.** Nie ma go w czystej grze, w ROT ani w Banner Kings. Dodamy go po swojemu. Będzie to dowódca straży miasta, a w Królewskiej Przystani kapitan Złotych Płaszczy. Wywiesza **listy gończe** na konkretne bandy z okolicy. Nie tworzymy nowych stałych postaci, bo gra ma już taki wzór: "Talk to the garrison commander".
4. **Gra ma już zadania "wybij bandytów", ale dają je inni:** sołtys (kryjówka, dezerterzy), kupiec (rozbójnicy, kłusownicy, zasadzka na karawanę) i herszt gangu (łowcy nagród). Tablica je pokaże. Lista jest w części 2.
5. **Nagrody są prawdziwe, nic nie bierze się z niczego.**
   - Za zadania z gry płaci zleceniodawca z własnej kiesy. To już działa w Armoury od WGRANIA 96.
   - List gończy kapitana opłaca kasa miasta, i to tylko z nadwyżki ponad zapas kupców i najbliższą ratę dla pana. Gdy miasto jest biedne, może dopłacić pan miasta. Gdy nie ma pieniędzy żaden z nich, listu nie ma.
   - Łup bandy (jej kiesa, skarbiec kryjówki) i tak bierzesz Ty, bo działa już nasza zasada "kiesa pokonanego dla zwycięzcy". Nagroda miasta jest dodatkowa, a nie podwójna.
6. **Ile za bandę:** banda 20 ludzi to ok. **375 denarów** przy żołdzie straży 5 na głowę. Jeśli banda paliła wsie miasta albo ma herszta z imienia, wychodzi więcej, najwyżej dwa razy tyle. Wyliczenie jest w części 4.

---

## 1. Co zobaczysz w grze (dla Jeffa)

### 1.1 Menu miasta

W głównym menu miasta, wysoko, obok dzielnicy karczm, pojawia się **"Read the notice board"**. Jeśli ktoś na tablicy ma dla Ciebie zadanie, opcja ma wykrzyknik, jak karczma czy zamek w grze. Nie chowamy jej do "Town affairs", żeby była od razu widoczna.

Tablicy nie ma, gdy jesteś w mieście w przebraniu. Wtedy nie możesz chodzić po mieście jak zwykle, więc to prościej i bezpieczniej.

### 1.2 Tablica: lista

Przykład dla Lannisportu (wygląd przybliżony, napisy w grze po angielsku):

```
NOTICE BOARD - LANNISPORT
Six notices are pinned up today.

[!] Gunthor, merchant - Help with brigands - 375 - 18 days - in the tavern
[!] Lord Damion Lannister - Garrison troops - 640 - 25 days - in the keep
[!] Captain of the watch - WANTED: Jon Rivers and his bandits (20) - 375 - 9 days
[!] Old Tom, headman of Oxcross - Bandit base near Oxcross - 313 - 27 days - in Oxcross
    Wenda, landowner of Fairmarket - Draught animals - 520 - 14 days - in Fairmarket
    Lord Tyland Lannister - Raid enemy lands - 1 800 - absent (with his army near Riverrun)
Back to town
```

Kolejność na liście:
1. najpierw ludzie w mieście (do nich możesz pójść od razu);
2. potem wsie miasta;
3. na końcu nieobecni.

W każdej grupie wyżej stoją lepiej płatne zlecenia.

### 1.3 Karta ogłoszenia

Po kliknięciu w wpis widzisz kartę:

```
HELP WITH BRIGANDS
Posted by Gunthor, merchant of Lannisport. He is in the tavern.

"Brigands have been preying on the roads. Break two of their bands
 within twenty days and I will make it worth your while."     (słowa zleceniodawcy z gry)

Pay: 375 - he pays from his own purse, and he can afford it.
     (Or send a companion with 10 men for about 6 days.)
Time: the offer stays up about 18 more days.

Go and see him        - wchodzisz do karczmy i stajesz przy nim
Talk to him now       - rozmowa bez chodzenia po mieście
Mark him on the map   - flaga
Back to the board
```

Różne rodzaje wpisów mają różne przyciski:
- **Osoba w mieście:** wszystkie trzy przyciski. Jeśli nie masz wstępu, na przykład do sali lorda, "Go and see" jest wyszarzone z powodem z gry. Wtedy zostaje "Talk now".
- **Osoba we wsi:** tylko "Ride to Oxcross" (flaga na mapie i opis). Rozmawiasz na miejscu. Nie robimy rozmów "przez tablicę" z kimś, kto stoi 30 km dalej, bo byłoby to nierealne i ryzykowne.
- **Nieobecny lord:** "Mark his party on the map".
- **Kapitan straży:** "Speak to the captain of the watch".

### 1.4 Kapitan straży: rozmowa

Rozmowa odbywa się w szybkiej scenie, jak w grze przy "Talk to the garrison commander". Twarz kapitana to najwyższy rangą piechur z załogi miasta, a w Królewskiej Przystani **Gold Cloak Captain** z ROT. Przykład:

```
Captain: You've read the watch's notice? Jon Rivers and his bandits - twenty
         cutthroats - have been working the roads by Oxcross. They burned two farms
         this season. The council pays 563 for them, coin on the table once they're
         broken. Whatever they carry is yours.
  > I'll hunt them down.      -> "They were last seen near Oxcross. I've marked it for you."
  > Who pays, exactly?        -> "The town's coffers. If those run dry, Lord Damion has promised the rest."
  > Not now.                  -> "The notice stays up. First to bring them down gets paid."
```

Jak działa list gończy:
- **Dowód** to rozbita banda: wygrywasz z nią bitwę albo czyścisz kryjówkę, w której siedzi. Pieniądze dostajesz od razu, z komunikatem.
- **List jest publiczny.** Jeśli przed Tobą bandę rozbije lord AI, nagrodę dostaje on. Jeśli rozbije ją straż miasta albo karawana, list zostaje zdjęty bez wypłaty, bo to ich praca.

### 1.5 Sołtys: "banda nęka wieś" (E2b, później)

Gdy banda paliła albo łupiła wieś miasta, a sołtys nie ma innego zadania, na tablicy w mieście wisi jego list. Ma przycisk "Ride to Oxcross". Rozmawiasz z sołtysem we wsi. Płaci kiesa wsi. Dostajesz też trochę relacji z sołtysem.

### 1.6 "Your tasks" (E3)

Na górze tablicy jest krótka lista Twoich zadań w toku: co, dla kogo, ile dni zostało, gdzie jest zleceniodawca. Przy każdym zadaniu jest "Mark on the map".

### 1.7 Czego tablica nie robi

- Nie tworzy nowych postaci.
- Nie zmienia zadań z gry.
- Nie zmienia tego, jak lordowie AI się zachowują.
- Nie pozwala przyjąć zadania sołtysa bez jazdy do wsi.

---

## 2. Jakie zlecenia (dla Jeffa)

### 2.1 Zadania z gry, które tablica pokaże

**Trudność (postęp gracza)** to liczba od 0,1 do 1. Gra liczy ją z lenn, siły klanu, sławy, poziomu i wielkości drużyny. Na początku nowej kampanii jest blisko 0,1, więc obowiązują dolne kwoty.

Kolumna "U nas" to kwota po skali Armoury przy żołdzie Twojej drużyny 5 na głowę. Jeśli Twoi ludzie biorą więcej, kwota rośnie w tej samej proporcji.

| Zadanie (jak w grze) | Kto daje | Kiedy się pojawia | Gra płaciła | U nas (trudność 0,1 → 1) |
|---|---|---|---|---|
| Bandit Base Near {wieś}: oczyść kryjówkę | sołtys | bezpieczeństwo miasta ≤ 50 | 3 000 | ok. 310 → 625 |
| {X} Needs Help With Brigands: rozbij 2–8 band | kupiec albo bogacz wiejski | zajęta kryjówka w pobliżu | 550 → 1 900 | 375 → 1 425 |
| Extortion by Deserters: dezerterzy we wsi | sołtys | bezpieczeństwo ≤ 50 | 1 220 → 5 000 | 600 → 2 300 |
| Army of Poachers | kupiec | bezpieczeństwo ≤ 60 | 800 → 3 500 | 375 → 2 000 |
| Captured by Bounty Hunters: odbij ludzi gangu | herszt gangu | kryjówka w pobliżu | 3 000 | ok. 310 → 625 |
| Caravan Ambush | kupiec albo rzemieślnik z karawaną | miasto bez portu | 1 300 → 4 000 | 625 → 2 600 |
| Escort Merchant Caravan | kupiec | bezpieczeństwo ≤ 50 | do 8 000 | nie policzone |

Inne zadania z gry, które też będą na tablicy (razem 43 rodzaje):
- **Rzemieślnik:** sprzedaż towaru po uczciwej cenie, drogie surowce.
- **Herszt gangu:** oszustwo na zakładach, rekruci, broń, sztylety z kuźni, paserstwo, rywalski gang, pułapka na bogacza.
- **Sołtys:** zboże, stado, zwierzęta pociągowe, narzędzia.
- **Bogacz wiejski:** robotnicy z jeńców, szkolenie ludzi, spór o pastwiska, rodzinna waśń, zaginiona córka.
- **Lordowie:** zaopatrzenie armii, żołnierze do załogi, konie, nauczyciel, schwytaj rywala (5 000), najazd na wroga (15 000), podbój (20 000), przemytnicy, zbiór podatków, zwiad załóg, marnotrawny syn, szpieg.

ROT ma tylko własne zadanie zaciągu i Krwawe Gody. Nie są to ogłoszenia mieszczan, więc na tablicy ich nie ma. Banner Kings nie ma własnych zadań.

### 2.2 Nowe: list gończy kapitana straży (E2)

- **Cel:** jedna konkretna banda z okolic miasta (nie kryjówka, nie banda na morzu, nie Inni).
- **Ile listów:** w każdym mieście najwyżej 2 naraz.
- **Kolejność:** najpierw bandy, które szkodziły wsiom i karawanom tego miasta, potem największe.
- **Termin:** 10 dni. Po terminie list znika, bez kary dla Ciebie.
- **Dowód:** banda rozbita. Płaci kasa miasta, a w razie potrzeby dopłaca pan miasta.
- **Bonus:** bezpieczeństwo miasta +2 i sława +1 dla tego, kto rozbił bandę. Czysta gra za "Pomoc z rozbójnikami" daje +5 i +1, więc to mniej.

### 2.3 Nowe: sołtys, "banda nęka wieś" (E2b)

Zasada jak u kapitana, ale:
- płaci kiesa wsi;
- dostajesz relację z sołtysem +2;
- list pojawia się tylko wtedy, gdy sołtys nie ma zadania z gry (każdy człowiek ma najwyżej jedno ogłoszenie).

### 2.4 Ile ogłoszeń będzie na tablicy

Gra trzyma najwyżej:
- 3 zadania od mieszczan na miasto;
- 2 na wieś;
- zadania u 10–20% lordów.

Do tego dochodzą do 2 listów kapitana. Typowo wyjdzie więc ok. 3–10 wpisów na miasto. Liczby notabli w miastach ROT nie sprawdziłem, więc to szacunek.

---

## 3. Skąd pieniądze (dla Jeffa)

| Kto płaci | Za co | Granica |
|---|---|---|
| Zleceniodawca z gry (kupiec, sołtys, lord) | zadania z gry | jego kiesa; jak ma mniej, płaci tyle, ile ma, i dostajesz komunikat (Armoury, działa od WGRANIA 96) |
| Kasa miasta | list gończy kapitana | tylko nadwyżka ponad zapas kupców i najbliższe raty dla pana i korony; jeden list najwyżej 1/4 tej nadwyżki |
| Pan miasta (właściciel) | dopłata, gdy kasa miasta za mała | jego złoto ponad rezerwę na żołd |
| Kiesa wsi | list sołtysa (E2b) | nadwyżka kiesy wsi; jeden list najwyżej 1/4 |
| Banda | jej kiesa i skarbiec kryjówki | już dziś trafiają do zwycięzcy (zasady Armoury), osobno od nagrody |

Biedne albo zadłużone miasto po prostu **nie wywiesza listów**. Kapitan powie: "The council's coffers are empty". Bandy biedne miasta nie znikają, tylko nikt za nie nie płaci. Tak było w średniowieczu i tak działa nasza ekonomia: nic z niczego.

Złoto z listów to przelew kasa → gracz albo kasa → lord, zapisany w księdze obiegu. W logu nie może być ani jednej wypłaty "z niczego".

---

## 4. Liczby nagród z uzasadnieniem

### 4.1 Reguła (decyzja projektu)

**Nagroda = N × D × S × 1,25 × (1 + dopłata)**

| Symbol | Co to jest | Wartość | Dlaczego |
|---|---|---|---|
| N | ilu ludzi trzeba posłać | liczba zdrowych ludzi bandy, min. 10, maks. 60 | miasto posłałoby co najmniej tylu strażników, ilu bandytów; gra do kryjówki z ok. 10–15 bandytami wymaga 10 ludzi, więc proporcja ta sama |
| D | ile dni roboty | 3, a 4 gdy większość bandy jest konna | 1 dzień szukania, 1 dzień pościgu i walki, 1 dzień powrotu; gra daje 5–12 dni na 2–8 band, czyli ok. 1,5–2,5 dnia na bandę |
| S | dniówka na głowę | średni żołd jednego człowieka z załogi tego miasta, a bez załogi 5 | list jest publiczny i kwotę trzeba ustalić z góry, więc liczy się koszt własnej straży miasta, a nie Twojej drużyny |
| 1,25 | narzut za ryzyko i rannych | jak w Armoury dla wszystkich zadań | jedna reguła z zadaniami gry (QuestPay8) |
| dopłata | szkody i herszt | +25% za każdą wieś miasta spaloną lub złupioną przez tę bandę w ostatnich 60 dniach, +15% za każdą rozbitą karawanę lub wóz wiejski okręgu, +50% gdy bandę prowadzi herszt-bohater; razem najwyżej +100% | miasta płaciły więcej za głośnych zbójów; górna granica pilnuje, żeby nie przesadzić |

**Dwie granice:**
- list wolno wywiesić tylko wtedy, gdy nagroda ≤ 1/4 wolnej kasy miasta (albo dopłaci pan miasta);
- przy wypłacie kasa nie schodzi poniżej zapasu kupców i rat.

Kwota jest **ustalana przy wywieszeniu** i stała do końca.

### 4.2 Przykłady

| Banda | Wyliczenie | Przy S = 5 | Przy S = 10 |
|---|---|---|---|
| 8 łupieżców (N = min 10) | 10 × 3 × S × 1,25 | 188 | 375 |
| **20 bandytów** | 20 × 3 × S × 1,25 | **375** | **750** |
| 20, spaliła 2 wsie (+50%) | 375 × 1,5 | 563 | 1 125 |
| 20, 2 wsie i herszt (+100%, sufit) | 375 × 2 | 750 | 1 500 |
| 40, w większości konni (D = 4) | 40 × 4 × S × 1,25 | 1 000 | 2 000 |
| 60 i więcej (N = 60) | 60 × 3 × S × 1,25 | 1 125 | 2 250 |

Typowego żołdu załóg w ROT nie sprawdziłem, dlatego są dwie kolumny. Linia logu pokaże prawdziwe S każdego miasta.

### 4.3 Porównanie z innymi zleceniami u nas

Na początku kampanii:
- kryjówka od sołtysa: ok. 310;
- "Pomoc z rozbójnikami" od kupca: 375 za 2 bandy;
- list gończy na jedną bandę 20 ludzi: 375.

To ten sam rząd wielkości. Zadania gry rosną z Twoim postępem, a list gończy rośnie z siłą bandy i szkodami, jakie zrobiła.

Dla porównania stare kwoty gry: 3 000 za kryjówkę to ok. 7 miesięcy dochodu rycerza. Dlatego Armoury je przycina.

### 4.4 Przykład kasy miasta

Załóżmy, że miasto ma w kasie 90 000, zapas kupców wynosi 70 000, a do rat trzeba trzymać 72 000.
- Wolna kasa: 90 000 − 72 000 = 18 000.
- Jeden list może więc kosztować najwyżej 4 500, czyli list za 375 mieści się bez trudu.
- Miasto z kasą na podłodze ma 0 wolnej kasy. List powstanie tylko wtedy, gdy pan miasta ma wolne złoto.

### 4.5 Ta sama banda w dwóch zleceniach

Jeśli ta sama banda liczy się do zadania kupca ("rozbij 2 bandy") i do listu kapitana, dostajesz obie zapłaty. Płacą dwaj różni ludzie prawdziwymi pieniędzmi, więc nie ma tu złota z niczego.

---

## 5. Decyzje projektu (każda z wyłącznikiem w MCM)

| # | Decyzja | Wyłącznik | Dlaczego |
|---|---|---|---|
| D1 | Tablica tylko w miastach (nie w zamkach i wsiach) | `NoticeBoard` | o to prosił Jeff; zamki mają "Request a meeting" z gry |
| D2 | Wsie miasta na tablicy, ale rozmowa tylko na miejscu (flaga) | `NoticeBoardVillages` | realizm, bez ryzyka dialogów zależnych od miejsca |
| D3 | Kapitan bez nowych postaci: twarz z załogi miasta, w `town_EW4` Gold Cloak Captain | `WatchBounties` | wzór z gry ("Talk to the garrison commander"); 0 nowych bohaterów i 0 obciążenia zapisu |
| D4 | List publiczny: płaci także lordowi AI, który rozbije bandę (nie wrogowi miasta, nie straży i nie karawanie) | `WatchBountyAiLords` | jedna reguła dla gracza i AI; AI nie zmienia zachowania, tylko czasem dostaje zapłatę; przepływ mierzony (próg L-7) |
| D5 | Pan miasta dopłaca, gdy kasa miasta za mała | `WatchBountyLordTopUp` | żeby biedne, a najbardziej nękane okolice też miały listy |
| D6 | Bez nowego typu zadania w zapisie (bez `QuestBase`); listy w naszym zapisie tekstowym | brak, decyzja techniczna | wyłączenie albo usunięcie modu nie psuje zapisu |
| D7 | Linia "I read your notice..." w rozmowie, gdy przyszedłeś z tablicy | `NoticeBoardOpening` | rozmowa zaczyna się od zadania; kwestie zadania zostają z gry |

---

## 6. Dla programisty

### 6.0 Ramy

- **Gałąź:** `w-toku/m99-tablica`.
- **Pliki:** `Armoury/src/NoticeBoard.cs` (E1), `NoticeBoard.Talk.cs` (E3), `NoticeBoard.Watch.cs` (E2), `NoticeBoard.Headman.cs` (E2b).
- **CLAUDE.md 7:**
  - żadnego `GameMenu.SwitchToMenu` z menu oczekiwania (tablica to zwykłe menu, więc `SwitchToMenu` wolno);
  - wyjątki licz per wpis, nie gaś funkcji (`Stumble` i licznik);
  - nic nie rób co klatkę.
- **Kontrola całości:** po buildzie (kod wyjścia 0, kotwice x1) autotest 120 dób. Grupy etap2, etap8_d1 i Obieg mają wyjść jak w biegu bazowym, poza nowymi przepływami E2.
- **Rejestracja:**
  - menu: `ArmouryBehavior.cs` ok. :2294 (wzór `TownDebt.AddMenus(starter)`);
  - doba: ok. :2652 (wzór `TownDebt.Daily()` z `DayProfile.Lap`);
  - zapis: ok. :1384 (wzór `SaveText.Sync(dataStore, "arm_towndebt", ...)`);
  - zdarzenia: ok. :1447 i :1610 (tam już wiszą `MobilePartyDestroyed`, `RaidCompletedEvent`, `MapEventEnded`).
- **TownMenus (m95-menu):** nie dopisywać `arm_board_enter` do planu przenosin. Opcja ma zostać w menu "town".

### 6.1 E1: tablica z zadaniami gry (tor M, ok. 4–6 h z autotestem)

**Menu.** Wzór z gry: `EncounterGameMenuBehavior` "request_meeting", czyli lista osób jako powtarzalna opcja menu, z wykrzyknikiem z `MenuHelper`:

```csharp
starter.AddGameMenuOption("town", "arm_board_enter", "{=!}Read the notice board", EnterCond, EnterCons, false, 2);
starter.AddGameMenu("arm_board", "{=!}{ARM_BOARD_TEXT}", BoardInit);
starter.AddGameMenuOption("arm_board", "arm_board_item", "{=!}{ARM_BOARD_ITEM}", ItemCond, ItemCons, false, -1, isRepeatable: true);
starter.AddGameMenuOption("arm_board", "arm_board_back", "{=!}Back to town", BackCond, a => GameMenu.SwitchToMenu("town"), isLeave: true);
starter.AddGameMenu("arm_board_note", "{=!}{ARM_NOTE_TEXT}", NoteInit);
// opcje karty: arm_note_visit, arm_note_talk, arm_note_mark, arm_note_captain (E2), arm_note_back -> SwitchToMenu("arm_board")
```

**EnterCond.**
- `Settlement.CurrentSettlement?.IsTown`, klucz `NoticeBoard`, `!Campaign.Current.IsMainHeroDisguised`.
- Wykrzyknik: `args.OptionQuestData |= AvailableIssue`, gdy któryś wpis ma `Issue.IsOngoingWithoutQuest`.

**BoardInit.**
- Zbiera `List<Entry>` na świeżo przy każdym otwarciu, bez pamięci podręcznej (BK raz w tygodniu sam zamyka zadania).
- Wywołuje `args.MenuContext.SetRepeatObjectList(list)`.
- Tło: `SetBackgroundMeshName(st.SettlementComponent.WaitMeshName)`, tytuł przez `args.MenuTitle`.
- Pusta lista: tekst "No notices are pinned up today." i tylko "Back".

**ItemCond.**
- `args.MenuContext.GetCurrentRepeatableObject() as Entry`.
- `MBTextManager.SetTextVariable("ARM_BOARD_ITEM", ...)`.
- Gdy wpis ma bohatera: `MenuHelper.SetIssueAndQuestDataForHero(args, hero)`.

**ItemCons.**
- `_sel = args.MenuContext.GetSelectedObject() as Entry`, potem `GameMenu.SwitchToMenu("arm_board_note")`.

**Skąd wpisy** (`Entry`: Hero, IssueBase, Kind {Town, Village, Lord, Absent, Watch, Headman}):

| Źródło | Kod |
|---|---|
| Notable miasta, bohaterowie bez partii, lordowie w partiach w mieście | `IssueManager.GetIssuesInSettlement(st, onlyNotables: false)` (IssueManager.cs:771); odfiltrować powtórki po `IssueOwner` |
| Wsie miasta | `st.BoundVillages` → `v.Settlement.Notables`, `h.Issue != null` |
| Ród pana miasta poza miastem | `st.OwnerClan.Heroes` z `IsLord && Issue != null && CurrentSettlement != st`, jako wpis Absent (tylko flaga) |
| Tylko | `issue.IsOngoingWithoutQuest` (zadania w toku idą do "Your tasks", E3) |

**Treść karty** (każdy wpis w osobnym try/catch, licznik `nagroda nieczytelna` albo `tekst z tokenami`):
- **Tytuł i opis:** `issue.Title`, `issue.Description` (IssueBase.cs:241, :258). Jeśli po `ToString()` w tekście zostaje `{`, wziąć sam tytuł i zwiększyć licznik. Opis przyciąć do ok. 600 znaków.
- **Nagroda:** nowy `internal static int QuestPay8.ShownReward(IssueBase)` woła chroniony getter `RewardGold` przez `AccessTools.PropertyGetter(typeof(IssueBase), "RewardGold")` (wzór QuestPay8.cs:1112). Przechodzi przez postfiks `QuestPay8.RewardShown` (:455), więc pokazuje kwotę po skali, tę samą co w rozmowie. Flaga `_raw` musi być wtedy false.
- **"Can he pay":** `issue.IssueOwner.Gold >= reward`; jeśli nie, tekst "he has only about N" (zaokrąglone).
- **Rozwiązanie zastępcze:** `issue.IsThereAlternativeSolution`, potem `GetTotalAlternativeSolutionNeededMenCount()` (IssueBase.cs:395) i liczba dni (QuestPay8 używa `GetBaseAlternativeSolutionDurationInDays()`; nazwę wersji "Total" sprawdzić w dekompilacji).
- **Termin:** `issue.IssueDueTime` (pole publiczne, :101); `CampaignTime.Never` daje "no set time". Po terminie gra zdejmuje zadanie z szansą 20% na dobę, więc karta pisze "may be taken down any day".
- **Gdzie jest osoba:** `LocationComplex.Current.GetLocationOfCharacter(hero)?.Name`, albo wieś, albo partia.

**Przyciski karty.** Każdy przed wykonaniem sprawdza na świeżo `hero.Issue == entry.Issue && IsOngoingWithoutQuest`. Jeśli warunek nie zachodzi: komunikat "This notice has been taken down." i powrót do tablicy.

| Przycisk | Warunek | Skutek (dokładnie jak w grze) |
|---|---|---|
| Go and see | `hero.CurrentSettlement == st`, `!hero.IsPrisoner`, `PlayerEncounter.LocationEncounter != null`, `LocationComplex.Current != null`, lokacja ≠ null; dostęp: `Campaign.Current.Models.SettlementAccessModel.CanMainHeroAccessLocation(st, loc.StringId, out dis, out why)`, a gdy brak, opcja wyszarzona z podpowiedzią `why` | `GameMenuOverlay.ExecuteTroopAction`, przypadek Conversation (vm\GameMenuOverlay.cs:212-228): `"alley"` → `GetLocationWithId("center")`; `CampaignEventDispatcher.Instance.OnPlayerStartTalkFromMenu(hero)`; `PlayerEncounter.LocationEncounter.CreateAndOpenMissionController(loc, null, hero.CharacterObject)` |
| Talk now | osoba w mieście | QuickConversation (:239-248): `CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, null, false, false, false, true), new ConversationCharacterData(hero.CharacterObject, null, false, false, false, true))` |
| Mark on the map / Ride to | zawsze | `Campaign.Current.VisualTrackerManager.RegisterObject(...)` dla partii albo osady (wzór Wayfinder.cs:116 i HideoutSpotter.cs:49); komunikat z kierunkiem jak w HideoutSpotter |

Przed wejściem do sceny i przed rozmową **`Log.Info` jedna linia** z bohaterem, lokacją i miastem. Jeśli gra wywali się do pulpitu, to będzie ostatnia linia w logu.

### 6.2 E3: rozmowa i opis (tor M, ok. 3 h, po E1)

1. **Linia otwierająca.** Zapamiętać `NoticeBoard.FromBoard = hero`, czyścić przy końcu rozmowy. Dopisać:

   ```csharp
   starter.AddPlayerLine("arm_board_issue", "hero_main_options", "issue_offer",
       "{=!}I read your notice on the board in {ARM_BOARD_TOWN}.", BoardIssueCond, null, 111);
   ```

   Linia z gry `hero_give_issue` ma priorytet 110, nie ma skutku i prowadzi prosto do `issue_offer` (LordConversationsCampaignBehavior.cs:794). Nasza robi to samo, tylko stoi wyżej. Warunek: `Hero.OneToOneConversationHero == FromBoard && hero.Issue?.IsOngoingWithoutQuest == true`, gracz nie jest jeńcem i nie płynie na tratwie (jak w grze :1897-1912).
2. **Słowa zleceniodawcy na karcie.** `issue.IssueBriefByIssueGiver` (IssueBase.cs:128), oczyszczone jak opis. Gdy zostają tokeny, pokazać `Description`.
3. **"Your tasks" na górze tablicy.** `Campaign.Current.QuestManager.Quests` z `IsOngoing`: tytuł, `QuestGiver`, dni do `QuestDueTime`, gdzie jest zleceniodawca, flaga.
4. **Pełne kwestie kapitana** (E2) w trzech odmianach: Westeros "the watch", Wolne Miasta "the city guard", `town_EW4` "the Gold Cloaks".

### 6.3 E2: listy gończe kapitana straży (tor L, jedna noc z testem 120 dób)

**Stan** (zapis tekstowy `arm_board`, wersja `v1|`, Import tolerancyjny, Reset jak `TownDebt.Reset`):

```
Notice { Town (StringId), Band (MobileParty.StringId), Men, Days, BonusPct, S, Sum, Payer {Town|Lord},
         PostedDay, DueDay, TakenByPlayer, Flagged, Kind {Watch|Headman}, Giver (headman StringId) }
Harm   { Band -> lista (dzien, wies|karawana, okreg) }   // 60 dni, czyszczone codziennie
```

Przy 2 listach na 97 miast to ok. 200 rekordów po ok. 80 B, czyli ok. 16 KB. Limit napisu zapisu to 32 KB (SaveText). **Bez `QuestBase` i bez `SaveableTypeDefiner`** (decyzja D6).

**Szkody.**
- `RaidCompletedEvent` z bandą po stronie napastnika: wpis "wieś". Wzór `Rumours.OnRaidCompleted`, ArmouryBehavior.cs:1448.
- `MapEventEnded`, gdy banda wygrała z karawaną albo wozem wiejskim: wpis "karawana". Wzór `Gallows.OnMapEventEnded` (Gallows.cs:535): strona przegrana, `MapFaction.IsBanditFaction`, `Undead.Party`.
- Okręg miejsca: `OutlawLaw.RegionAt(pos)` albo `OutlawLaw.NearestTownOf(pos)` (OutlawLaw.cs:283, :2017).

**Doba** (`NoticeBoard.Daily`, z `DayProfile.Lap`):
1. **Zdjąć listy:** banda nie istnieje albo nieaktywna, minął termin, miasto zmieniło pana na wroga, miasto oblężone. Przy oblężeniu list stoi, ale nowych nie ma.
2. **Rozłożyć bandy na miasta:** jeden przebieg po bandach bandyckich (nazwę kolekcji w 1.4.8 sprawdzić, np. `MobileParty.AllBanditParties`), miasto = `OutlawLaw.NearestTownOf(bandPos)`.
   - Pominąć: `Undead.Party(mp)`, bandy w kryjówce (`mp.CurrentSettlement?.IsHideout`, to są zadania sołtysa z gry), bandy na morzu (właściwość NavalDLC do sprawdzenia), bandy poniżej 5 ludzi.
3. **Wywiesić list,** gdy miasto ma mniej niż `WatchBountiesPerTown` listów, a banda jeszcze nie ma listu. Wybór: najwyższa dopłata, potem najwięcej ludzi.
   - `S = MountedWage.FullTotalWage(garrison) / garrison.MemberRoster.TotalManCount`, a gdy załogi brak, `QuestPay8.FallbackRate` (5).
   - `Sum` według części 4.1.
   - Wolna kasa: `Free = max(0, town.Gold − max(TownPurse.Reserve(town), DsTowns.TdKeep(st)))` (TownPurse.cs:230, DsTowns.TownDebt.cs:40).
   - Wywiesić, gdy `Sum ≤ Free / 4`, albo gdy przy `WatchBountyLordTopUp` pan miasta ma ponad rezerwę na żołd co najmniej `Sum` (klucz `Ai Gear Gold Reserve`, jak w CraftOrderClanPurse).
   - Rada z długiem `TownDebt` nie wywiesza listów. Potrzebny nowy `TownDebt.OwesAny(Settlement)` czytający ich słownik; dziś publicznego odczytu nie widzę.
4. **Liczniki pokrycia:** miasta z bandą, z listem, bez listu z podziałem na powody. Powód "inne" musi wynosić 0.

**Rozmowa z kapitanem.** Wzór z gry: `LordNeedsGarrisonTroopsIssueQuestBehavior`, sprawdzony, tablica-tmp\LordNeedsGarrison.cs:673-709 i :490.
- **Twarz:**
  - w `town_EW4`: `MBObjectManager.Instance.GetObject<CharacterObject>("goldcloak_captain")` (ROT-Troops.xml:8969, Cavalry, poziom 26);
  - w innych miastach: piechur z najwyższym poziomem z `town.GarrisonParty.MemberRoster.GetTroopRoster()`, a na start `OwnerClan.Culture.EliteBasicTroop`, jak w grze.
- **Partia rozmówcy:** `town.GarrisonParty.Party`. Bez załogi opcja jest wyszarzona: "The watch has no one to speak for it right now." Gra robi tak samo.
- **Otwarcie:** `CampaignMapConversation.OpenConversation(new ConversationCharacterData(CharacterObject.PlayerCharacter, PartyBase.MainParty), new ConversationCharacterData(captain, town.GarrisonParty.Party, noHorse: true))`.
- **Dialog:** `DialogFlow.CreateDialogFlow("start", 310)`, warunek `_captainTalk && CharacterObject.OneToOneConversationCharacter == _captainChar`.
  - Flaga `_captainTalk` jest konieczna, bo ten sam szablon żołnierza rozmawia też w innych miejscach.
  - Priorytet 310 stoi nad 300 z gry, na wypadek starego `_selectedCharacterToTalk` w zadaniu o załodze.
  - Flagę czyścić przy końcu rozmowy (zdarzenie końca rozmowy w 1.4.8, sygnatura do sprawdzenia).
  - Opcje gracza: przyjmij (`TakenByPlayer`, flaga na bandzie, komunikat), kto płaci, nie teraz.
  - Nazwa bandy z plotek ("Jon Rivers and his bandits", Rumours.cs:756-824). Funkcję wystawić jako `internal`, jeśli jest prywatna (nie sprawdzone).

**Dowód i wypłata.** `MobilePartyDestroyed(mp, by)` (CampaignEvents:703) oraz `MapEventEnded`. Banda po stronie przegranej z mniej niż 25% ludzi z chwili listu liczy się jak rozbita.

| Kto rozbił | Wypłata |
|---|---|
| Gracz albo armia prowadzona przez gracza | `Hero.MainHero` |
| Lord AI (`IsLordParty`, `LeaderHero != null`, ród niewrogi frakcji miasta) i `WatchBountyAiLords` | `LeaderHero` |
| Straż, milicja, patrol, karawana, inni bandyci, ktoś nieznany | list zdjęty, 0 wypłaty, licznik powodu |

Kolejność płatników:
1. Kasa miasta: `x = min(Sum, Free)` przez `GiveGoldAction.ApplyForSettlementToCharacter(st, payee, x, true)`. Metoda sprawdzona (GiveGoldAction:52) i już używana u nas (CastlePurse.cs:682).
2. Reszta od pana miasta: `GiveGoldAction.ApplyBetweenCharacters(owner, payee, y, true)`, do jego rezerwy.
3. Brak pieniędzy: niedopłata z komunikatem w brzmieniu QuestPay8: "The council could pay only N of the promised M."

Do tego `town.Security += 2` i `GainRenownAction.Apply(payee, 1)`.

**Księga obiegu.** Sprawdzić w `MoneyLedger`, czy przelew osada → bohater trafia do Obiegu i do kontrolki Szczelność (164a) jako przelew z płatnikiem. Jeśli księga rozpoznaje przelewy po ramce wywołania, dodać ramkę "listy goncze".

### 6.4 E2b: sołtys (tor M, ok. 3 h, po teście E2)

- **Rodzaj listu:** Headman.
- **Zleceniodawca:** sołtys (`IsHeadman`) wsi, którą banda spaliła albo złupiła w ostatnich 30 dniach. Tylko gdy `headman.Issue == null`.
- **Płatnik:** złoto osady wsi przez `GiveGoldAction.ApplyForSettlementToCharacter(village.Settlement, payee, x, true)`. Granica: 1/4 nadwyżki kiesy wsi; regułę rezerwy wsi w Armoury trzeba ustalić (nie sprawdzone, szukać w DsVillage i VillageBook).
- **Rozmowa:** we wsi, przez naszą linię w `hero_main_options` (priorytet 111) "About your notice - the band harrying {VILLAGE}...". Dalej kwestie jak u kapitana.
- **Nagroda:** wzór z 4.1, S = żołd załogi miasta tej wsi. Na końcu `ChangeRelationAction.ApplyPlayerRelation(headman, 2)`.

### 6.5 Klucze MCM (Settings.cs, nowa grupa `--- Notice board ---`, komentarz po angielsku dla gen_mcm.py)

| Klucz | Domyślnie | Etap | Opis w MCM (skrót) |
|---|---|---|---|
| `NoticeBoard` | true | E1 | "Read the notice board" in town: everyone with work for you, with pay, payer, time and where to find them; off = no board |
| `NoticeBoardVillages` | true | E1 | the board also lists the notables of the town's villages (ride there to talk) |
| `NoticeBoardOpening` | true | E3 | coming from the board, you can open with "I read your notice..." |
| `WatchBounties` | true | E2 | the captain of the watch posts bounties on bands near the town, paid from the town's spare coin |
| `WatchBountiesPerTown` | 2 | E2 | most bounties a town keeps posted at once |
| `WatchBountyDays` | 10 | E2 | days a bounty stays up |
| `WatchBountyAiLords` | true | E2 | a lord who breaks a posted band is paid too (not the watch, patrols or caravans) |
| `WatchBountyLordTopUp` | true | E2 | the town's lord pays what the coffers cannot |
| `HeadmanBounties` | true | E2b | headmen of harried villages post bounties paid from the village purse |

Nowe klucze nie istnieją jeszcze w `Armoury.json` Jeffa, więc zadziałają wartości domyślne (CLAUDE.md 7).

### 6.6 Linie logu (ASCII, prefiksy stałe)

```
Tablica ogloszen (m99): wpieta - menu town 1/1, podmenu 2/2, linia rozmowy 1/1; klucze: Notice Board TAK, Villages TAK, Opening TAK
Tablica ogloszen (m99): dzien N, otwarcia A, wpisow sr. B (miasto C, wsie D, lordowie E, nieobecni F, straz G, soltysi H), odwiedziny I, rozmowy teraz J, flagi K, z tablicy do zadania L, odmowy dostepu M, zdjete przed kliknieciem O, nagroda nieczytelna P, tekst z tokenami Q, koszt otwarcia sr. R ms, potkniecia S
Tablica ogloszen (m99): odwiedziny - <hero> w <lokacja> (<miasto>)            // przed wejsciem do sceny
Tablica ogloszen (m99): rozmowa teraz - <hero> (<miasto>)

Listy goncze (m99): wpiete - zdarzenia 3/3 (MobilePartyDestroyed, MapEventEnded, RaidCompleted), dialog kapitana 1/1, zapis arm_board v1
Listy goncze (m99): dzien N, miasta z banda A/97, z listem B, bez listu: brak kasy C, limit D, oblezone E, dlug rady F, inne G; czynne H (nowe I, przyjete przez gracza J); wyplacone K za L zl (gracz M/N zl, lordowie O/P zl; z kas miast Q, od panow R), niedoplata S, z niczego 0; zdjete T (straz/karawana U, banda znikla V, termin W, zmiana pana X); kasa ponizej Keep po wyplacie 0; zapis arm_board Y B; koszt Z ms; potkniecia K2
Listy goncze (m99): list - <miasto> <banda> (<N> ludzi, <D> d, S <s>, szkody +<p>%) = <Sum>, platnik <kasa|pan>, wolna kasa <Free>
Listy goncze (m99): wyplata - <miasto> <banda> obiecane <Sum>, kasa <a> + pan <b>, niedoplata <c>, dla <gracz|lord id>; kasa przed <g0> po <g1> (Keep <k>)
```

### 6.7 Progi sprawdz_logi (nowe grupy w tools/sprawdz_logi.py, wzór bloku `etap8_d1` :14474)

**`--grupa m99tablica` (E1 i E3)**
- **T-1:** linia "wpieta" i komplet 1/1, 2/2, 1/1.
- **T-2:** linia doby w każdej dobie okna, bez pierwszej i ostatniej.
- **T-3:** potknięcia 0 i 0 linii `ERROR in NoticeBoard*`.
- **T-4:** "nagroda nieczytelna" 0.
- **T-5:** "tekst z tokenami" ≤ 2% wpisów.
- **T-6:** koszt otwarcia średnio ≤ 5 ms.
- **T-7 (INFO):** rozkład wpisów. Autotest otwiera tablicę hakiem testowym, patrz 6.9.

**`--grupa m99listy` (E2 i E2b)**
- **L-1:** linia "wpiete" i komplet.
- **L-2:** linia doby w każdej dobie.
- **L-3:** z niczego 0, a rachunek się zgadza: obiecane = kasa + pan + niedopłata, w każdej wypłacie.
- **L-4:** "kasa ponizej Keep po wyplacie" 0.
- **L-5:** "inne" (bez listu bez powodu) 0 w każdej dobie od d2.
- **L-6:** od d7 co najmniej 1 list czynny, jeśli są bandy (bez band INFO).
- **L-7:** wypłaty lordom w oknie ≤ 1% wpływów kas miast z linii "Kasy miast (169c)". Gdy próg nie przejdzie, domyślne `WatchBountyAiLords = false` i raport dla Jeffa.
- **L-8:** zapis `arm_board` ≤ 16 KB.
- **L-9:** koszt doby ≤ 3 ms.
- **L-K:** potknięcia 0.

**Regresja:** grupy `etap2`, `etap5`, `etap8_d1` i Obieg porównane z biegiem bazowym. Różnice wolno mieć tylko w nowych przepływach "listy goncze".

### 6.8 Ryzyka i zabezpieczenia

| Ryzyko | Skąd | Zabezpieczenie |
|---|---|---|
| Gra wywala się do pulpitu przy wejściu do sceny z menu | brak spotkania gracza w lokacji, `null` lokacji, przebranie, brak wstępu | dokładnie kod z `GameMenuOverlay`; warunki z 6.1; przebranie ukrywa tablicę; wejście tylko z opcji zwykłego menu (gra robi to samo w karczmie i zamku, EncounterGameMenuBehavior:336), nigdy z okienka wyboru ani z menu oczekiwania; linia logu przed wejściem |
| Rozmowa z bohaterem, który właśnie odszedł albo którego zadanie zamknął BK | zamknięcie zadania przez gubernatora BK, lord AI załatwiający zadanie | ponowne sprawdzenie przy kliknięciu; komunikat "taken down" |
| Dialog kapitana przejmuje inne rozmowy z tym samym żołnierzem | szablon żołnierza, nie bohater | flaga `_captainTalk`, czyszczona na końcu rozmowy; priorytet 310 |
| Brak załogi w mieście | partia rozmówcy musi istnieć (wzór z gry) | opcja wyszarzona z powodem |
| Zapis | nowy typ zadania w zapisie | brak nowego typu; napis `arm_board` z wersją; brak bandy po wczytaniu zdejmuje list |
| Ekonomia | przelewy z kas miast | Keep (zapas kupców i raty), najwyżej 1/4 wolnej kasy, L-3, L-4, L-7, księga Obieg |
| Tokeny w tekstach zadań | część tekstów ustawia zmienne dopiero w rozmowie | czyszczenie i licznik (T-5) |
| Zgodność ROT i BK | BK zamyka zadania; ROT poprawia `GangLeaderNeedsToOffloadStolenGoods`; `ROT_Hideouts` | lista budowana na świeżo; bandy w kryjówkach pominięte w E2 |
| Inne mody rozmów (AIInfluence, Diplomacy) | mogą dodawać kwestie albo przejmować początek rozmowy | test ręczny z włączonymi modami (6.9 pkt 3); nie sprawdzone |
| Wydajność | przegląd band co dobę | jeden przebieg po bandach, kubełki po miastach; L-9 |

### 6.9 Test w grze

1. **Build:** kod wyjścia 0, kotwice x1. Wgranie próbnej DLL zgodnie ze zgodą na autotest, potem przywrócenie zatwierdzonych.
2. **Autotest 120 dób.** Hak testowy `NoticeBoard.Test` (wzór `DistrictWorks.Test.cs`) w d5, d30 i d90 dla 10 miast, w tym `town_EW4`, miasto Dorzecza i Wolne Miasto:
   - buduje listę bez UI i loguje liczby;
   - czyta nagrodę i termin każdego wpisu;
   - sprawdza lokację osób w mieście bez wchodzenia do sceny;
   - dobiera twarz kapitana.

   Potem `m99tablica`, `m99listy` i regresja. Zapis i wczytanie z czynnymi listami robi autotest zapisu.
3. **Test ręczny** (10–15 minut, poza grą Jeffa; robi go Jeff albo Claude za jego zgodą):
   - otworzyć tablicę w Królewskiej Przystani i w dwóch innych miastach;
   - "Go and see" do kupca w karczmie i do lorda w sali;
   - "Talk now" z notablem;
   - flaga na wieś;
   - rozmowa z kapitanem i przyjęcie listu;
   - rozbicie bandy i wypłata z komunikatem;
   - zapis i wczytanie z czynnym listem;
   - wczytanie po wyłączeniu `WatchBounties`, bez błędu;
   - to samo z włączonym AIInfluence.
4. **Wgranie** po udanym teście, z kopią. Nigdy, gdy Jeff gra.

### 6.10 Kolejność i czas

1. **E1 (M, 4–6 h):** sama tablica z zadaniami gry daje Jeffowi 80% tego, o co prosił. Wgrywamy osobno.
2. **E3 (M, ok. 3 h):** linia otwierająca, słowa zleceniodawcy, "Your tasks".
3. **E2 (L, jedna noc):** listy gończe, osobny test 120 dób i decyzja o L-7.
4. **E2b (M, ok. 3 h):** sołtysi.

Każdy etap to osobna DLL i osobne wgranie (zasada "jedna zmiana naraz"). Miejsce w kolejce nocy wyznacza PRZEKAZANIE. Propozycja: E1 po pracach obozowych.

---

## 7. Co sprawdziłem sam (dowody) i poprawki do rozpoznań

1. **"Visit" i "Talk now" w grze.** `GameMenuOverlay.ExecuteTroopAction` (tablica-tmp\vm\GameMenuOverlay.cs:184-253):
   - Conversation: `GetLocationOfCharacter`, alley → center, `OnPlayerStartTalkFromMenu`, `CreateAndOpenMissionController(location, null, character)`;
   - QuickConversation: `CampaignMapConversation.OpenConversation(...)`.

   Potwierdzone.
2. **Lista zadań z miasta.** `IssueManager.GetIssuesInSettlement(settlement, onlyNotables)` (IssueManager.cs:771) zbiera notabli, potem `HeroesWithoutParty` bez notabli, potem bohaterów z partii w osadzie. API w IssueBase:
   - `IssueDueTime` (pole publiczne, :101);
   - `Title` (:241), `Description` (:258), `IssueBriefByIssueGiver` (:128);
   - `IsOngoingWithoutQuest` (:263);
   - `RewardGold` chronione (:283);
   - wypłata z niczego `GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, RewardGold)` (:765-767).

   Potwierdzone.
3. **Wzór Armoury.** QuestPay8.cs:
   - `Premium = 1.25f`, `FallbackRate = 5`;
   - `MakePledge`: N i D z rozwiązania zastępczego, robocizna = n × d × rate × 1,25;
   - `Rate()` = pełny żołd drużyny gracza na głowę (:336-348);
   - postfiks `RewardShown` przycina kwotę w ofercie (:455-480);
   - getter przez `AccessTools.PropertyGetter` (:1112).

   Opis klucza w Settings.cs: "clearing a hideout pays about 440 instead of 3,000". Potwierdzone.
4. **Kasa miasta.** `TownPurse.Reserve` (zapas kupców; dolna granica z gry 10 000 + 12 × dobrobyt) i `TownPurse.Spare` (TownPurse.cs:230-266), `DsTowns.TdKeep` (podłoga i najbliższe raty, DsTowns.TownDebt.cs:40). Potwierdzone.
5. **Rozmowa z żołnierzem, który nie jest bohaterem, działa w grze.** `LordNeedsGarrisonTroopsIssueQuestBehavior`: "Talk to the garrison commander" w menu "town", twarz to piechur z najwyższym poziomem z załogi, `CampaignMapConversation.OpenConversation(..., new ConversationCharacterData(char, GarrisonParty.Party))`, dialog `CreateDialogFlow("start", 300)` z warunkiem `OneToOneConversationCharacter == _selectedCharacterToTalk` (tablica-tmp\LordNeedsGarrison.cs:673-709, :490). Potwierdzone. To zamyka niesprawdzony punkt rozpoznania o rozmowie z postacią bez Hero, w wariancie z partią załogi.
6. **Menu-lista osób w grze.** "Request a meeting with someone" jest w menu `town_guard` i `castle_guard`, a nie w głównym menu zamku. To powtarzalna opcja (`SetRepeatObjectList`, `GetCurrentRepeatableObject`, `MenuHelper.SetIssueAndQuestDataForHero`), a rozmowa idzie przez `CampaignMission.OpenConversationMission` w scenie spotkania (EncounterGameMenuBehavior.cs:253, :258, :271, :2739-2833). Potwierdzone. To jest wzór menu tablicy.
7. **Wejście w zadanie w rozmowie.** Linia gracza `hero_give_issue` prowadzi do `issue_offer`, priorytet 110, bez skutku (LordConversationsCampaignBehavior.cs:794). Potwierdzone, na tym oparta linia E3.
8. **Pozostałe API:**
   - `CampaignEvents.MobilePartyDestroyed` (IMbEvent<MobileParty, PartyBase>, CampaignEvents:703), już podpięte u nas (ArmouryBehavior.cs:1447);
   - `GiveGoldAction.ApplyForSettlementToCharacter` (GiveGoldAction:52, u nas CastlePurse.cs:682);
   - `SettlementAccessModel.CanMainHeroAccessLocation` (DefaultSettlementAccessModel:135).

   Potwierdzone.
9. **ROT:** `town_EW4` = King's Landing (ROT-Map settlements.xml); `goldcloak_captain` to "Gold Cloak Captain", occupation Soldier, poziom 26, default_group Cavalry (ROT-Troops.xml:8969). Potwierdzone.
10. **Poprawki do rozpoznania "nasze-mody":**
    - `MultiSelectionInquiryData` jest w 12 plikach Armoury, nie w 17;
    - `CampaignMapConversation` nie jest dziś nigdzie używane w naszym kodzie (grep 0), więc tablica będzie pierwszym użyciem;
    - najbliższy nasz wzór wejścia do sceny z menu to `SideVillageHome.cs:475` (`VillageEncounter.CreateAndOpenMissionController`);
    - menu miasta porządkuje m95-menu (`TownMenus.cs`), więc opcję tablicy trzeba zostawić poza planem przenosin.

## 8. Czego nie sprawdziłem

- Prawdziwe kwoty w kampanii Jeffa. W logach nie ma jeszcze wypłaty za zadanie. Typowego żołdu załóg ROT (S) też nie znam, dlatego przykłady są przy S = 5 i S = 10.
- Ile notabli i zadań ma typowe miasto ROT, czyli ile wpisów będzie naprawdę na tablicy.
- Czy `CreateAndOpenMissionController(loc, null, character)` sam zaczyna rozmowę po wejściu do sceny, czy tylko stawia gracza przy osobie. Wiem tylko, że tak woła to gra z paska portretów.
- Czy "Talk now" z notablem w mieście pokazuje poprawnie każde zadanie, bo część kwestii może zależeć od miejsca. Gra oferuje tę opcję w pasku portretów, więc prawdopodobnie tak.
- Nazwy w 1.4.8:
  - kolekcji band bandyckich (`MobileParty.AllBanditParties`?);
  - właściwości "na morzu" z NavalDLC;
  - zdarzenia końca rozmowy (sygnatura);
  - `GetTotalAlternativeSolutionDurationInDays`.
- Czy `MoneyLedger` i kontrolka Szczelność (164a) liczą `ApplyForSettlementToCharacter` jako przelew z płatnikiem bez dopisku.
- Czy w `TownDebt` jest publiczny odczyt "rada ma dług" (widzę tylko `ArrearsLike`, `TermsLike`).
- Czy funkcja nazwy bandy w `Rumours.cs` jest dostępna z zewnątrz.
- Jaka jest reguła rezerwy kiesy wsi w Armoury (dotyczy E2b).
- Czy AIInfluence, Diplomacy, BetterEconomy, StrategicCampaignAI145 albo BKROTPatch zmieniają rozmowy lub zadania (z modów sprawdzone tylko ROT.dll i BannerKings.dll, w rozpoznaniu).
- Jak `ROT_Hideouts` i `ROTBanditLeaveHideouts` wpływają na liczbę band poza kryjówkami.
- Gotowy mod tablicy dla ROT. Nexus zwracał 403, przegląd był niepełny; z cudzych modów bierzemy tylko pomysły.