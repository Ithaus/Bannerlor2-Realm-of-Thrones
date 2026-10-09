# PACZKA 171 - ZBROJENIE ZALOG I KOMPLETY REKRUTOW (specyfikacja dla wykonawcy)

Status: SAM PROJEKT PO KRYTYCE (08.10.2026, noc; odpowiedzi na 17 uwag - rozdz. 17). Nic nie zbudowane, nic nie wgrane, nic nie zacommitowane - commit robi wykonawca.
Wykonanie (08.10.2026): zaimplementowane w tym drzewie razem z ta specyfikacja (build kod 0, NIEWGRANE - DO SPRAWDZENIA w autotescie); odchylenia wykonawcy - wpis 171 w CHANGELOG.md, akapit "Odchylenia od specyfikacji".
Drzewo robocze (jedyne miejsce zmian): `SCR3\dzien-6\zaloga171\repo`, galaz `w-toku/171-zbrojenie-zalog` od `2e235ea` (= wersja w grze, Armoury c01a54ba).
SCR3 = `C:\Users\GAME\AppData\Local\Temp\claude\C--Program-Files--x86--Steam-steamapps-common-Mount---Blade-II-Bannerlord\3cf3e0ac-5529-4b68-a794-0edec69cfda7\scratchpad`.
Build: `cd "<drzewo>/Armoury" && dotnet build Armoury.csproj -c Release -v q --nologo "-p:GameLibs=C:/Users/GAME/Bannerlor2-Realm-of-Thrones/libs" > build.log 2>&1; echo rc=$?`
(rc=0 = OK; nigdy przez `| grep | head`). Po zmianie `Settings.cs`: `python tools/gen_mcm.py` w drzewie ("python3" to atrapa Sklepu).

Podstawa (przeczytane w calosci): `docs/rozpoznanie-2026-10-08/paczka-171-wynik-1.md` (przyczyny "bez zapisu", kazde miejsce zmieniajace pule),
`-wynik-2.md` (nasz system sprzetu, regula "z tym, co ma", liczby z logow), `-wynik-3.md` (zaopatrzenie zamkow, wozy, szkolenie) - w repo Jeffa.
Rzeczy niepewne sprawdzilem sam w dekompilacji i w logach; oznaczenia: [K] przeczytane w kodzie, [P] pomiar z logu, [S] szacunek, [H] historia.

Decyzje Jeffa (wiazace):
- 08.10 ok. 12:15: "jak znika to zamykamy, ma byc logiczny system ekonomii, ze wszystko z czegos wynika".
- 08.10 ok. 12:30 "tak potwierdzam": (1) zaloga zamku kupuje bron w najblizszym przyjaznym miescie - pan zamku placi kasie miasta, towar jedzie do zamku;
  (2) rekrut bez kompletu przychodzi z tym, co ma (gorszy sprzet albo bez broni) zamiast kompletu wzorca z niczego - dozbraja go pan, kupujac na targu;
  (3) szkolenie zalogi = codzienne cwiczenia wlasna bronia (oboz BetterEconomy z XP z niczego zamyka paczka 170).
- Parametry ekonomii dobiera projekt (bez pytan o liczby); mechanika obejmuje 100% przypadkow; pytania tylko o zmiany rozgrywki gracza (rozdz. 15).
- Wpis 92 (05.10, "tak"): "Awans X -> Y w puli notabla: komplet Y = komplet X (albo dobytek X, ktory Y tez nosi) + kupione na targu"; tier 1 przychodzi
  z wlasnym dobytkiem. Paczka 160: kon jest wlasnoscia zolnierza; kon najemnika z wzorca "do kroku weterani" (167).

Scalanie: po kolei 169 -> 170 -> 171. Dlatego: wszystko nowe w NOWYCH plikach; w `Settings.cs`, `McmSettings.cs` (generowany), `CHANGELOG.md`,
`ArmouryBehavior.cs`, `SubModuleMain.cs` - zwarte bloki opisane nizej; w pozostalych plikach male, nazwane wstawki.

---

## 0. Dla Jeffa - prostym jezykiem

1. **Najwieksza dziura, nowa (sprawdzona w kodzie DTE i w 4 logach):** Dynamic Troop Equipment NIE zapisuje zbrojowni garnizonow. Po kazdym wczytaniu
   zapisu wszystkie zalogi stoja z pusta zbrojownia i panowie kupuja im wszystko od nowa: w ciaglym roku testu zalogi wydawaly 60-130 tys. zl dziennie,
   po wczytaniu zapisu z doby 360 - **490-870 tys. zl dziennie przez co najmniej 12 dob** (ok. 10 tys. sztuk dziennie, razem ok. 7.8 mln zl; w tym
   czasie kapital Banku spadl z 700 do 70 tys., a dlug rodow wzrosl z 6.77 do 7.53 mln). Kazde Twoje wczytanie to miliony zlota panow wyrzucone na powtorne
   zbrojenie zalog - jedna z przyczyn, dla ktorych rody biednieja. 171 zapisuje zbrojownie zalog sama. Twoj obecny zapis tych danych jeszcze nie ma: przy
   pierwszym wczytaniu po wgraniu 171 zbrojownie zalog AI zostana raz odtworzone (jak dorobek stuleci na starcie), zamiast puszczac panow na zakupy (C9a).
2. **Rekruci AI z kompletem z niczego - zamkniete.** ROT przy kazdym werbunku lorda zamienia rekruta na czlowieka rodu i oglasza "werbunek" drugi raz;
   ten sam czlowiek dostawal drugi, pelny komplet z niczego (na starcie ok. 2 700 kompletow dziennie, po roku ok. 600). Teraz: ten sam czlowiek, jeden komplet.
   Najemnik, ochotnik bez zapisu i ochotnik z mapy przychodza z tym, co maja w domu (dobytek, ktory pasuje do ich sluzby); jeniec - z niczym. Reszte kupuje pan.
3. **Komplet idzie z czlowiekiem:** gdy ROT/nasz "zaciag rodowy" zmienia ochotnika w czlowieka rodu, gdy zaloga bierze ochotnika z puli, gdy w zamku BK
   ochotnik awansuje, gdy lord zostawia ludzi w zalodze albo ich z niej zabiera, gdy rozwiazana partia wchodzi do zalogi - jego rzeczy ida z nim, a nie sa
   sprzedawane, kupowane drugi raz albo tworzone od nowa. Zalogi sprzedaja raz w tygodniu to, czego maja za duzo (po awansach, zamianach ROT, lupie).
4. **Zamek kupuje w miescie:** zaloga zamku zamawia w miescie, z ktorym handluja wsie zamku; pan placi miastu, towar schodzi z polki i jedzie wozem
   (1-4 dni drogi). Zamek przestaje byc targiem broni (kupcy juz nie przerzucaja tam broni natychmiast, bez drogi).
5. **Cwiczenia wlasna bronia:** zaloga i druzyna lorda AI cwicza tyle, ile maja broni - bez broni nie ma musztry, wiec awanse zwalniaja, gdy brakuje sprzetu.
   Po recenzji kodu: liczy sie bron na szczebel czlowieka (wlocznia rekruta nie "uzbraja" weterana), a wolniej cwiczy ten oddzial, ktoremu brakuje, nie cala druzyna.
6. **Uczciwy bilans (rozdz. 7):** dzis polki miast po roku maja ponad 1 mln sztuk broni, a kowale robia 500-700 sztuk dziennie - ten stos to sprzet z niczego.
   Po 171 w NOWEJ kampanii przez pierwszy miesiac lub dwa armie AI beda gorzej uzbrojone niz ich wzorzec (to jest prawda o swiecie, w ktorym nikt nie dosypuje),
   ale nie "stana": bitwy AI z AI liczy gra bez patrzenia w zbrojownie, a w Twoich bitwach brakujacy zolnierz dostaje sprzet chlopa swojej kultury
   na czas bitwy i kare morale. W Twojej trwajacej kampanii zapas na polkach starczy na lata. Po recenzji kodu nowa kampania zaczyna sie z zapasem
   2 miesiecy pracy rzemieslnikow na polkach (dotad 2 tygodnie), bo 171 zabiera drugi komplet z niczego, ktory dotad zapychal te luke.
7. **Strzaly i belty:** zaden warsztat ich dzis nie robi (0 sztuk dziennie we wszystkich logach), a 171 sprawia, ze lucznicy AI kupuja je z polek. W Twojej
   kampanii polki maja ok. 2 900 strzal i 820 beltow i ubywa ich ok. 60 dziennie - to sprawa paczki produkcji (rozdz. 16), pytanie 9.

---

## 1. Co znaleziono - liczby

### 1.1 Zbrojownie zalog gina przy kazdym wczytaniu zapisu (NOWE) [K][P]
- DTE `EveryoneCampaignBehavior.CreateSerializedPartyArmories` (dekompilacja `fa2fd7a6...\scratchpad\dte\DynamicTroopEquipmentReupload\EveryoneCampaignBehavior.cs:975-1000`)
  zapisuje tylko partie, dla ktorych `ShouldPersistParty` (`:1049-1075`) = true: wodz-bohater (nie gracz), wlasciciel-bohater, `TotalHeroes > 0`.
  **Garnizon nie ma wodza - jego zbrojownia nigdy nie trafia do zapisu.**
- Po wczytaniu `OnGameLoaded` (`:156-190`) wola `OnMobilePartyCreated` dla partii bez zbrojowni; dla AI po starcie gry blokuje to nasza
  `LevyGold.PartyCreatedPrefix` (LevyGold.cs:92-105) - log doby wczytania: "nowe partie AI bez darmowego kompletu DTE: 599" (zwykle 100-300).
  Zalogi zostaja z pusta zbrojownia, a `AiGear.TryBuy` kupuje im wszystko od nowa (budzet 25% kiesy pana ponad 2 000 na kazda zaloge co dobe).
- Nasza latka wpisu 89 (`AiGear.KeepGarrisonArmory`) chroni zbrojownie garnizonu tylko przed sprzataniem DTE w trakcie gry, nie przed zapisem.
- Dowod [P] (ZakupyAI, garnizony):

| Log | Doby | Zakupy garnizonow | Zloto garnizonow na dobe | Szt. wszystkich zakupow AI |
|---|---|---|---|---|
| 08-18-38 (rok ciagly T3) | 109190-109199 | 62-78 | 61 774 - 133 936 | 1 359 - 2 138 |
| 10-54-00 (wczytany zapis doby 360) | 109196-109199 | 150-165 | 742 498 - 867 915 | 9 636 - 10 673 |
| 11-22-18 (wczytany) | 109196-109197 | 149-169 | 830 786 - 834 634 | 9 537 - 10 500 |
| 11-32-39 (wczytany, 161) | 109196-109197 | 148-168 | 820 362 - 834 839 | 9 659 - 10 525 |
| 11-35-24 (wczytany, doba dalej) | 109198 | 143 | 611 464 | 9 462 |

  "Przeplywy osad (kasy zamkow)": zakupy sprzetu AI w zamkach w roku ciaglym +28.7-29.3 tys./dobe, po wczytaniu +342 tys./dobe.
  Raport 3 tlumaczyl skok "x15" hipoteza (awanse z XP, cena zamku) - przyczyna jest prostsza: pusta zbrojownia po wczytaniu.

### 1.2 Rekruci AI bez zapisu - przyczyny (z raportow 1-2, sprawdzone) [K][P]
| Przyczyna | Gdzie | Ile na dobe (start / po roku) |
|---|---|---|
| ROT oglasza werbunek drugi raz (ten sam czlowiek, oddzial Y, zrodlo i osada null) | `ROTTroopRecruiter.ExchangeClanTroops` (rot\ROT.CampaignBehaviors\ROTTroopRecruiter.cs:128, zamiana :201-203, echo :213-217, `_firingEvent` :23, wywolanie z `fireEvent:true` tylko w `OnTroopRecruited` :105-126) | ok. 2 700 / ok. 600 duplikatow; komplet wzorca Y z niczego + licznik "bez zapisu" |
| HouseLevies zamienia szlacheckiego X na czlowieka rodu Y, komplet zostaje przy X (sierota) | `HouseLevies.Convert` :74 | 952, 638, 462, 352 zamian (pierwsze doby) |
| Swiezy ochotnik szlachecki BK tieru 2 (EliteBasicTroop, level 11) - VolunteerKit traktuje go jak tier 1 | `VolunteerKit.Postfix` :79 | ok. 270 / ok. 40 rekrutow bez zapisu |
| Pule zamkow BK - poza VolunteerKit (awanse za darmo, bez zapisu) | `BKNotableBehavior.UpdateVolunteers` (bk\BannerKings.Behaviours\BKNotableBehavior.cs:194-284) | brak licznika |
| Autowerbunek garnizonu bez zdarzenia - komplet z zapisu zostaje sierota, zaloga dostaje golego | `GarrisonRecruitmentCampaignBehavior.TickAutoRecruitmentGarrisonChange` (cs\...\GarrisonRecruitmentCampaignBehavior.cs:76-95) | uzgodnienie 1. doby: "nadmiar wobec puli 1257" |
| Najemnik z karczmy (zrodlo null, osada = miasto) | `RecruitmentCampaignBehavior.ApplyInternal` (MercenaryFromTavern) | ok. 375 / ok. 45 kompletow wzorca |
| Jeniec (zrodlo i osada null) | `RecruitPrisonersCampaignBehavior.RecruitPrisonersAi` :111-118 | 0-100 / ok. 70 kompletow wzorca |

### 1.3 Ochotnik z mapy (VolunteerFromMap) [K]
`RecruitmentCampaignBehavior.GetRecruitVolunteerFromMap` (:646-649) - zrodlo i osada null, oddzial = `BasicTroop` klanu (rody pomniejsze; bywa tier 2+). Zaden raport go
nie liczyl osobno - dzis wpada do "bez osady" razem z jencami i duplikatami.

### 1.4 Co DTE robi, gdy w zbrojowni brakuje sztuk [K] (wazne dla bilansu, rozdz. 7)
- **Bitwy AI z AI (symulacja) nie patrza w zbrojownie.** W DTE nie ma latki na symulacje bitwy (grep "Simulat" w dekompilacji: tylko podzial lupu gracza).
  Sila w symulacji to wzorzec oddzialu - brak sprzetu NIE oslabia armii AI w wojnach AI.
- W bitwie z udzialem gracza: `PartyEquipmentDistributor.ApplyEmergencyLoadout` (:1805-1880) - pusty slot zbroi i broni dostaje na czas bitwy sztuke
  z `BasicTroop` kultury (oznaczona jako tymczasowa, NIE zdejmowana ze zbrojowni), `MarkUnderEquippedAssignmentsIfEnabled` (:1906-1935) - kara morale
  2 za kazdy brakujacy tier, najwyzej 20. Domyslnie wlaczone (`ModSettings.EnableEmergencyLoadout`, `Underequipped` = true, :54-58).
- Awans AI nie wymaga sztuk (DTE `GetUpgradeRequiresItemFromCategoryPatch`; my przywracamy tylko konia - `Stables`).
- **Lup z przegranych to przeniesienie, nie dubel** (sprawdzone po krytyce, rozdz. 17 pkt 2): DTE `DistributeLootRandomly`/`GetAllLootItems`
  (EveryoneCampaignBehavior.cs:793-850) kopiuje zwyciezcom cala zbrojownie przegranych w `OnMapEventEnded` (gra wola je w MapEvent.cs:2079), ale chwile
  pozniej (MapEvent.cs:2157 `HandleMapEventEnd`) gra niszczy kazda pusta partie przegranych (MapEventSide.cs:437-444), a DTE `OnMobilePartyDestroyed`
  (:510-520) kasuje jej zbrojownie. Partia przegranych jest zawsze pusta: `CaptureDefeatedPartyMembers` (MapEvent.cs:1955-2032) zdejmuje z jej rosteru KAZDEGO
  zwyklego czlowieka bezwarunkowo (`AddToCountsAtIndex(-Number, -WoundedNumber)`; ranni do niewoli, zdrowi z szansa 25%, reszta rozproszona), bohaterow bierze
  do niewoli albo robi zbiegami. Dotyczy to takze zalogi po przegranym wypadzie i przegranym szturmie. Przy ucieczce (`EndedByRetreat`) DTE lupu nie dzieli,
  a gra nikogo nie zdejmuje. Wniosek: C9 nie utrwala zadnego dubla; latki na lup nie trzeba.

---

## 2. Zasady (jedna zasada na jedno zjawisko)

| Nr | Zasada | Dlaczego |
|---|---|---|
| Z1 | Ten sam czlowiek ma jeden komplet. Zamiana oznaki (ROT, HouseLevies) nie tworzy rzeczy. | Zamiana Blackwooda w czlowieka Blackwoodow to zmiana barw, nie nowy ekwipunek. |
| Z2 | Komplet idzie z czlowiekiem: pula -> pula (zamiana), pula -> partia (werbunek), pula -> zaloga (autowerbunek), partia <-> zaloga (lord zostawia albo zabiera ludzi, rozwiazana partia wchodzi do zalogi - A7). | Rzeczy kupione przez notabla albo pana naleza do tego czlowieka - nie znikaja i nie dubluja sie; pan nie placi drugi raz za to, co jego czlowiek juz ma. |
| Z3 | Wolny czlowiek bez zapisu przychodzi z dobytkiem: rzeczy przodka tieru 1 swojej linii, ktore jego oddzial tez nosi (regula wpisu 92). | To ta sama, juz przyjeta regula co przy awansie bez zapisu; kupna nikt nie zapisal, wiec go nie bylo. |
| Z4 | Jeniec przychodzi z niczym. | Jego sprzet wzial zwyciezca w lupie (DTE), a obszukany jeniec ma lachmany (CaptiveRags). |
| Z5 | Dobytek (to, co czlowiek ma z domu) idzie z nim; notabl sprzedaje na targu tylko to, co sam kupil. | Rzeczy z domu nie sa towarem kupca - dzis przy odejsciu ochotnika trafialy na targ jako sztuki z niczego. |
| Z6 | Zamek nie jest targiem broni. Zaloga zamku kupuje w miescie, towar jedzie droga, placi pan. | Zamek nie ma kupcow (gra: lup sprzedaje sie tylko w miastach; ColdStart zaopatruje tylko miasta); przenoszenie bez drogi bylo teleportem. |
| Z7 | Zbrojownia zalogi jest majatkiem zamku/miasta - przetrwa zapis gry. | Bron nie wyparowuje z magazynu, bo ktos zapisal i wczytal gre. |
| Z8 | Szkolenie = cwiczenia wlasna bronia: doswiadczenie dnia razy udzial ludzi, ktorzy maja bron swojego rodzaju w zbrojowni. | Decyzja Jeffa (3); musztra bez broni nie uczy walki. |
| Z9 | Nadwyzka zbrojowni zalogi idzie na targ po cenie skupu, zloto do pana (ta sama regula co nadwyzki partii lorda - MenPurse). | Bez ujscia nadwyzki zalog (awanse, zamiany ROT, lup z obrony) rosna bez konca, a po C9 trwaja w zapisie i zabieraja sprzet z rynku. |

---

## 3. CZESC A - przyczyny braku kompletow i naprawa kazdej

### A1. Duplikat ROT - ten sam czlowiek, bez drugiego kompletu
**Decyzja:** zdarzenie werbunku ogloszone przez ROT z wnetrza `ExchangeClanTroops(..., fireEvent: true)` to echo pierwszego werbunku - DTE nie dostaje go wcale,
RecruitKit liczy je tylko jako "duplikat ROT", PeopleLedger nie liczy go jako nowego czlowieka.
**Dlaczego:** ROT tylko zamienia X na Y w tej samej druzynie (`roster.RemoveTroop(X)` + `AddToCounts(Y)`, :201-203); komplet X dostal juz w zdarzeniu pierwszym.

Wykonanie (NOWY plik `Armoury/src/RecruitSources.cs`, `internal static class RecruitSources`):
- Licznik glebokosci, nie pole prywatne ROT: `private static int _rotDepth;`.
- Latka Harmony na `AccessTools.TypeByName("ROT.CampaignBehaviors.ROTTroopRecruiter")`, metoda `"ExchangeClanTroops"` (prywatna; parametry
  `Hero owner, TroopRoster roster, CharacterObject troop, int count, bool fireEvent, Settlement settlement`):
  ```
  public static void RotExchangePrefix(bool fireEvent, out bool __state) { __state = fireEvent; if (fireEvent) _rotDepth++; }
  public static Exception RotExchangeFinalizer(Exception __exception, bool __state) { if (__state && _rotDepth > 0) _rotDepth--; return __exception; }
  ```
  Finalizer (nie postfiks) - licznik wraca nawet po wyjatku w cudzym sluchaczu (ROT ustawia swoje `_firingEvent` bez try/finally - na nim nie polegamy).
  Wzorzec `out __state` + finalizer z `__state` juz dziala w repo (`RecruitCost.WherePrefix` / `WhereFinalizer`).
- `internal static bool IsRotEcho(Settlement settlement, Hero source)` =
  `Settings.Current != null && Settings.Current.RotSwapSameMan && _rotDepth > 0 && settlement == null && source == null`.
- Wpiecie: `RecruitSources.ApplyAll(Harmony h)` (zapamietuje `h` w polu statycznym, jak `MountedWage`), wolane w `SubModuleMain` zaraz po `RecruitCost.ApplyAll(_harmony);`.
  Gdy typu ROT jeszcze nie ma (kolejnosc ladowania) - `ApplyLate()` z `ArmouryBehavior.OnSessionLaunched` probuje raz jeszcze.
  **Flagi wpiecia na caly proces** (krytyka 11): `private static Harmony _h; private static bool _applied, _rotHooked, _prisonHooked, _garrisonAutoHooked;`
  - NIE czyszczone w `Reset()` (Reset jest w konstruktorze ArmouryBehavior, czyli raz na kampanie, a latki Harmony zyja caly proces gry).
  `ApplyAll`: `if (_applied) return; _applied = true;` potem kazda latka tylko gdy jej flaga = false, flaga = true po udanym `Patch`.
  `ApplyLate()`: `if (_h == null || _rotHooked) return;` i ponawia WYLACZNIE latke ROT. **Dlaczego:** podwojnie wpiety postfiks A5 wkladalby kazdy komplet
  autowerbunku dwa razy (sprzet z niczego). Ta sama zasada w `GarrisonArmory.ApplyAll` i `ArmsDrill.ApplyAll` (zbior wpietych typow modeli - statyczny,
  jak `MountedWage._totalHooked`). Log startu:
  `RecruitSources: echo werbunku ROT (ten sam czlowiek) wpiete|BRAK ROTTroopRecruiter.ExchangeClanTroops; jency wpiete|BRAK; autowerbunek zalog wpiety|BRAK.`
- `AiGear.RecruitKitPrefix` - nowa sygnatura (nazwy parametrow jak w DTE `OnTroopRecruited(Hero recruiterHero, Settlement recruitmentSettlement, Hero recruitmentSource, CharacterObject troop, int amount)`):
  ```
  public static bool RecruitKitPrefix(Hero recruiterHero, Settlement recruitmentSettlement, Hero recruitmentSource, CharacterObject troop, int amount)
  {
      var s = Settings.Current;
      if (s == null || recruiterHero == null) return true;
      if (recruiterHero == Hero.MainHero) { /* bez zmian: RecruitKit.OnRecruited(...) tylko zdejmuje zapisy */ return true; }
      try { if (RecruitSources.IsRotEcho(recruitmentSettlement, recruitmentSource)) { RecruitKit.NoteEcho(troop, amount); return false; } }
      catch (Exception e) { RecruitSources.Stumble("echo", e); }
      if (!s.AiRecruitsBringKit) return false;
      try { if (RecruitKit.On) return RecruitKit.OnRecruited(recruiterHero, recruitmentSettlement, recruitmentSource, troop, amount); } catch (Exception e) { Log.Error("RecruitKit", e); }
      return true;
  }
  ```
- `PeopleLedger.OnTroopRecruited` (PeopleLedger.cs:134): pierwsza linia w `try`: `if (RecruitSources.IsRotEcho(settlement, source)) { _rRotEcho += amount; return; }`;
  `_rRotEcho` zerowany razem z `_rNoPlace`; w linii "Ludzie:" po "gracz N" dopisac `, duplikaty ROT (ten sam czlowiek, nie liczeni) M`.
  Bilans partii rodow ("zwerbowani przez wodzow") przestaje liczyc echo podwojnie - to poprawka.
- Gracz: ROT go nie dotyka (`IsHeroManageable` = false dla gracza i jego rodu, :523-542) - echo dla gracza nie wystepuje.
- Cudzy kod, nie ruszamy: gra przy echu daje drugi raz doswiadczenie Leadership lorda (`RecruitmentCampaignBehavior.OnTroopRecruited` :178-189); ROT przy zmianie
  tieru oddaje/pobiera roznice ceny werbunku `GiveGoldAction.ApplyBetweenCharacters(null, owner, ...)` (:205-212) - zloto z/w nicosc -> pozycja dla 164 (rozdz. 16).

### A2. HouseLevies - komplet przechodzi z X na Y
**Decyzja:** przed zamiana slotu zapis kompletu X przechodzi na Y (ten sam czlowiek, te same rzeczy).
**Dlaczego:** Z1/Z2; dzis zapis X zostawal sierota (Reconcile go sprzedawal), a Y przy werbunku dostawal wzorzec z niczego.

Wykonanie:
- `HouseLevies.Convert` (HouseLevies.cs:73-74): przed `slots[i] = repl;` dodac
  `try { RecruitKit.OnSwap(notable, troop, repl); } catch (Exception e) { RecruitKit.Stumble("OnSwap", e); }`.
- `RecruitKit.OnSwap(Hero n, CharacterObject x, CharacterObject y)`: gdy `!On || !Settings.Current.HouseLeviesKeepKit` - return.
  `var k = Pop(n, x); if (k == null) { _dSwapNoKit++; return; }`
  `Push(n, new Kit { Troop = y, Template = false, Own = k.Own + (k.Template ? TemplateItems(k.Troop) : brak), Items = k.Items });  _dSwap++;`
  (wzorzec ColdStart X staje sie dobytkiem: to rzeczy tego czlowieka, nie wzorzec Y).

### A3. Swiezy ochotnik tieru 2+ - znany komplet tam, gdzie powstaje
**Decyzja:** nowy ochotnik tieru 2+ (szlachcic BK, kaplan druidow) traktowany jak awans z `BasicTroop` swojej kultury: notabl kupuje na targu kluczowe czesci
(ta sama `VolunteerKit.Buy`, co przy awansie). Gdy towaru albo zlota brak - ochotnik ZOSTAJE (wariant lagodny) z tym, co ma (Z3), bez zakupu.
**Dlaczego:** "wyzszy tier = ktos kupil sprzet" (Jeff 04.10, VolunteerKit); wariant lagodny nie zmienia skladu pul, z ktorych werbuje gracz (wariant scisly - pytanie 2).

Wykonanie (`VolunteerKit.Postfix`, VolunteerKit.cs:75-83):
```
var x = gone.FirstOrDefault(g => g.UpgradeTargets != null && g.UpgradeTargets.Contains(y));
if (x == null)
{
    if (y.Tier >= 2 && RecruitKit.FreshOn)            // FreshOn = RecruitKit.On && Settings.Current.FreshVolunteerKit
    {
        var basic = BasicOf(y, n);                     // y.Culture?.BasicTroop ?? n.Culture?.BasicTroop
        bool ok = basic != null && basic != y && Buy(n, market, basic, y, false);   // false = nie licz w "powodach cofniec"
        RecruitKit.OnFresh(n, y, ok ? new List<EquipmentElement>(_lastBought) : null);
        if (ok) _freshBought++; else _freshOwn++;
    }
    continue;                                          // tier 1 - wlasny dobytek (bez zmian)
}
```
- **Tylko w trwajacej grze** (krytyka 5): na samym poczatku `VolunteerKit.Postfix`, zaraz po `if (__state == null) return;`:
  `if (Campaign.Current == null || !Campaign.Current.GameStarted) return;` - przy tworzeniu kampanii VolunteerKit nic nie kupuje, nie cofa i nie zapisuje
  (dotyczy obu latek: gry i BK z A4). **Dlaczego:** gra przy tworzeniu kampanii napelnia pule raz dla kazdej osady
  (`RecruitmentCampaignBehavior.OnNewGameCreatedPartialFollowUpEnd`, RecruitmentCampaignBehavior.cs:166-176), PRZED `OnSessionLaunched`, czyli przed
  `ColdStart.Markets` (Campaign.cs:1694-1695; `GameStarted` = true dopiero w pierwszym `RealTick`, Campaign.cs:894-896; przy wczytaniu zapisu od razu,
  :1355-1357). A3 kupilby wtedy czesci kluczowe ok. 1 520 swiezym szlachcicom (log 11-40-05: "ochotnicy tieru 2+ w pulach 1520") z ok. 8 tys. sztuk
  sprzetu na polkach gry (ok. 3 tys. zbroi i broni), zanim ColdStart je napelni - a tych ochotnikow i tak obejmuje dorobek stuleci (B5).
- `Buy(..., bool countWhy = true)`: nowy ostatni parametr; przy `false` nie wola `Why(...)` (zamowienie `NoteUnmetOnce` zostaje - popyt jest prawdziwy).
  `Buy` przy `false` wyniku nic nie kupuje (sprawdzone: wychodzi przed zaplata, :199 i :204).
- `RecruitKit.OnFresh(Hero n, CharacterObject y, List<EquipmentElement> bought)`:
  `Push(n, new Kit { Troop = y, Own = OwnOf(y), Items = bought ?? new List<EquipmentElement>() })`; liczniki `_dFresh++`, `_dFreshPcs += ...`.
- Licznik `_freshBought/_freshOwn` w linii "Pule ochotnikow (171)" (rozdz. 8). Zakup swiezego liczy sie w "Ochotnicy:" jako zakup (szt., zloto), nie jako awans.

### A4. Pule zamkow BK - ta sama latka VolunteerKit
**Decyzja:** awans i nowy ochotnik u notabla zamku idzie przez VolunteerKit (zakup na targu, zapis kompletu, A3) tak samo jak w miescie i wsi.
**Dlaczego:** jedna regula dla wszystkich pul; dzis zamki BK awansowaly za darmo i bez zapisu.

Wykonanie (`VolunteerKit.ApplyAll`): druga latka tej samej pary `Prefix`/`Postfix` na
`AccessTools.Method(AccessTools.TypeByName("BannerKings.Behaviours.BKNotableBehavior"), "UpdateVolunteers", new[] { typeof(Settlement) })`
(prywatna, metoda instancji, parametr `settlement` - nazwa zgodna z naszym `Prefix(Settlement settlement, ...)`, sprawdzone bk\...\BKNotableBehavior.cs:194).
- `Prefix`: na poczatku `if (settlement != null && settlement.IsCastle && !Settings.Current.VolunteerKitCastles) { __state = null; return; }`
  (gra wola swoja metode tylko dla miast i wsi, :217 - wiec `IsCastle` = droga BK).
- Liczniki zamkow osobno (`_castleBought`, `_castleReverted`, `_castleFresh` - przy `settlement.IsCastle`), w linii "Ochotnicy:" dopisek
  `; w tym zamki BK: awanse X, cofniete Y, swiezi t2+ Z`.
- Targ notabla zamku (poprawione po krytyce 12): w `VolunteerKit.Postfix` dla `settlement.IsCastle`:
  `var market = settlement.IsCastle ? (ArmyClothing.MarketTown(settlement) ?? MarketOf(settlement)) : MarketOf(settlement);`
  (`ArmyClothing.MarketTown` i tak staje sie `internal` w C1). Ten sam `market` idzie do `Buy`, `OnVanished` i A3 - notabl sprzedaje rzeczy tam, gdzie je kupil.
  **Dlaczego:** `MarketOf` liczy linie prosta bez wojny i zapamietuje wynik na stale (VolunteerKit.cs:93-114) - notabl zamku kupowalby w miescie wroga albo
  za morzem; `MarketTown` = miasto handlowe wsi zamku (droga, wlasna frakcja, przeliczane przez gre), z wylaczeniem miast wroga - to samo miasto, co w C1.
  `MarketOf` zostaje bez zmian dla miast i wsi oraz dla wyceny konia rekruta (`Stables.MarketPrice`, 143) - to poza zakresem 171.
- Log ApplyAll: `VolunteerKit: ... wpiety; zamki BK (UpdateVolunteers) wpiete|BRAK.`

### A5. Autowerbunek garnizonu - komplet do zbrojowni zalogi zamiast sieroty
**Decyzja:** czlowiek wziety z puli do zalogi przynosi do zbrojowni zalogi swoj komplet z zapisu; bez zapisu - tier 1 swoj dobytek (jak przy werbunku do partii),
tier 2+ - "z tym, co ma" (Z3).
**Dlaczego:** Z2; dzis zaloga dostawala golego czlowieka, a jego komplet notabl sprzedawal (sierota).

Wykonanie (`RecruitSources.cs`):
- Prefiks + postfiks na `AccessTools.Method(typeof(GarrisonRecruitmentCampaignBehavior), "TickAutoRecruitmentGarrisonChange", new[] { typeof(Town) })`
  (obok istniejacej latki `RecruitCost.GarrisonWherePrefix` - nie koliduja: tamta tylko ustawia miejsce werbunku).
  ```
  public static void GarrisonAutoPrefix(Town __0, out Dictionary<Hero, CharacterObject[]> __state)
  // gdy !RecruitKit.On || !s.GarrisonRecruitKeepsKit || __0 == null: __state = null
  // migawka VolunteerTypes (Clone) notabli: __0.Settlement.Notables + kazdej wsi z __0.Settlement.BoundVillages (v.Settlement.Notables)
  //   (ta sama lista, z ktorej gra buduje _volunteerListCache, GarrisonRecruitmentCampaignBehavior.cs:125-165)
  public static void GarrisonAutoPostfix(Town __0, Dictionary<Hero, CharacterObject[]> __state)
  // dla kazdego notabla i indeksu i: before[i] != null && after[i] == null -> wziety X = before[i]
  //   gra zeruje slot dokladnie przy przejsciu do zalogi (:92), wiec roznica = wzieci
  ```
- `RecruitKit.OnGarrisonTook(Hero n, CharacterObject x, MobileParty garrison)`:
  - wlasciciel zamku/miasta = rod gracza i `!GarrisonBuysGearPlayer` -> `OnVanished(n, x, MarketOfNotable(n))` (jak dzis, tylko od razu; `_dGarPlayer++`);
  - `x.Tier <= 1` -> `TemplateItems(x)` do zbrojowni (dobytek tieru 1 - ta sama regula co przy werbunku do partii, gdzie DTE daje komplet tieru 1);
  - tier 2+: `var k = Pop(n, x); items = k != null ? Materialize(k) : OwnOf(x);` (licznik: z zapisu / z tym co ma);
  - kazda sztuka `AiGear.AddToArmory(garrison, e.Item, 1)` (bez `AiWear.NoteBought` - zalog nie sledzi AiWear, jak dzis w TryBuy).
- `garrison` = `__0.GarrisonParty` w postfiksie (gra tworzy go w metodzie, gdy go nie bylo, :82-85).
- **Tier 1 do zalogi - swiadomie przyjete (krytyka 6):** to NOWY doplyw sztuk (dzis autowerbunek przychodzi nagi, GarrisonRecruitmentCampaignBehavior.cs:90,
  tylko `AddToCounts`). Przyjmujemy go, bo to ta sama, juz przyjeta regula wpisu 92 ("tier 1 przychodzi z wlasnym dobytkiem"), ktora dziala przy werbunku do
  partii - jedna regula dla jednego zjawiska, niezaleznie od tego, czy chlop idzie do druzyny lorda, czy do zalogi. Skala: gra pozwala na najwyzej 1 autowerbunek
  na twierdze na dobe (`DefaultSettlementGarrisonModel.GetMaximumDailyAutoRecruitmentCount` = 1; `ROTSettlementGarrisonModel` :77-79 przekazuje dalej;
  `BKGarrisonModel` tego nie nadpisuje), czyli najwyzej 227 ludzi i ok. 1.0 tys. szt. na dobe, realnie [S] 0.2-0.7 tys. (zalogi w T3 rosly z 50 do 78 tys.
  ludzi w 150 dob, razem z darmowym przyrostem podstawowym i ludzmi zostawianymi przez lordow). Pozycja w bilansie 7.2, liczba w linii 3 i w tescie 13A.
  Mniej zakupow pana na te sztuki (tier 1 i tak by kupil) - pan placi mniej, rynek traci ten popyt.

### A6. Sieroty, ktore zostaja (bez zmian, sprzata Reconcile raz na dobe)
CrashScribe `Mends.LocalLevies` (czysci ochotnikow obcej kultury), ROT `ChangeNotableCulture` (Others), ekran werbunku gracza BK (`RecruitmentOnDonePatch` -
zapis gracza), straze majatku gracza BK (`BKLandsLaborBehavior.TryGuardTick`). To prawdziwe odejscia czlowieka z puli: notabl sprzedaje to, co kupil (Z5),
dobytek odchodzi z czlowiekiem. **Dlaczego bez zmian:** regula "ochotnik odszedl" juz jest i jest poprawna.

### A7. Partia <-> zaloga i rozwiazanie partii - komplet idzie z czlowiekiem (NOWE po krytyce 1 i 3)
**Decyzja:** gdy ludzie przechodza miedzy partia lorda a zaloga (gra: lord zostawia albo zabiera ludzi) albo rozwiazana partia wchodzi do zalogi, ich sprzet
przechodzi z nimi miedzy zbrojowniami DTE. Gdy ludzie rozwiazanej partii odchodza (do milicji wsi albo do domu), biora swoje komplety; zapas ponad komplety
(tabor) sprzedaje sie na targu dla rodu.
**Dlaczego:** Z2. Sprawdzone w kodzie: `GarrisonTroopsCampaignBehavior.LeaveTroopsToGarrison` (:542-581) i `TakeTroopsFromGarrison` (:583-610) przenosza
samych ludzi (`AddToCounts` na rosterach), wolane przy KAZDYM wjezdzie lorda AI (i armii) do twierdzy wlasnej frakcji (`OnSettlementEntered` :247-264).
Dzis zostawieni ludzie przychodza do zalogi nadzy (pan zamku kupuje im komplet drugi raz), a ich sprzet zostaje u lorda jako nadwyzka, ktora MenPurse
sprzedaje po cenie skupu - lord placi dwa razy, rynek traci towar; zabrani zostawiaja komplet w zbrojowni zalogi (nadwyzka bez ujscia - C10).
`DisbandPartyCampaignBehavior.MergeDisbandPartyToFortification` (:309-362) dodaje ludzi do zalogi (`MemberRoster.Add`, :358) i czysci roster, potem
`DestroyPartyAction.ApplyForDisbanding` niszczy partie (DestroyPartyAction.cs:34-42: najpierw zdarzenie `OnPartyDisbanded`, potem `OnMobilePartyDestroyed`),
a DTE kasuje jej zbrojownie (EveryoneCampaignBehavior.cs:510-520) - sprzet ludzi znika, MenPurse ratuje tylko zloto sakiewki (MenPurse.cs:63-81).

Wykonanie (`Armoury/src/GarrisonArmory.cs` - wszystko o zbrojowniach zalog w jednym pliku; latki w `GarrisonArmory.ApplyAll(Harmony)`):
1. **Lord zostawia / zabiera ludzi.** Prefiks + postfiks na `AccessTools.Method(typeof(GarrisonTroopsCampaignBehavior), "LeaveTroopsToGarrison")` i na
   `"TakeTroopsFromGarrison"` (obie prywatne, parametry `MobileParty mobileParty, Settlement settlement, int numberOfTroopsToLeave|numberOfTroopsToTake,
   bool archersAreHighPriority`):
   ```
   public static void LeavePrefix(MobileParty mobileParty, out Dictionary<CharacterObject, int> __state)      // migawka rosteru DAWCY (partia lorda)
   public static void LeavePostfix(MobileParty mobileParty, Settlement settlement, Dictionary<CharacterObject, int> __state)
   //   moved = __state - roster po (tylko dodatnie, bez bohaterow); to = settlement.Town.GarrisonParty (gra tworzy ja w metodzie, :556-559)
   //   MoveKits(mobileParty, to, moved, "leave")
   public static void TakePrefix(Settlement settlement, out Dictionary<CharacterObject, int> __state)        // migawka rosteru DAWCY (zaloga)
   public static void TakePostfix(MobileParty mobileParty, Settlement settlement, Dictionary<CharacterObject, int> __state)
   //   moved = __state - roster zalogi po; MoveKits(settlement.Town.GarrisonParty, mobileParty, moved, "take")
   ```
   Migawka: `Dictionary<CharacterObject, int>` z `GetElementCopyAtIndex` (Number, bez bohaterow) - 10-30 pozycji, tylko gdy gra naprawde przenosi ludzi.
   `__state = null`, gdy `!KitMoves` (`AiGear.On && Settings.Current.KitMovesWithMen`) albo DTE nie znaleziony.
2. **Rozwiazanie partii.** Prefiks + postfiks na `AccessTools.Method(typeof(DisbandPartyCampaignBehavior), "OnPartyDisbanded")` (prywatna, parametry
   `MobileParty disbandParty, Settlement relatedSettlement`) - jedyne wejscie do obu scalen (:290-307), wolane ze zdarzenia przed zniszczeniem partii:
   - prefiks: `toGarrison = relatedSettlement != null && !disbandParty.IsCustomParty && relatedSettlement.IsFortification && !relatedSettlement.IsUnderSiege
     && disbandParty.MemberRoster.TotalManCount > 0 && disbandParty.MapFaction == relatedSettlement.MapFaction` (ten sam warunek co gra, :292-300 i :343-347);
     `__state = toGarrison`. Gdy `!toGarrison` i partia ma zbrojownie: **ludzie odchodza** (milicja wsi :380-384, obca twierdza :361, wies zlupiona, osada null) -
     `SellSurplus(disbandParty, NeedByType(disbandParty), MarketForDisband(relatedSettlement, disbandParty), payee, keepPercent: 0, "rozwiazana")`
     przed scaleniem (roster jeszcze pelny): komplety ludzi zostaja w zbrojowni i znikaja z partia (ludzie odchodza ze swoimi rzeczami), zapas ponad nie idzie
     na targ; `payee = disbandParty.ActualClan?.Leader` (zywy), inaczej bez sprzedazy (tabor odchodzi z ludzmi).
     `MarketForDisband`: miasto -> ono samo; zamek -> `ArmyClothing.MarketTown(zamek)`; wies -> `Village.Bound`, gdy jest miastem, inaczej
     `ArmyClothing.MarketTown(Bound)`; null -> brak sprzedazy. Rynek w wojnie z `disbandParty.MapFaction` -> brak sprzedazy.
   - postfiks: gdy `__state` i `relatedSettlement.Town.GarrisonParty != null` -> `MoveAll(disbandParty, relatedSettlement.Town.GarrisonParty, "rozwiazana")`
     - cala zbrojownia (takze konie i zapas: partia przestaje istniec, jej tabor zostaje w twierdzy; nadwyzke zaloga sprzeda w C10).
   **Dlaczego postfiks, nie prefiks:** zaloga moze powstac dopiero w metodzie gry (`AddGarrisonParty`, :349-352), a zbrojownia DTE partii istnieje do
   `OnMobilePartyDestroyed`, ktore gra wola PO `OnPartyDisbanded`.
3. **Wspolne funkcje** (`internal static`, w `GarrisonArmory`):
   ```
   // sprzet przeniesionych ludzi: najpierw ile sztuk kazdego TYPU (ItemType) im sie nalezy, potem ktore sztuki
   internal static int MoveKits(MobileParty from, MobileParty to, Dictionary<CharacterObject, int> moved, string why)
   //  needMoved[typ] = suma po moved: liczba przedmiotow tego typu w sl. 0-11 wzorca (ch.Equipment; sl. 10-11 = kon i rzad: kon jest wlasnoscia zolnierza, 160)
   //  needLeft[typ]  = to samo dla rosteru `from` PO przeniesieniu (NeedByType(from))
   //  have[typ]      = sztuki tego typu w zbrojowni `from` (AiGear.Armories()[from.Id]; Equipmentish albo HorseKind; bez unikatow - ArmsPricing.IsUnique)
   //  share[typ] = have >= needMoved + needLeft ? needMoved : (int)Math.Round(have * (double)needMoved / (needMoved + needLeft))   (najwyzej needMoved i have)
   //  wybor sztuk: dla kazdego koszyka (typ*10+tier) ludzi `moved`, od najwyzszego tieru: najpierw dokladnie przedmiot ze wzorca, potem ten sam tier,
   //  potem wyzsze tiery rosnaco, potem nizsze malejaco - az do share[typ]
   //  zdjecie: arm[it] -= n (usuniecie klucza przy 0, jak MenPurse.SellAiSurplus); dodanie: AiGear.AddToArmory(to, it, n);
   //  gdy `to` jest partia lorda: AiWear.NoteSound(to, it, n) (sztuki z zalogi wchodza jako sprawne, jak zakupy - zalogi nie maja zapisu zuzycia);
   //  gdy `from` jest partia lorda - nic: AiWear.Sync sam przytnie obite do liczby sztuk (AiWear.cs, "ubytek ... obitych nie wiecej niz sztuk")
   internal static int MoveAll(MobileParty from, MobileParty to, string why)          // cala zbrojownia from -> to, potem arm.Clear()
   internal static Dictionary<int, int> NeedByType(MobileParty mp)                    // typ -> sztuk wzorcow (sl. 0-11, bez bohaterow)
   ```
   **Dlaczego proporcja, a nie "pelny komplet dla odchodzacych":** nie wiemy, ktorym ludziom brakuje sprzetu; przy zbrojowni z brakami pelny komplet dla
   odchodzacych ogolocilby zostajacych, a proporcja rozklada brak po rowno. Gdy zbrojownia ma pelny komplet albo zapas - odchodzacy biora pelny komplet,
   zapas zostaje u dawcy (lord sprzeda go w miescie, zaloga w C10).
4. **Gracz:** gra nie wola Leave/Take dla partii gracza ani (poza swiezo zdobyta twierdza) dla jego twierdz (:249) - jego zalogi i druzyna bez zmian.
   Partie jego towarzyszy biora ludzi z zalog AI tak jak lordowie AI - sprzet przechodzi z ludzmi (sciezka AI). Ludzie, ktorych gracz sam zostawia
   w zalodze na ekranie druzyny - bez zmian (jego zbrojownia DTE to osobny system, `ArmyArmory`).
5. Wylacznik `KitMovesWithMen` (domyslnie wlaczony; wylaczony = jak dzis: ludzie bez sprzetu). Liczniki w linii "Zbrojownie zalog (171): dzien" (rozdz. 8 pkt 10).
   Potkniecia: `catch` w kazdej latce -> licznik + `Log.Error` raz na dobe; wyjatek nie zatrzymuje metody gry (prefiksy/postfiksy nie zwracaja `false`).

---

## 4. CZESC B - "rekrut z tym, co ma" (kazdy pozostaly przypadek bez zapisu)

### B1. Definicja (RecruitKit.cs)
```
// Dobytek wolnego czlowieka (Z3, regula wpisu 92): rzeczy przodka tieru 1 jego linii, ktore oddzial y tez nosi
private static List<EquipmentElement> OwnOf(CharacterObject y)
{
    var root = Tier1Root(y) ?? (y.Culture != null ? y.Culture.BasicTroop : null);
    var l = new List<EquipmentElement>();
    if (root == null || root == y) return l;
    var have = new HashSet<ItemObject>(); foreach (var e in TemplateItems(root)) have.Add(e.Item);
    foreach (var e in TemplateItems(y))
        if (have.Contains(e.Item) && !MenPurse.HorseKind(e.Item)) l.Add(e);   // konie i rzedy - rozdz. B6
    return l;
}
```
- `Tier1Root(y)`: mapa rodzicow budowana leniwie raz na sesje: przejscie `MBObjectManager.Instance.GetObjectTypeList<CharacterObject>()` (bez bohaterow),
  dla kazdego celu z `UpgradeTargets` dopisac rodzica. Od `y` w gore (najpierw rodzice tej samej kultury), najwyzej 6 krokow, pierwszy przodek z `Tier <= 1`.
  Wynik (takze null) w `Dictionary<CharacterObject, CharacterObject>`; obie mapy czyszczone w `RecruitKit.Reset()`.
- **Dlaczego przeciecie, a nie caly dobytek tieru 1:** to dokladnie przyjeta regula wpisu 92 ("dobytek X, ktory Y tez nosi"); reszta dobytku zostaje w domu
  i nie trafia ani do zbrojowni, ani (jako nadwyzka) na targ - zero nowych sztuk z niczego. Skutek: "z tym, co ma" to zwykle 0-3 sztuki (buty, kaptur,
  czasem luk w linii lucznikow) - czyli "gorszy sprzet albo bez broni", jak w decyzji Jeffa.

### B2. Werbunek AI - wszystkie przypadki (`RecruitKit.OnRecruited`, nowa kolejnosc)
Sygnatura: `internal static bool OnRecruited(Hero recruiterHero, Settlement settlement, Hero source, CharacterObject troop, int amount)` (dochodzi `settlement`).
`Day()` wolane na samym poczatku (dzis tier 1 liczy sie przed `Day()` - pierwsze zdarzenie nowej doby szlo do starej; poprawka przy okazji tej samej metody).

| Kolejnosc | Przypadek | Wykrycie | Do zbrojowni partii | Skad to w swiecie | Licznik |
|---|---|---|---|---|---|
| 0 | gracz | `recruiterHero == Hero.MainHero` | DTE jak dzis (`return true`), tylko `Pop` zapisow | bez zmian | - |
| 1 | duplikat ROT | `IsRotEcho` (w AiGear, przed OnRecruited) | nic | ten sam czlowiek, komplet w zdarzeniu pierwszym | `echo` |
| 2 | jeniec | `RecruitSources.InPrisonerRecruit` (licznik glebokosci, ponizej) | nic (`return false`) | lup wzial zwyciezca | `jeniec` |
| 3 | tier 1 | `troop.Tier <= 1` | komplet DTE (`return true`) | wlasny dobytek (wpis 92) | `t1` |
| 4 | brak partii | `recruiterHero.PartyBelongedTo == null` | `return true` jak dzis (DTE i tak wychodzi) | - | - |
| 5 | ochotnik z zapisem | `Pop(source, troop) != null` | `Materialize(k)` | kupione przez notabla + dobytek | `zapis` |
| 6 | ochotnik bez zapisu | `source != null`, brak zapisu | `OwnOf(troop)` | dobytek | `notabl bez zapisu` |
| 7 | najemnik z karczmy | `source == null && settlement != null` | `OwnOf(troop)` + kon i rzad z wzorca (B6) | dobytek; kon - 160 do 167 | `najemnik` |
| 8 | bez zrodla (ochotnik z mapy, inne mody) | `source == null && settlement == null` | `OwnOf(troop)` | dobytek | `bez zrodla` |
| - | wylacznik `RecruitBringsWhatHeHas` = false | - | 2: jak tier (3 albo wzorzec); 6-8: `TemplateItems(troop)` jak dzis | jak dzis | `bez zapisu (wzorzec)` |

- Jency: prefiks + finalizer na `AccessTools.Method(typeof(RecruitPrisonersCampaignBehavior), "RecruitPrisonersAi")` (prywatna, :111) - `_prisonerDepth++/--`
  w `RecruitSources`, `internal static bool InPrisonerRecruit => _prisonerDepth > 0`. Gra wola tam `OnTroopRecruited(lord, null, null, ...)` synchronicznie (:117).
- Przypadek 2 przed 3: obszukany jeniec tieru 1 tez nic nie ma (dzis dostawal komplet DTE).
- Dozbrojenie: pan kupuje braki przez `AiGear.TryBuy` przy najblizszym wejsciu do miasta albo na dziennym postoju. **Bez wyjatku "drugi zakup tego dnia"**
  (raport 2 to proponowal) - **dlaczego:** werbunek przy wjezdzie (`BeforeSettlementEnteredEvent`, RecruitmentCampaignBehavior.cs:135, :563) biegnie PRZED
  zakupem przy wjezdzie (`SettlementEntered` -> `AiGear.OnSettlementEntered`), wiec wiekszosc najemnikow i ochotnikow miast jest dozbrajana tego samego dnia;
  nowa sciezka zakupu w srodku zdarzenia werbunku kosztowalaby przeglad polki przy kazdym rekrucie.

### B3. Dobytek osobno od kupionych rzeczy (`Kit.Own`)
**Decyzja:** `Kit` dostaje liste `Own` (dobytek czlowieka). `Materialize` = wzorzec (gdy `Template`) + `Own` + `Items`. `SellOff` i zwrot na targ zmarlego notabla
(Reconcile, :124-128) biora tylko `Items`.
**Dlaczego:** Z5 - dzis rzeczy z dobytku (przeciecie wzorcow przy awansie bez zapisu) notabl sprzedawal na targ przy odejsciu ochotnika: sztuki z niczego na polkach.
- `private class Kit { internal CharacterObject Troop; internal bool Template; internal List<EquipmentElement> Items = new ...; internal List<EquipmentElement> Own = new ...; }`

### B4. Awans w puli bez zapisu (`RecruitKit.OnUpgrade`)
- z zapisem: `Own = old.Own + (old.Template ? TemplateItems(old.Troop) : brak)`, `Items = old.Items + bought` (wzorzec ColdStart staje sie dobytkiem - to rzeczy tego czlowieka).
- bez zapisu: `Own = dobytek tieru 1 linii X, ktory Y tez nosi` = przeciecie `TemplateItems(x.Tier <= 1 ? x : (Tier1Root(x) ?? kultura.BasicTroop))` z `TemplateItems(y)`
  (bez koni), `Items = bought`. Dla X tieru 1 to dokladnie dzisiejsze dzialanie; dla X tieru 2+ bez zapisu konczy sie "przyblizenie z niczego" (raport 1, :49).
- `VolunteerKit.Missing(x, y)` liczy braki wzgledem WZORCA X, nie zapisu - zostaje (notabl kupuje to, czego wzorzec X nie ma; czego nie ma w zapisie, dokupi pan).

### B5. ColdStart (dorobek stuleci raz na kampanie) - zostaje
**Decyzja:** zostaje bez zmian w zbrojowniach i na targach. Gra przy tworzeniu kampanii napelnia pule RAZ dla kazdej osady
(`OnNewGameCreatedPartialFollowUpEnd`, RecruitmentCampaignBehavior.cs:166-176), a VolunteerKit przed `GameStarted` nic nie robi (A3), wiec w chwili
`RecruitKit.SeedCampaignStart` zapisow zwykle nie ma. Dla kazdej pary (notabl, oddzial tieru 2+): `p` = ilu w puli, `e` = ile zapisow; dopisac `p - e` zapisow
`Template = true`. **Warunek bezpieczenstwa** (krytyka 5): istniejacy zapis z niepustym `Items` (np. z innej drogi przed startem) najpierw
`SellOff(n, k, MarketOfNotable(n))` (RecruitKit.cs:79, :98)
(kupione wracaja na targ, notabl odzyskuje zloto - ta sama funkcja co przy odejsciu ochotnika), potem `Template = true`, `Items.Clear()`, `Own.Clear()`;
licznik `zapisy sprzed startu zamienione na dorobek N` w linii ColdStart.
**Dlaczego:** ColdStart ma dac ochotnikom startowym pelny dorobek (Jeff 05.10); `Materialize` = wzorzec + `Own` + `Items`, wiec zapis `Template = true`
z kupionymi `Items` dawalby dwie zbroje na czlowieka.

### B6. Konie i rzedy konskie (regula 160 bez zmian)
- Najemnik z karczmy (przypadek 7): do `OwnOf` dochodza `Horse` i `HorseHarness` z `TemplateItems(troop)` - 160: "kon najemnika z wzorca do kroku weterani (167)".
  Licznik szt. `konie i rzedy najemnikow z wzorca (160)` w linii dnia - zeby 167 wiedziala, ile zamyka.
- Ochotnik (A3, przypadki 5-6, 8): kon tylko z zapisu (kupil go notabl, VolunteerKit - kon jest czescia kluczowa). Bez zapisu - bez konia.
  **Dlaczego:** 160 mowi wprost, ze kon ochotnika kupil notabl; z wzorca bierze sie tylko kon najemnika.
- Jeniec: bez konia (zabrany w lupie).
- **Konny bez konia** (krytyka 16): oddzial konny (`troop.IsMounted`) przychodzacy bez konia (przypadki 2, 6, 8) zostaje oddzialem konnym wzorca - `MountedWage`
  placi mu zold x1.5 (sprawdza `IsMounted` wzorca, MountedWage.cs:60), a zaden system AI nie kupuje koni do zbrojowni (AiGear pomija konie). W 171 tylko
  licznik `konni bez konia N` w linii 1 (rozdz. 8) i pozycja dla 167 (rozdz. 16). **Dlaczego bez zmiany zachowania:** kon "z wzorca" bylby koniem z niczego
  (wbrew 160), a regula "bez konia = pieszy i zold pieszego" to zmiana zoldu i skladu wojska - nalezy do 167 (weterani i konie). Skala mala: szlachcice t2 ROT
  sa piesi (river/reach/crownlands/stormlands_noble_recruit, vale_page - bez slotu Horse), a przypadek 6 po A3/A4 prawie znika.

---

## 5. CZESC C - zaloga zamku kupuje w najblizszym przyjaznym miescie

### C1. Wybor miasta
**Decyzja:** `ArmyClothing.MarketTown(castle)` (ArmyClothing.cs:250-268; zmienic `private` na `internal`) - miasto handlowe (TradeBound) pierwszej wsi zamku,
ktore nie jest w wojnie z zamkiem; inaczej najblizsze niewrogie miasto. Do tego w chwili zamowienia: miasto nie oblezone, zamek nie oblezony, brak wojny
`FactionManager.IsAtWarAgainstFaction(castle.MapFaction, market.MapFaction)`, droga:
```
float d = m.GetDistance(market, castle, false, false, MobileParty.NavigationType.Default);           // m = Campaign.Current.Models.MapDistanceModel
if (!(d >= 0f && d < CartTownExit.BkLimit))                                                      // brak drogi ladowej (wyspa)
    try { d = m.GetDistance(market, castle, market.HasPort, castle.HasPort, MobileParty.NavigationType.All); } catch { d = -1f; }
if (!(d >= 0f && d < CartTownExit.BkLimit)) -> "bez drogi" (bez zamowienia)
if (s.MarketMaxDistance > 0f && d > s.MarketMaxDistance) -> "za daleko"
```
**Dlaczego:** TradeBound liczy gra (droga + wlasna frakcja) i przelicza przy wojnie, pokoju i zmianie wlasciciela (VillageTradeBoundCampaignBehavior) - koszt zero;
to samo miasto kupuje juz odziez zalogi zamku (150), wiec jedna regula "zamek zaopatruje sie w miescie swoich wsi". Ladowa siatka ROT ma 35 czesci (IslandRoads.cs:16-22),
stad zapasowa droga ladem i morzem dla zamkow wyspiarskich. `VolunteerKit.MarketOf` sie nie nadaje (linia prosta, bez wojny, zapamietane na stale - raport 3).

### C2. Zakup i platnosc (w `AiGear.TryBuy`, galaz zalogi zamku)
**Decyzja:** zaloga zamku najpierw kupuje z polki WLASNEGO zamku (jak dzis, placi pan kasie zamku), reszte brakow zamawia w miescie z C1: towar schodzi z polki
miasta w chwili zakupu, zloto pana idzie do kasy miasta w chwili zakupu, sztuki jada do zamku.
**Dlaczego:** to, co juz lezy w zamku, oplacila kasa zamku i jest na miejscu; nowe sztuki tylko z targu (Z6). Platnik = `st.OwnerClan.Leader` jak dzis (AiGear.cs:179) -
decyzja Jeffa (1) "pan zamku placi".

Wykonanie - refaktor `AiGear.TryBuy` (zachowanie partii lordow i zalog miast **1:1** jak dzis):
1. Wydzielic liczenie brakow (dzis :203-245) do
   `internal static Dictionary<int, int> Deficit(MobileParty mp, Dictionary<ItemObject, int> armory, Dictionary<int, int> needOut = null)` (koszyk `typ*10+tier` -> brak,
   po zastepstwie tierow jak dzis; `needOut` - potrzeba przed odjeciem, dla linii "Pokrycie").
2. Wydzielic petle zakupu (dzis :247-294) do
   ```
   private delegate void Deliver(EquipmentElement el, int n, int bucket, int unitPrice);
   private static int BuyLoop(Settlement market, MobileParty buyer, Dictionary<int, int> need, int budget, int maxPieces,
                              ref int pieces, List<string> bought, Deliver deliver)   // zwraca wydane zloto
   ```
   Wybor kandydata, cena (`market.Town.MarketData.GetPrice(el, buyer, false, market.Party)`), limity sztuk i budzetu, `market.ItemRoster.AddToCounts(el, -n)` - bez zmian.
   Zloto i miejsce dostawy robi `deliver` (dla partii lorda: `_add.Invoke`, `AiWear.NoteBought` (tylko nie-zaloga), `MenPurse.Take`, `lord.ChangeHeroGold`,
   `st.Town.ChangeGold`, `MoneyLedger.Note(NGear, ...)` - kod z :283-288 przeniesiony do lambdy). Kolejnosc typow `Order`, tiery 6..1, kandydaci t i t-1 - jak dzis.
3. Lordowie tylko w miastach: zaraz po wyliczeniu `garrison` i PRZED stemplem `_lastDay`:
   `if (!garrison && st.IsCastle && GarrisonCarts.On) return;` - lord odwiedzajacy zamek nie traci zakupu tego dnia w miescie.
4. Zaloga zamku (`garrison && st.IsCastle && GarrisonCarts.On`):
   - `need = Deficit(...)`; `GarrisonCarts.SubtractTransit(st, need)` (towar w drodze, C5);
   - `bool orderDay = GarrisonCarts.CanOrderToday(st)` (C6); budzet i limit sztuk (poprawione po krytyce 4):
     dzien bez zamowienia - jak dzis: `budget = (pan.Gold - AiGearGoldReserve) * p`, `maxPieces = AiGearMaxPiecesPerVisit`;
     dzien zamowienia - na wszystkie doby miedzy zamowieniami (D = `Math.Max(1, GarrisonOrderDays)`):
     `budget = (pan.Gold - AiGearGoldReserve) * (1 - Math.Pow(1 - p, D))`, `maxPieces = AiGearMaxPiecesPerVisit * D`
     (p = `AiGearBudgetPercent / 100`; przy 25% i D = 3: 57.8% kiesy ponad rezerwe i 180 szt.), bez sakiewki ludzi (C7);
   - `spent = BuyLoop(st, mp, need, budget, maxPieces, ...)` z dostawa do zbrojowni jak dzis (polka zamku, zloto pana -> kasa zamku); licznik "z polki wlasnego zamku";
   - gdy `orderDay`, zostal brak i zostal budzet: `var market = GarrisonCarts.MarketFor(st, out float dist, out string why)`;
     gdy `market != null`: `var lines = new List<GarrisonCarts.Line>(); int paid = 0;` i `BuyLoop(market, mp, need, budget - spent, maxPieces - pieces, ...)` z dostawa:
     ```
     (el, n, bucket, unit) => { lord.ChangeHeroGold(-unit * n); market.Town.ChangeGold(unit * n); MoneyLedger.Note(MoneyLedger.NGear, market, unit * n);
                                lines.Add(new GarrisonCarts.Line { El = el, N = n, Bucket = bucket }); paid += unit * n; }
     ```
     potem, gdy `lines.Count > 0`: `GarrisonCarts.Place(st, market, st.OwnerClan, lines, paid, dist)`;
   - zamowienia dla warsztatow (`SupplyDemand.NoteUnmetOnce`, dzis :298-308) dla brakow zalogi zamku - tylko w dzien zamowienia, w `market` i ze sprawdzeniem
     `onShelf` na POLCE MIASTA (`market.ItemRoster`), nie zamku (krytyka 15); bez miasta (brak drogi albo miasta) - nic. **Dlaczego:** wpis 81 - "za drogie"
     albo "limit sztuk/budzetu wyczerpany" to nie brak towaru; popyt zamku nie ma juz odbiorcy w zamku (C8 - DailyTrade nie wozi do zamkow).
   - **Dlaczego budzet na D dob:** zamowienie zastepuje D dziennych zakupow, wiec moze wydac tyle, ile wydalyby one razem (1 - (1-p)^D kiesy, nie wiecej);
     z dziennym limitem 60 szt. na 3 doby zamek po C8 (polka zamku tylko schodzi) dostawalby najwyzej 20 szt./dobe - zaloga ok. 250 ludzi x 4.45 szt.
     uzbrajalaby sie ok. 2 miesiace, a przez D2 prawie nie cwiczyla. Koszt przegladu polki miasta zostaje raz na D dob.
5. Zaloga miasta i partie lordow: dokladnie jak dzis (BuyLoop na polce swojej osady).
6. Linia "ZakupyAI: dzien ..." bez zmian w znaczeniu: zamowienia zamkow wliczone w szt. i zloto garnizonow (to sa zakupy); szczegoly w linii "Zaopatrzenie zamkow (171)".

### C3. Dostawa - "zamowienie w drodze" bez partii (NOWY plik `Armoury/src/GarrisonCarts.cs`, `internal static class GarrisonCarts`)
**Decyzja:** bez prawdziwego wozu na mapie; zamowienie to zapis z data przyjazdu. Czas = `max(1, ceil(dist / MarketCarts.PerDay(s)))` dob
(`MarketCarts.PerDay` - zmienic `private` na `internal`; ok. 41 jedn./dobe przy WorldPacePercent 50), droga morska liczona ta sama predkoscia (ostroznie wolniej niz statek).
**Dlaczego:** wlasne partie wozow wymagaja zapisywalnego typu komponentu (psuje zapis po zdjeciu moda), AI ruchu, straznika BK i do ok. 130 partii na mapie (raport 3);
predkosc wozu wsi = te same wozy, ktore woza plon (MarketCarts); bandyci nie rozbijaja dostawy - swiadome uproszczenie.

```
internal static bool On { get { var s = Settings.Current; return s != null && s.GarrisonGearFromTown && AiGear.On; } }
internal sealed class Line { internal EquipmentElement El; internal int N; internal int Bucket; }    // internal: AiGear buduje liste (krytyka 10, CS0051); Bucket = koszyk potrzeby, na ktory kupiono (t albo t-1)
private sealed class Order { internal Settlement Castle, Market; internal Clan PayerClan; internal IFaction FactionAtOrder;              // FactionAtOrder = Castle.MapFaction w chwili zamowienia (C4)
                             internal int DayOrdered, DayDue, Paid; internal List<Line> Lines = new List<Line>(); }
private static readonly List<Order> _orders = new List<Order>();
private static readonly Dictionary<Settlement, Dictionary<int, int>> _transit = new ...;   // zamek -> koszyk -> szt. w drodze (z _orders)
private static readonly Dictionary<Settlement, int> _lastOrder = new ...;                  // zamek -> doba ostatniego zamowienia (bez zapisu)
private static string _pending;                                                            // z SyncData, rozwiazywane po starcie sesji
internal static void Reset();   internal static void SubtractTransit(Settlement castle, Dictionary<int, int> need);
internal static bool CanOrderToday(Settlement castle);   internal static Settlement MarketFor(Settlement castle, out float dist, out string why);
internal static void Place(Settlement castle, Settlement market, Clan payer, List<Line> lines, int paid, float dist);
internal static void Daily();   internal static string Export();   internal static void Import(string s);   internal static void ResolvePending(string why);
```

### C4. Zdarzenia w drodze (`GarrisonCarts.Daily`, raz na dobe; kolejnosc warunkow)
| Warunek (dla kazdego zamowienia) | Co sie dzieje | Dlaczego |
|---|---|---|
| `today < DayDue` | jedzie dalej | - |
| `today - DayOrdered > 30` | **zawraca** (zwrot jak nizej) | ten sam prog co "wiesc z drogi" wozow wsi (MarketCarts.cs:659); sprawdzany PRZED warunkami "czeka", inaczej woz czekajacy pod oblezonym zamkiem nigdy by nie zawrocil (krytyka 9) |
| rod placacy zyje (`PayerClan != null && !PayerClan.IsEliminated`) i zamek w wojnie z nim (`IsAtWarAgainstFaction(Castle.MapFaction, PayerClan.MapFaction)`) | **zawraca** (zwrot) | zamek padl - towaru nie oddaje sie wrogowi |
| rod placacy wymarl (`PayerClan == null` albo `PayerClan.IsEliminated`) i `Castle.MapFaction != FactionAtOrder` | **zawraca bez zwrotu** | nie ma kogo zapytac o wojne, a zamek zmienil strone - towar nie jedzie do obcych (krytyka 9) |
| zamek oblezony (`Castle.IsUnderSiege`) | czeka (sprawdzane co dobe) | woz nie wjedzie przez linie oblezenia |
| zamek bez zalogi (`Castle.Town.GarrisonParty == null`) | czeka | nie ma komu oddac |
| inaczej | **dostawa**: kazda linia `AiGear.AddToArmory(garrison, el.Item, n)`; `_transit` w dol | - |

- **Zmiana pana w tym samym krolestwie / pan zginal:** dostawa do zalogi bez zwrotu. **Dlaczego:** towar jest dla ludzi zalogi, ktorzy zostaja z zamkiem;
  oplacony towar przechodzi z zamkiem (przelew miedzy rodami, nic nie znika).
- **Wojna miedzy zamkiem a miastem wybuchla po wyjezdzie:** woz dojezdza. **Dlaczego:** wyjechal w pokoju; liczy sie tylko stan celu.
- **Zawrocenie:** sztuki wracaja na polke miasta-zrodla (`Market.ItemRoster.AddToCounts(el, n)`, z modyfikatorami); zwrot zaplaty
  `refund = min(Paid, Market.Town.Gold)` do `PayerClan.Leader` (gdy rod istnieje i glowa zyje), `Market.Town.ChangeGold(-refund)`, `MoneyLedger.Note(NGear, Market, -refund)`.
  Rod wymarly -> bez zwrotu (towar wraca, zloto zostaje w kasie miasta - jak zwrot rzeczy zmarlego notabla w Reconcile). **Dlaczego pelna zaplata, a nie cena skupu:**
  kupiec nie dowiozl - kontrakt zerwany po jego stronie; zwrot ograniczony kasa miasta, wiec nic nie powstaje z niczego.
- **Brak towaru w miescie:** nic nie kupione, zamowienie dla warsztatow w tym miescie (C2.4), licznik.
- Wylacznik `GarrisonGearFromTown` wylaczony w trakcie gry: nowe zamowienia ustaja, `Daily` dalej dowozi albo zawraca oplacone (nic nie przepada).

### C5. Towar w drodze odejmowany od potrzeby
`SubtractTransit(castle, need)`: dla kazdego koszyka `need[k] = max(0, need[k] - _transit[castle][k])`. Klucz = koszyk potrzeby, na ktory kupiono (zakup t-1 na brak t liczy sie
do t) - **dlaczego:** inaczej zamek co dobe zamawialby to samo. `_transit` budowane z `_orders` (Place/dostawa/zawrocenie i po wczytaniu w ResolvePending).

### C6. Jak czesto zamek zamawia
**Decyzja:** zamek sklada zamowienie w miescie najwyzej raz na `GarrisonOrderDays` = 3 doby (polka wlasnego zamku - codziennie jak dzis); zamowienie obejmuje
wszystkie doby od poprzedniego: budzet `1 - (1-p)^D` kiesy ponad rezerwe i limit `AiGearMaxPiecesPerVisit x D` sztuk (C2.4, krytyka 4).
`CanOrderToday(castle)` = brak wpisu w `_lastOrder` albo `today - _lastOrder[castle] >= D`; wpis stawiany przy kazdej probie zamowienia (takze "bez miasta",
"bez drogi", "brak towaru"), zeby nieudana proba nie powtarzala przegladu polki miasta co dobe.
**Dlaczego:** woz kursuje co kilka dni (droga tam i z powrotem 2-6 dob przy 41 jedn./dobe), a przeglad polki miasta z wycena kazdego kandydata to najdrozsza czesc
AiGear (pomiar 08.10: AiGear.OnDailyTickParty 4 ms/klatke po roku) - codzienne zamowienia ok. 130 zamkow (227 twierdz z zaloga - 97 miast, log ColdStart) dokladalyby kosztu.

### C7. Budzet pana
**Decyzja:** bez zmian (ta sama formula co dzis dla zalogi: 25% kiesy pana ponad 2 000 na zakup, bez sakiewki ludzi).
**Dlaczego:** jedna regula budzetu; paczka 166 BUDZET RODU zastepuje ja w jednym miejscu (`AiGear.cs:192`, projekt ekonomii rozdz. 6.1: sprzet 0.17/0.22 x D).
Uwaga dla 166 (rozdz. 16): pan z wieloma lennami placi 25% kiesy NA KAZDA zaloge co dobe - przy 3 zalogach do ok. 58% kiesy dziennie; w dzien zamowienia zamku
(C6) jedna zaloga moze wziac do 57.8% (to samo, co 3 dzienne zakupy, tylko naraz). Po naprawie 1.1 (zapis zbrojowni, C9/C9a) i A7 (sprzet idzie z ludzmi)
duze braki zdarzaja sie juz tylko przy prawdziwych przyczynach (nowa zaloga, awanse, straty) - dlatego wspolny budzet rodu zostaje w 166.

### C8. Zamek przestaje byc targiem broni (`SupplyDemand.DailyTrade`)
**Decyzja:** przy `GarrisonCarts.On` zamek nie jest celem wywozu nadwyzek: w petli celow (SupplyDemand.cs:436-448) `if (noCastles && dst.IsCastle) continue;`
(`noCastles = GarrisonCarts.On`, policzone raz przed petla). Zamek zostaje ZRODLEM - jego zapas ponad popyt kupcy wywoza do miast (placi miasto kasie zamku).
**Dlaczego:** Z6 - natychmiastowe przenoszenie bez drogi bylo teleportem towaru; zapas, ktory juz lezy w zamkach, splywa do miast albo kupuje go zaloga (C2).
Zalogi miast kupuja u siebie jak dzis (to juz zgadza sie z decyzja (1)).

### C9. Zbrojownia zalogi przetrwa zapis gry (NOWY plik `Armoury/src/GarrisonArmory.cs`, `internal static class GarrisonArmory`)
**Decyzja:** Armoury zapisuje zbrojownie wszystkich zalog (miasta i zamki, takze gracza) i po wczytaniu ustawia je z zapisu.
**Dlaczego:** rozdz. 1.1 - DTE ich nie zapisuje; to naprawa bledu (nic nie powstaje: odtwarzamy to, co bylo), wiec dotyczy kazdej zalogi.

- `Export()`: zawsze zaczyna sie znacznikiem `v1|` (zeby odroznic "zapis z 171 bez zalog" od "brak klucza"). Gdy `!GarrisonArmorySurvivesSave` - `v1|off`.
  Inaczej `v1|` + dla kazdej `Settlement.All` z `IsFortification` i `Town.GarrisonParty != null`: wpis zbrojowni DTE (`AiGear.Armories()[garrison.Id]`),
  gdy niepusty: `osada>przedmiot:ile,przedmiot:ile~` (id osady i przedmiotu = `StringId`; bez modyfikatorow - DTE ich nie trzyma).
- `Import(string s)`: tylko zapamietanie: `_loaded = true; _pending = s;` (moze byc null - brak klucza), jak RecruitKit 161. `_loaded` i `_pending` czyszczone w `Reset()`.
- `Restore(string why)` z `OnSessionLaunched` (gra wola `OnGameLoaded` DTE PRZED `OnSessionStart`, Campaign.cs:1670-1671 - wiec DTE juz skonczyl);
  na poczatku `if (_restored) return; _restored = true;` (`_restored` czyszczone w `Reset()`; Restore moze tez zawolac Export - rozdz. 10):
  - `!_loaded` -> nowa gra, nic (zbrojownie daje ColdStart);
  - `_pending == "v1|off"` -> nic (zapis zrobiony z wylaczonym przelacznikiem - jak przed 171);
  - `_pending == null` albo bez znacznika `v1|` -> **stary zapis** -> C9a;
  - inaczej: osada przez `MBObjectManager.Instance.GetObject<Settlement>(id)`, zaloga `Town.GarrisonParty`; gdy zbrojownia DTE juz ma wpis (np. DTE dal cos
    przy wczytaniu) - liczymy jego sztuki jako "zastapione" i `Clear()`; potem `AiGear.AddToArmory(g, item, n)` (DTE sam rozwiaze przedmiot i zablokuje czarna liste).
  Log: `Zbrojownie zalog (171): po wczytaniu przywrocone 224 zalog, 60950 szt. (bez zalogi 1, zastapione z DTE 0 szt., nieznane przedmioty 0).`
- Przy zapisie raz: `Zbrojownie zalog (171): zapis - 225 zalog, 61200 szt.`
- Rozmiar: ok. 227 zalog x 30-150 pozycji x ok. 30 znakow = 0.2-1 MB napisu -> `SaveText.Sync` (kawalki po 8 000 znakow); zapis rosnie o tyle (dzis 31.5 MB po roku).

### C9a. Stary zapis - jednorazowe odtworzenie zbrojowni zalog AI (NOWE po krytyce 7)
**Decyzja:** przy pierwszym wczytaniu zapisu sprzed 171 (brak klucza `arm_garrisonarmory`) kazda zaloga AI (wlasciciel nie jest rodem gracza, nie Inni)
dostaje do zbrojowni brakujace sztuki wzorca swoich ludzi - ten sam algorytm co `ColdStart.Armories` (tylko zalogi). Wylacznik
`GarrisonArmoryRestoreOldSave` (domyslnie wlaczony; wylaczony = zalogi puste, panowie kupuja od nowa jak dzis).
**Dlaczego:** to naprawa skutku bledu DTE, nie dosypka: ten sprzet byl oplacony i lezal w zalogach przed zapisem, a zniknal tylko dlatego, ze DTE go nie zapisal -
ta sama zasada co C9 ("odtwarzamy to, co bylo"). Bez tego pierwsze wczytanie obecnej kampanii Jeffa powtarza dokladnie blad 1.1: log 10-54-00 - garnizony
wydaja 489-868 tys. zl na dobe przez co najmniej 12 dob (razem ok. 7.8 mln), kapital Banku spada z 700 do 70 tys., dlug rodow rosnie z 6.77 do 7.53 mln -
fala bankructw, ktorej Jeff wprost nie chce. Wzorzec to gorna granica tego, co bylo (stan sprzed zapisu jest nieznany) - swiadome przyblizenie, opisane w 14.
Wykonanie:
- `ColdStart.cs`: wydzielic cialo petli `Armories()` (liczenie brakow w koszykach i dokladanie sztuk wzorca) do
  `internal static int FillToTemplate(MobileParty mp)` (zwraca dolozone sztuki); `Armories()` wola je dla kazdej partii jak dzis - zachowanie 1:1.
- `GarrisonArmory.Restore`, galaz "stary zapis": gdy `GarrisonArmoryRestoreOldSave`: dla kazdej `Settlement.All` z `IsFortification`, `Town.GarrisonParty != null`,
  `OwnerClan != Clan.PlayerClan` i nie Inni (`!Undead.Party(g)`): `pieces += ColdStart.FillToTemplate(g)`.
  Log: `Zbrojownie zalog (171): stary zapis bez zbrojowni zalog - odtworzone po bledzie DTE 219 zalog AI, 341200 szt. (wzorce ich ludzi, jednorazowo; zalogi gracza bez zmian).`
  Wylaczony: `Zbrojownie zalog (171): stary zapis - brak klucza, odtworzenie wylaczone w MCM; zalogi bez zbrojowni do pierwszego zapisu z 171.`
- Zalogi gracza - bez odtworzenia (jak ColdStart, ktory tez je pomija: sprzet z niczego dla gracza to prezent); od pierwszego zapisu z 171 jego zalogi sa zapisywane.

### C10. Nadwyzka zalogi na targ (NOWE po krytyce 3)
**Decyzja:** raz w tygodniu kazda zaloga AI sprzedaje to, czego ma ponad potrzebe swoich ludzi i 10% zapasu (`SurplusKeepPercent`), po cenie skupu; zloto
dostaje pan (`OwnerClan.Leader`, zywy). Zaloga miasta - na polke swojego miasta (placi kasa miasta). Zaloga zamku - na polke swojego zamku (placi kasa zamku),
skad kupcy wywoza nadwyzke do miast (C8: zamek zostaje zrodlem).
**Dlaczego:** Z9 - dzis nadwyzki zalog (awanse o 2+ tiery, zamiany ROT w zalogach `ROTTroopRecruiter.DailyTickSettlement` :51-75, lup z obrony, ludzie zabrani
przez lorda) nie maja ujscia (`MenPurse.SellAiSurplus` tylko dla `IsLordParty`, MenPurse.cs:168), a wczytanie zapisu je kasowalo; po C9 rosna bez konca, zabieraja
sprzet z rynku i powiekszaja zapis. Polka wlasnego zamku zamiast wozu do miasta: zadnej nowej drogi towaru - zamek jest zrodlem dla kupcow juz dzis.
Wykonanie (`GarrisonArmory.Daily()`, raz na dobe, z `ArmouryBehavior.OnDailyTick` tuz po `GarrisonCarts.Daily()`):
- Zaloga w kolejce dnia: `(int)(g.Id.InternalValue % 7) == today % 7` (jak DTE `GarbageCollectEquipments`) - kazda zaloga raz na 7 dob, ok. 32 dziennie.
- Pomijane: wylacznik `GarrisonSellsSurplus` = false; DTE nie znaleziony; zaloga gracza bez `GarrisonBuysGearPlayer`; Inni; osada oblezona; zaloga w bitwie.
- Potrzeba = `NeedByType(g)` + ludzie patroli BK tej osady (BK zabiera na patrol do 50% zdrowych ludzi zalogi, sprzet zostaje w zamku -
  `GarrisonPartyComponent.CreateParty`, bk\BannerKings.Components\GarrisonPartyComponent.cs:67-80; bez tego zaloga sprzedalaby sprzet ludzi na patrolu).
  Patrole: raz na dobe, tylko gdy ktoras zaloga jest w kolejce, jedno przejscie `MobileParty.All` z `p.PartyComponent?.GetType() == _bkPatrolType`
  (`AccessTools.TypeByName("BannerKings.Components.GarrisonPartyComponent")`, raz na sesje) -> `Dictionary<Settlement, List<MobileParty>>` po `HomeSettlement`.
- `internal static int SellSurplus(MobileParty mp, Dictionary<int, int> needByType, Settlement market, Hero payee, float keepPercent, string why)`:
  dla kazdego typu: `extra = have - (int)Math.Ceiling(need * (1 + keepPercent / 100))`; sprzedaz najgorszych sztuk (tier, potem wartosc - jak MenPurse) bez koni
  i rzedow (`MenPurse.HorseKind`) i bez unikatow (`ArmsPricing.IsUnique`); cena `market.Town.MarketData.GetPrice(el, mp, true, market.Party)` (skup tej polki),
  najwyzej tyle, ile kasa `market.Town.Gold` udzwignie; sztuki `market.ItemRoster.AddToCounts`, zloto `market.Town.ChangeGold(-unit)` -> `payee.ChangeHeroGold(unit)`;
  zdjecie ze zbrojowni jak `MenPurse.SellAiSurplus` (`arm[it]--`, usuniecie klucza przy 0). Stan sztuki: partia lorda (A7, rozwiazana) -
  `new EquipmentElement(it, AiWear.TakeCondition(mp, it))` jak MenPurse (obita idzie jako obita); zaloga - `new EquipmentElement(it)` (zalogi nie maja zapisu
  zuzycia - jak przy zakupach). Ta sama funkcja sluzy A7 (rozwiazana partia, `keepPercent` = 0). Pomijana zaloga w bitwie: `g.MapEvent != null`.
- Liczniki w linii "Zbrojownie zalog (171): dzien" (rozdz. 8 pkt 10). Koszt: ok. 32 zalogi dziennie x 30-150 pozycji zbrojowni + jedno przejscie `MobileParty.All`.

---

## 6. CZESC D - szkolenie

### D1. Co zostaje, a co jest "z niczego" [K]
| Zrodlo doswiadczenia | Kto | Co robimy | Dlaczego |
|---|---|---|---|
| Budynki Training Fields (miasto 1/2/3, zamek 3/4/5 na dobe) i codzienny projekt "Drills" zamku (8) - `GarrisonRecruitmentCampaignBehavior.HandleGarrisonXpChange` (:110-122) | zalogi | zostaje, x udzial uzbrojonych (D2) | plac cwiczen i musztra sa prawdziwe; bez broni musztra nie uczy |
| BK `BKPartyTrainningModel.GetEffectiveDailyExperience`: 15 + 3 x tier (druzyna glowy rodu) albo 10 + 2 x tier na czlowieka dziennie dla partii lordow AI (bk\...\BKPartyTrainningModel.cs:30-50) + perki (CombatTips, RaiseTheMeek, Drills, BullsEye) | partie lordow AI, zalogi (perki) | x udzial uzbrojonych (D3) | to codzienne cwiczenia druzyny - ta sama zasada Z8; glowny silnik awansow AI, a wiec popytu na sprzet |
| Oboz BEE (XP z niczego, `ApplyAiTrainingCampPassive`) | zamki AI | zamyka 170 | - |
| Mnoznik polityki BK zalogi (x0.7 / x1.3) | zalogi | zostaje (mnozy sie z naszym) | decyzja pana |
| Bitwy, oblezenia | wszyscy | zostaje | prawdziwe doswiadczenie |
| Oplata za awans zolnierza (`PartyUpgraderCampaignBehavior` :139-150, `GiveGoldAction(owner, null)`, takze zalogi :54-60) | wszyscy | NIE tu - pozycja dla 164a (juz w projekcie: "awanse do sakiewki partii") | rozdz. 16 |

### D2. Cwiczenia zalogi (NOWY plik `Armoury/src/ArmsDrill.cs`, `internal static class ArmsDrill`)
- Postfiks (Priority.Last) na `CalculateGarrisonXpBonusMultiplier(Town)` kazdej niestabstrakcyjnej podklasy `DailyTroopXpBonusModel` (przeglad typow jak
  `SupplyDemand.ApplyAll`; BK lata `DefaultDailyTroopXpBonusModel` swoim postfiksem - mnozenie jest przemienne), z licznikiem zagniezdzenia
  (prefiks `Priority.First` depth++, finalizer depth--, postfiks dziala tylko przy depth == 1 - jak SupplyDemand :272-276):
  ```
  public static void GarrisonXpPostfix(Town __0, ref float __result)
  // gdy GarrisonDrillNeedsArms && __0?.GarrisonParty != null && Gated(__0.GarrisonParty): __result *= ArmedShare(__0.GarrisonParty)
  ```
- `Gated(garrison)`: nie Innych (`!Undead.Party(g)`), a wlasciciel != rod gracza albo `GarrisonBuysGearPlayer` (zaloga gracza tylko w pelnym systemie - rozdz. 12).
- Gra wola te metode raz na twierdze na dobe (GarrisonRecruitmentCampaignBehavior.cs:113) - koszt: jeden `ArmedShare` na twierdze dziennie.

### D3. Cwiczenia druzyny lorda AI
- Postfiks (Priority.Last, ten sam wzorzec zagniezdzenia) na `GetEffectiveDailyExperience(MobileParty, TroopRosterElement)` kazdej podklasy `PartyTrainingModel`
  (BK `BKPartyTrainningModel`; NavalDLC tylko przekazuje - licznik zagniezdzenia nie policzy dwa razy):
  ```
  public static void TrainingPostfix(MobileParty __0, TroopRosterElement __1, ref ExplainedNumber __result)
  // gdy PartyDrillNeedsArms i Gated(__0): share = ArmedShare(__0); gdy share < 1: lost += __result.ResultNumber * (1 - share) * __1.Number;
  //                                       __result = new ExplainedNumber(__result.ResultNumber * share);
  ```
  `Gated` dla partii: `__0.IsLordParty && __0 != MobileParty.MainParty && __0.ActualClan != Clan.PlayerClan && !Undead.Party(__0)`; dla zalogi - jak D2
  (perki gubernatora). Opisy ExplainedNumber gubimy swiadomie - partii AI nikt nie oglada.
- **Dlaczego wlaczone tez dla partii, choc decyzja (3) mowila o zalodze:** jedna zasada Z8 dla jednego zjawiska (codzienne cwiczenia), a to jedyny hamulec,
  ktory bez tworzenia czegokolwiek z niczego dopasowuje tempo awansow AI do ilosci broni w swiecie (rozdz. 7). Rekrut tieru 1 przychodzi z wlasna bronia, wiec
  armie nie stoja w miejscu - zwalniaja dopiero awanse na szczebel, ktory wymaga innego rodzaju broni, ktorej nie ma.

### D4. Udzial uzbrojonych (`ArmsDrill.ArmedShare(MobileParty mp)`)
```
// 1.0, gdy DTE nie znaleziony albo partia bez ludzi (bez bohaterow); pamiec: Dictionary<MobileParty, float> na biezaca dobe (czyszczona przy zmianie doby)
need[typ] += el.Number     // typ = MainWeaponType(ch): strzelec (ch.IsRanged) - pierwszy Bow/Crossbow w slotach 0-3;
                           //   inaczej pierwszy OneHandedWeapon/TwoHandedWeapon/Polearm w slotach 0-3; inaczej pierwszy Thrown; brak - czlowiek nie wchodzi do rachunku
have[typ] = suma sztuk zbrojowni DTE (AiGear.Armories()[mp.Id]) o tym ItemType (dowolny tier - cwiczy sie kazdym mieczem)
share = suma_typ min(need, have) / suma_typ need
```
**Zmiana po recenzji kodu (uwaga 13 w rozdz. 18):** "dowolny tier" sprawial, ze wlocznia t1, ktora rekrut zawsze przynosi, "uzbrajala" czlowieka t5 - hamulec
z 7.3 pkt 3 prawie nigdy nie dzialal. Teraz koszyk glownej broni = typ x tier przedmiotu ze wzorca (`AiGear.Bucket`), a pokrywa go sztuka tego typu o tierze
>= t-1 (te same sztuki, ktore `AiGear.Deficit` uznaje za pokrycie). Przydzial od najwyzszego szczebla (sztuki nadajace sie wyzszym nadaja sie tez nizszym, wiec
kolejnosc nie psuje pokrycia nizszych). `ArmedShare` = udzial calej partii (zaloga - mnoznik D2, linia "Pokrycie"); w D3 udzial **oddzialu** (element rosteru):
wolniej cwiczy ten, kogo nie ma czym uzbroic na jego szczeblu, a nie cala druzyna. Cwiczenia wedlug broni dzialaja tylko przy zakupach AI (`AiGear.On`;
zaloga - takze `GarrisonBuysGear`; uwaga 9): bez zakupow DTE kasuje zbrojownie zalog co dobe i mnoznik zatrzymalby szkolenie wszystkich zalog.
`MainWeaponType` z `ch.Equipment` (to samo co `AiGear.NeedBuckets`), wynik w pamieci na `CharacterObject` (czyszczona w Reset). Ta sama "glowna bron" co
`VolunteerKit.IsKey` (wpis 76/78).

---

## 7. CZESC E - BILANS BRONI

### 7.1 Co mowia logi [P] (log roczny T3 `Armoury-2026-10-08_08-18-38.log`, srednie dobowe; skrypt `SCR(7016f733)\p171\rok.py`)
| | doby 1-30 | doby 150-179 | doby 330-359 |
|---|---|---|---|
| ludzie w partiach rodow / w zalogach | 64 tys. / 50 tys. | 110 tys. / 78 tys. | 100 tys. / 78 tys. |
| zwerbowani wedlug logu / prawdziwi (bez duplikatow ROT) | 4 018 / ok. 2 050 | 1 317 / ok. 620 | 1 272 / ok. 600 |
| "Komplet rekruta": tier 1 / bez zapisu (wzorzec) / z zapisu (z duplikatami) | 2 772 / 984 / 51 | 569 / 606 / 108 | 572 / 541 / 144 |
| ZakupyAI: szt. na dobe (zloto garnizonow) | 3 172 (46 tys.) | 2 156 (67 tys.) | 1 899 (77 tys.) |
| Warsztaty: szt. na dobe | 708 | 588 | 489 |
| w tym zbroja korpusu / bron jednoreczna | 20 / 33 (d1-89) | 10 / 22 | 2 / 43 |
| "Odpuszczone z braku surowca (glownie ruda)" - cykli na dobe | 406-1 119 (start, log 11-40-05) | - | - |
| sakiewki ludzi: nadwyzki sprzedane na targ, szt. na dobe | 4 740 | 4 975 | 6 568 |
| polki miast: sprzet razem | 20.8 tys. (d0) -> 58 tys. (d30) | 441 tys. | 1.02 mln (d360: 1.06 mln, w tym zbroja korpusu 254 tys., bron 1H 384 tys., nogi 295 tys.) |
| przyrost polek, szt. na dobe | +1.2 tys. | +2-4 tys. | +2.4 tys. (srednio w roku +2.9 tys.) |

Wniosek: kowale robia ok. 0.5-0.7 tys. sztuk dziennie, a polki rosna o ok. 2.9 tys. dziennie - **ok. 2/3 rocznego przyrostu polek to sprzet z niczego**,
ktory armie oddaja na targ jako nadwyzke (duplikaty ROT, wzorce "bez zapisu", dobytek tieru 1). Przez ten zalew kowale prawie nie kuja zbroi korpusu
(2 sztuki dziennie pod koniec roku): sa "bez zysku", bo polki sa zawalone.

### 7.2 Co zmienia 171 (sztuki na dobe; 4.45 szt. na komplet - ColdStart: 344 099 szt. / 77 tys. ludzi) [S]
| Zrodlo | Start kampanii | Po roku |
|---|---|---|
| duplikat ROT - drugi komplet z niczego | -2 700 x 4.45 = **-12.0 tys.** | -600 x 4.45 = **-2.7 tys.** |
| tier 2+ bez zapisu: notabl 270 / 40, najemnik 375 / 45, jeniec ok. 50 / 70 - wzorzec zamiast dobytku (ok. 0.5 szt.) | -(695 x 4) = **-2.8 tys.** | -(155 x 4) = **-0.6 tys.** |
| razem mniej sprzetu z niczego doplywajacego do armii | **ok. -15 tys.** | **ok. -3.3 tys.** |
| pozostaje z niczego (przyjete): dobytek tieru 1 (DTE), ColdStart raz, kon i rzad najemnika (160, do 167) | bez zmian | bez zmian |
| NOWE z niczego (przyjete, A5): dobytek tieru 1 przy autowerbunku zalog (najwyzej 1 czlowiek na twierdze na dobe) | **+0.2-0.7 tys.** [S] (gorna granica +1.0 tys.) | **+0.2-0.7 tys.** [S] |
| partia <-> zaloga, rozwiazana partia do zalogi (A7) - przeniesienie, nie doplyw | 0 (mniej zakupow zalog i mniej nadwyzek lordow na targu) | 0 |
| nadwyzki zalog na targ (C10) - przeniesienie istniejacych sztuk z zalog na polki | + (nie mierzone dzis; linia 10) | + |
| po wczytaniu zapisu: zalogi nie kupuja na nowo calego sprzetu (1.1) | - | dzis ok. -10 tys. szt. i -0.5..-0.87 mln zl dziennie przez TYGODNIE po kazdym wczytaniu (log 10-54-00: 12 dob, ok. 7.8 mln zl, Bank 700 -> 70 tys.); po 171 - 0 |
| pierwsze wczytanie zapisu sprzed 171 (C9a) - jednorazowo, do zbrojowni zalog AI, nie na targ | - | raz: ok. ludzie zalog AI x 4.45 (kampania T3: do ok. 0.35 mln szt.) zamiast ok. 7.8 mln zl zakupow |

Bilans rynku po 171: start ok. +1.2 tys. - 15 tys. + 0.2-0.7 tys. = **niedobor rzedu 5-14 tys. szt. dziennie** (gorna granica: czesc kompletow z niczego lezala
dotad w zbrojowniach bez uzytku), polki startowe 17.5 tys. szt. bez koni - **kluczowe koszyki (zbroja korpusu, bron 1H, helmy) puste po kilku dniach nowej kampanii.**
Po roku: ok. +2.9 tys. - 3.3 tys. + 0.2-0.7 tys. = **ok. -0.2..+0.3 tys. dziennie, czyli rownowaga**; w trwajacej kampanii (T3: 1 mln szt. na polkach po roku)
zapas starczy na lata.

### 7.3 Co sie dzieje, gdy broni brakuje - armie AI nie stoja
1. **Wojny AI z AI** (symulacja bitwy) - zbrojownia nie ma znaczenia [K, 1.4]. Brak broni nie zatrzymuje ani nie oslabia armii AI w ich wojnach.
2. **Bitwa z graczem:** pusty slot = sprzet chlopa kultury na czas bitwy (DTE, nie z magazynu) i kara morale do -20 [K] - to jest dokladnie "idzie do boju z tym, co ma".
3. **Awanse AI** nie wymagaja sztuk (DTE), ale **zwalniaja przez D3**: druzyna bez broni swojego rodzaju cwiczy mniej, wiec rzadziej awansuje na szczebel,
   ktorego nie ma czym uzbroic - popyt sam dopasowuje sie do podazy (bez tworzenia czegokolwiek).
4. **Pule ochotnikow:** notabl bez towaru cofa awans (VolunteerKit, dzis 40-70% cofniec na starcie) - wiecej rekrutow tieru 1 z wlasnym dobytkiem, mniej tieru 2+ do dozbrojenia.
5. **Rynek:** brak = zamowienie (`NoteUnmetOnce`) -> wyzszy popyt -> wyzsza cena -> warsztaty biora najlepiej placace sztuki. Po zniknieciu zalewu cena zbroi korpusu
   wroci i kowale zaczna ja kuc (dzis 2/dobe tylko dlatego, ze polki sa zawalone).
6. **Naprawy** (AiWear, kowale miast) trzymaja w uzyciu to, co jest; wraki ida na zlom (158).
7. **Gorsza sztuka:** AiGear juz kupuje t-1 na brak t (wpis 89). **Swiadomie NIE dodaje t-2 i nizej:** pokrycie t-2 musialoby liczyc sie tez po awansie
   (stara zbroja t2 "pokrylaby" potrzebe t4 i awans bylby darmowy w sprzecie), a kupno "na chwile" i sprzedaz nadwyzki po 10% ceny nowej (sufit skupu B3) drenowaloby kiesy lordow.

### 7.4 Waskie gardlo poza 171 (do projektu produkcji)
Warsztaty: "odpuszczone z braku surowca 406-1 119 cykli dziennie, glownie ruda" (start). Po 171 to ruda, nie zalew, bedzie ograniczac zbroje i bron.
~~Jesli test 40 dob pokaze pokrycie ponizej 70%, nastepny krok: `ColdStartMarketDays` 14 -> 60.~~ **Decyzja po recenzji kodu (uwaga 15): 60 juz w 171, przed testem.**
Dlaczego: log 17-44-34 (40 dob nowej kampanii BEZ 171) pokazuje, ze polki tarcz, lukow i helmow juz leza na dnie (minima 40 dob: tarcze 1 259 -> 236, luki 1 460 -> 324, helmy 737 -> 522; w dobie 40 tarcze 481, luki 376,
helmy 1 051) mimo zalewu dubli; po 171 popyt rekrutow t2+ "z tym co ma" (ok. 330-760 ludzi dziennie na starcie) oproznilby polki korpusu w kilka dob.
Dorobek stuleci 2 miesiecy pracy rzemieslnikow (14 dni = ok. 10.7 tys. szt. na 97 miast, 60 dni = ok. 46 tys.: korpus ok. 15 tys., bron 1H ok. 10 tys., helmy
ok. 3.5 tys., strzaly ok. 2.4 tys.) to 2-5 tygodni popytu t2+ na korpus i bron biala - czas, w ktorym ceny rosna i kowale przestawiaja sie na zbroje.
To prawdopodobny stan miasta ze stuletnia historia (zapas kupcow i warsztatow), nie dosypka: tylko nowa kampania, raz. Kampanii Jeffa nie dotyczy.
**Nie rozwiazuje tarcz, lukow i strzal** (cechy tarczownikow i lucznikow maja male udzialy: +0.4 tys. tarcz, +0.6 tys. lukow): tarcze robi sie 27-53 dziennie
wobec 150-350 popytu, strzal i beltow - 0. To paczka produkcji (rozdz. 16) - przed wgraniem 171 albo razem z nim (pytanie 9).
Pulapka MCM: jesli `ColdStartMarketDays` siedzi w `Armoury.json` Jeffa z wartoscia 14, nowa domyslna nie zadziala - sprawdzic przed testem (CLAUDE.md rozdz. 7).

---

## 8. Linie logu (raz na dobe; po polsku bez polskich znakow)

1. `RecruitKit.Day()` - zastepuje dzisiejsza linie "Komplet rekruta: dzien ...":
   ```
   Komplet rekruta: dzien 108837 - werbunek AI: z zapisu notabla 68, z tym co ma 412 (notabl bez zapisu 37, najemnik z karczmy 371, bez zrodla 4), jeniec bez niczego 12, tier 1 z wlasnym dobytkiem 2020; duplikat ROT pominiety 2975 (ten sam czlowiek, bez drugiego kompletu); do zbrojowni szt.: z zapisu 306, z tym co ma 118, konie i rzedy najemnikow z wzorca (160) 41; konni bez konia (do 167) 3; rzeczy ochotnikow, ktorzy odeszli, sprzedane 120 szt.
   ```
   Przy `RecruitBringsWhatHeHas` = false zamiast "z tym co ma ..." i "jeniec ...": `bez zapisu (wzorzec) N` (jak dzis).
   Po recenzji kodu: w nawiasie duplikatu ROT `; echo: brak Y wobec X N szt. u M ludzi - pan dokupuje` (uwaga 17), a na koncu `(na polke bez zaplaty - kasa
   miasta pusta N)` (uwaga 6).
2. Zaraz po niej:
   ```
   Komplet rekruta (tiery): dzien 108837 - z zapisu t2 40, t3 20, t4 8, t5 0, t6 0 | z tym co ma t2 300, t3 90, t4 20, t5 2, t6 0 | jeniec t1 5, t2 5, t3 2, t4 0, t5 0, t6 0 | duplikat ROT t1 2020, t2 700, t3 200, t4 50, t5 5, t6 0.
   ```
3. Zaraz po niej (RecruitKit):
   ```
   Pule ochotnikow (171): dzien 108837 - swiezi ochotnicy t2+ 270 (notabl kupil 60, z tym co ma 210; szt. 140); HouseLevies: komplet przeniesiony 640, bez zapisu 12; autowerbunek zalog: ludzi 61 w 61 twierdzach (z zapisu 9, z tym co ma 4, tier 1 48), do zbrojowni zalog 236 szt. (w tym dobytek tieru 1 205); zalogi gracza poza systemem 1; potkniecia 0.
   ```
   (Autowerbunek: najwyzej 1 czlowiek na twierdze na dobe - liczba ludzi nigdy nie przekracza liczby twierdz z zaloga, ok. 227; krytyka 6.)
   Po recenzji kodu w nawiasie "do zbrojowni zalog": `; kupione przez notabla oplacone przez pana N szt. za Z zl, bez zaplaty - na targ M` (uwaga 5).
4. "Ochotnicy: dzien ..." (VolunteerKit) - dopisek: `; w tym zamki BK: awanse 12, cofniete 30, swiezi t2+ 5`.
5. "Ludzie: dzien ..." (PeopleLedger) - dopisek po "gracz N": `, duplikaty ROT (ten sam czlowiek, nie liczeni) 2975`.
6. `GarrisonCarts.Daily()`:
   ```
   Zaopatrzenie zamkow (171): dzien 108837 - zamowien 41 w 33 miastach: 1210 szt. za 48200 zl (kiesy panow -> kasy miast; srednio 29.5 szt. na zamowienie, na limicie szt. 3, na limicie budzetu 6); z polki wlasnego zamku 260 szt. za 9100 zl; w drodze 88 zamowien (2650 szt.); dojechalo 37 (1105 szt., srednio 1.8 doby drogi); czeka: oblezenie 2, brak zalogi 0; zawrocone 1 (30 szt., zwrot 1250 zl; zamek wrogi 1, rod wymarly i zamek u obcych 0, 30 dob 0, bez odbiorcy zwrotu 0); bez zamowienia: bez miasta 1, bez drogi 2, za daleko 0, przerwa (co 3 doby) 78; brak towaru w miescie: 96 koszykow (zamowienia dla warsztatow); potkniecia 0.
   ```
7. `ArmsDrill.Daily()`:
   ```
   Cwiczenia (171): dzien 108837 - zalogi AI: twierdz z cwiczeniami 141, mnoznik srednio x0.84 (zero: 6, pelny: 101); partie rodow AI: 330, mnoznik srednio x0.77 (zero: 2, pelny: 198), doswiadczenia mniej przez brak broni ok. 96000; potkniecia 0.
   ```
8. `ArmsDrill.Daily()` (gdy `ArmsCoverageLog`; **pelny przeglad raz na 5 dob**, `today % 5 == 0` - krytyka 17):
   ```
   Pokrycie zbrojowni AI (171): dzien 108835 - partie rodow AI (41200 ludzi): korpus 88%, glowna bron 93%, helm 71%, tarcza 64%, nogi 90%, rece 55%; zalogi AI (45800 ludzi): korpus 80%, glowna bron 85%, helm 60%, tarcza 52%, nogi 81%, rece 40%; zamki z pokryciem glownej broni < 50%: 9 z 130; brakuje razem 61200 szt. (korpus 9100, glowna bron 4300); w drodze do zamkow 2650 szt.; czas przegladu 3 ms.
   ```
   Pokrycie typu = (potrzeba - brak po zastepstwie tierow, `AiGear.Deficit`) / potrzeba; "glowna bron" = `ArmedShare` wazone ludzmi.
   Po recenzji kodu (uwaga 13): "glowna bron (wedlug szczebla)" - `ArmedShare` liczy sztuki o tierze >= t-1 (D4); dochodza "strzaly", "belty" i blok
   `| przedmioty t3+: korpus X%, bron Y%` (koszyki przedmiotow tieru 3+ - sprzet wyzszych szczebli); "brakuje razem ... bron" = wszystkie sztuki broni wzorcow. Partie: `MobileParty.AllLordParties`
   z wodzem, bez rodu gracza i Innych; zalogi: twierdze z `Town.GarrisonParty`, bez gracza (chyba ze `GarrisonBuysGearPlayer`) i Innych.
   **Dlaczego co 5 dob:** codzienny przeglad ok. 650 partii i 227 zalog tylko dla logu dokladalby kosztu (Jeff: "nie dokladac kosztu"); trend w tescie 40 dob
   widac i przy 8 pomiarach. Czas przegladu (Stopwatch) w linii - pomiar do testu D.
9. Start sesji: `RecruitSources: ...` (A1), `VolunteerKit: ... zamki BK ...` (A4), `ArmsDrill: cwiczenia wlasna bronia - modele szkolenia N (wpiete), modele doswiadczenia zalog M (wpiete).`,
   `GarrisonArmory: komplet z ludzmi - zostawienie wpiete|BRAK, zabranie wpiete|BRAK, rozwiazanie partii wpiete|BRAK; patrole BK: typ znaleziony|brak.` (A7, C10),
   `Zbrojownie zalog (171): ...` (C9/C9a).
10. `GarrisonArmory.Daily()` (A7 i C10, liczniki doby):
   ```
   Zbrojownie zalog (171): dzien 108837 - komplet z ludzmi: lordowie zostawili w zalogach 340 ludzi (1210 szt.), zabrali z zalog 220 ludzi (760 szt.), rozwiazane partie do zalog 3 (1450 szt.), rozwiazane - ludzie odeszli 2 (komplety z ludzmi 300 szt., tabor sprzedany 120 szt. za 5400 zl); nadwyzki zalog: sprzedalo 31 z 32 zalog w kolejce, 640 szt. za 21000 zl (kasy osad -> panowie; w tym ludzie na patrolach BK policzeni 2 zalogi); partie lordow bez wodza albo rozwiazywane - zbrojownia zachowana 12; potkniecia 0.
   ```
   Po recenzji kodu: `partie lordow bez wodza albo rozwiazywane - zbrojownia zachowana N` (uwaga 1); w linii zapisu/wczytania `partie lordow bez wodza albo
   rozwiazywane N, M szt.`; w linii 6 "zawrocone" - `zamek u obcych po pokoju N` (uwaga 4).

Kazda nowa klasa: `catch` per obiekt -> licznik potkniec w swojej linii + `Log.Error` najwyzej raz na dobe (nie gasimy funkcji - CLAUDE.md rozdz. 7).

---

## 9. Wylaczniki MCM (Settings.cs - jeden zwarty blok na koncu pol, przed `public static void Load`; potem `python tools/gen_mcm.py`)

```
        // --- Arming the garrisons ---
        public bool RotSwapSameMan = true;                 // when Realm of Thrones turns a lord's new recruit into a man of the lord's house, it is still the same man: he keeps the kit he came with and no second kit appears from thin air (off = the swap is treated as a new man from nowhere: what he owns, or a full kit when Recruit Brings What He Has is off)
        public bool RecruitBringsWhatHeHas = true;         // an AI recruit whose gear nobody bought - a hireling from a tavern, a volunteer without a record, a man from the roads - brings only what he owns at home (the gear of his line's first rank that his troop also wears; a hireling also his own horse), a captive brings nothing; his lord buys the rest at a market (off = as before: a full kit of his troop from thin air)
        public bool FreshVolunteerKit = true;              // a volunteer who appears above the first rank (a noble youth) has his notable buy his key gear on the town market, as for a promotion; if the stall or the purse fails, he keeps what he owns and still waits in the pool (off = as before: no record, his kit is decided at recruitment)
        public bool HouseLeviesKeepKit = true;             // when House Levies makes a noble volunteer a man of the house that owns the land, the gear recorded for him goes with him (off = the record is lost and the notable sells the gear)
        public bool VolunteerKitCastles = true;            // castle volunteers (Banner Kings) rise to a better troop only with the gear their notable buys, like volunteers of towns and villages (off = as before: castle promotions cost nothing)
        public bool GarrisonRecruitKeepsKit = true;        // a volunteer the garrison takes in by auto-recruitment brings his gear into the garrison's armoury - what his notable bought for him, or his own belongings (off = as before: the garrison gets nothing and the notable sells his gear)
        public bool GarrisonGearFromTown = true;           // a castle's garrison buys what its men lack in the town its villages trade with: the lord of the castle pays that town, the goods leave its stalls and reach the castle by cart in the days the road takes; arms are no longer shipped to castles by traders and lords buy arms only in towns (off = as before: the garrison buys from the castle's own stall)
        public int GarrisonOrderDays = 3;                  // a castle sends an order for arms to its market town at most once in this many days; one order covers all those days (their pieces and their share of the lord's purse); what lies on the castle's own stall it buys every day
        public bool GarrisonArmorySurvivesSave = true;     // garrison armouries are kept in the save game: Dynamic Troop Equipment does not save them, so after every load all garrisons stood empty and their lords bought all their arms again
        public bool GarrisonArmoryRestoreOldSave = true;   // the first time a save made before this change is loaded, the AI garrisons get back the kit of their men that Dynamic Troop Equipment failed to save - once, instead of their lords buying it all again (off = they stand empty and their lords buy)
        public bool KitMovesWithMen = true;                // when a lord leaves men in a garrison or takes men from it, or a disbanded party joins a garrison, the men's arms go with them; men of a disbanded party who go home take their own kit and the spare is sold for their house (off = as before: men move without their arms)
        public bool GarrisonSellsSurplus = true;           // once a week a garrison sells the arms it holds beyond what its men wear (and a tenth spare) to the stall of its own town or castle, at the merchant's buying price; the coin goes to the lord of the place (AI garrisons; yours only with Garrison Buys Gear Player)
        public bool GarrisonDrillNeedsArms = true;         // a garrison's daily drill (training fields, drills) teaches only the men who have a weapon of their kind in the armoury - the experience is scaled by the share of armed men (AI garrisons; yours only with Garrison Buys Gear Player)
        public bool PartyDrillNeedsArms = true;            // an AI lord's daily training of his men needs weapons: the experience is scaled by the share of his men who have a weapon of their kind in the armoury - with arms short, promotions slow down
        public bool ArmsCoverageLog = true;                // every fifth day write a line to the log: how much of the arms and armour AI parties and garrisons need is in their armouries
```
Kazdy domyslnie wlaczony. Wylaczony = zachowanie sprzed 171 dla tej jednej rzeczy. Grupa MCM "Arming the garrisons".
Pulapka MCM (CLAUDE.md rozdz. 7): nowe klucze nie istnieja w `Armoury.json` Jeffa - obowiazuja wartosci z kodu; przed wgraniem sprawdzic (tylko odczyt), ze zaden z nich tam nie siedzi.

---

## 10. Zapis gry

| Klucz | Format | Wczytanie | Stary zapis (sprzed 171) | Cofniecie do c01a54ba |
|---|---|---|---|---|
| `arm_garrisoncarts` (NOWY, `SaveText.Sync`) | `zamek\|miasto\|rod\|frakcja\|dobaZam\|dobaPrzyj\|zaplacone\|przedmiot:modyfikator:ile:koszyk;...~` (w tabeli `\|` = kreska pionowa) (id = StringId; `frakcja` = `FactionAtOrder.StringId` - krolestwo albo rod; pusty modyfikator = puste pole) | `Import` -> `_pending`; `ResolvePending("wczytanie")` w `OnSessionLaunched` (Settlement/Clan/Kingdom/ItemObject/ItemModifier przez `MBObjectManager`; frakcja: najpierw `Kingdom`, potem `Clan`); zamowienia z brakujacym zamkiem/miastem - odrzucone z licznikiem w logu; brak frakcji - `FactionAtOrder` = obecna frakcja zamku; `_transit` przebudowane | brak klucza = brak zamowien | klucz ignorowany; oplacony towar w drodze przepada (zloto juz w kasach miast) - ryzyko 14.8 |
| `arm_garrisonarmory` (NOWY) | `v1\|` + `osada>przedmiot:ile,...~` albo `v1\|off` | `Import` -> `_loaded`, `_pending`; `Restore` w `OnSessionLaunched` (C9) | brak klucza (null albo bez znacznika v1) = stary zapis -> jednorazowe odtworzenie zalog AI (C9a, wylacznik `GarrisonArmoryRestoreOldSave`) | ignorowany - wraca dzisiejszy blad |
| `arm_recruitkit` (ISTNIEJACY, format rozszerzony) | jak dzis `notabl>oddzial>T\|->przedmioty~`; dobytek (`Own`) jako tokeny z przedrostkiem `+` w tej samej liscie: `+przedmiot:mod` | `ResolvePending`: token `+...` -> `Own`, reszta -> `Items`; przyjmowac 4 pola jak dzis | stare zapisy = bez dobytku (Own pusty) | stary parser: `+przedmiot` -> `GetObject` = null -> pominiety (dobytek ginie, kupione zostaja) - bez wywrotki |

- Blok w `ArmouryBehavior.SyncData`: zaraz po bloku `arm_mendstock` (na koncu metody), **kazdy klucz we wlasnym `try`** i `Export` TYLKO przy zapisie
  (krytyka 8 - przy wczytaniu `Export` przechodzilby `Settlement.All` i zbrojownie DTE w trakcie deserializacji, a jego wyjatek gubil po cichu `Import`):
  ```
  try { string gc = dataStore.IsSaving ? GarrisonCarts.Export() : null; SaveText.Sync(dataStore, "arm_garrisoncarts", ref gc); if (dataStore.IsLoading) GarrisonCarts.Import(gc); }
  catch (Exception e) { Log.Error("SyncData.arm_garrisoncarts", e); }
  try { string ga = dataStore.IsSaving ? GarrisonArmory.Export() : null; SaveText.Sync(dataStore, "arm_garrisonarmory", ref ga); if (dataStore.IsLoading) GarrisonArmory.Import(ga); }
  catch (Exception e) { Log.Error("SyncData.arm_garrisonarmory", e); }
  ```
  (`SaveText.Sync` przy wczytaniu nie czyta `value` - SaveText.cs: galaz `IsLoading` - wiec `null` jest bezpieczne; brak klucza zostawia `null`.)
  `Export` przy nierozwiazanym `_pending` najpierw `ResolvePending("zapis przed startem sesji")` (wzor RecruitKit.Export :212); dla `GarrisonArmory` to
  `Restore("zapis przed startem sesji")` (flaga `_restored`, zeby `Restore` z `OnSessionLaunched` nie dzialal drugi raz - C9a dodalaby wtedy braki ponownie,
  a galaz "z zapisu" zrobilaby `Clear()` i dolozyla to samo).
- `OnSessionLaunched` (ArmouryBehavior.cs:982), zaraz po `RecruitKit.ResolvePending`, kazde we wlasnym `try`:
  `GarrisonCarts.ResolvePending("wczytanie"); GarrisonArmory.Restore("wczytanie"); RecruitSources.ApplyLate();`
  (Restore przed `ColdStart.Run` z :985 - w wczytanej grze ColdStart i tak nic nie robi: `_done` albo wiek kampanii > 3 doby.)
- Konstruktor `ArmouryBehavior` (:389): dopisac na koncu listy `RecruitSources.Reset(); GarrisonCarts.Reset(); GarrisonArmory.Reset(); ArmsDrill.Reset();`.
- Nowa kampania: `ColdStart.Run` wypelnia zbrojownie zalog jak dzis; pierwszy zapis juz je trzyma (`_loaded` = false - Restore nic nie robi).

---

## 11. Pliki i metody - lista dla wykonawcy

NOWE pliki (csproj bierze `src\**\*.cs` sam - Armoury.csproj:24, nic nie dopisywac):
- `Armoury/src/RecruitSources.cs` - A1 (echo ROT), B2 (jency), A5 (autowerbunek zalog); `Reset`, `ApplyAll(Harmony)`, `ApplyLate()`, `IsRotEcho`, `InPrisonerRecruit`, `Stumble`.
- `Armoury/src/GarrisonCarts.cs` - C1-C8: zamowienia, dostawy, zapis, linia "Zaopatrzenie zamkow (171)".
- `Armoury/src/GarrisonArmory.cs` - C9: zapis i odtworzenie zbrojowni zalog; C9a: stary zapis; A7: latki Leave/Take/OnPartyDisbanded, `MoveKits`, `MoveAll`,
  `NeedByType`; C10: `SellSurplus`, `Daily()` (kolejka tygodniowa, patrole BK), linia "Zbrojownie zalog (171): dzien"; `Reset`, `ApplyAll(Harmony)` (flagi na proces).
- `Armoury/src/ArmsDrill.cs` - D2-D4 i linie "Cwiczenia (171)", "Pokrycie zbrojowni AI (171)"; `ApplyAll(Harmony)`, `Daily()`, `ArmedShare`.

ZMIENIANE:
| Plik | Co |
|---|---|
| `RecruitKit.cs` | `Kit.Own`; `Materialize`; `SellOff`/Reconcile tylko `Items`; `OnRecruited` (B2, nowa sygnatura); `OnUpgrade` (B4); NOWE `OnSwap`, `OnFresh`, `OnGarrisonTook`, `NoteEcho`, `OwnOf`, `Tier1Root`, `FreshOn`, `Stumble`; `SeedCampaignStart` (B5); `Day()` - linie 1-3; `Export`/`ResolvePending` (tokeny `+`); `Reset` czysci nowe mapy i liczniki; opis klasy (summary) uzupelnic o 171 |
| `VolunteerKit.cs` | `Postfix` (warunek `GameStarted`, A3, targ zamku `ArmyClothing.MarketTown`, liczniki zamkow A4); `Buy(..., bool countWhy = true)`; `BasicOf`; `ApplyAll` - latka BK (A4); `Prefix` - wylacznik zamkow; `Flush` - dopisek zamkow |
| `ColdStart.cs` | wydzielenie `internal static int FillToTemplate(MobileParty mp)` z petli `Armories()` (zachowanie 1:1) - dla C9a |
| `HouseLevies.cs` | `Convert` - wywolanie `RecruitKit.OnSwap` przed zamiana slotu (A2) |
| `AiGear.cs` | `RecruitKitPrefix` (nowa sygnatura, echo); `Deficit`, `BuyLoop`, `Deliver`; `TryBuy` - lordowie tylko w miastach, galaz zalogi zamku (C2) |
| `SupplyDemand.cs` | `DailyTrade` - zamek nie jest celem (C8) |
| `ArmyClothing.cs` | `MarketTown`: `private` -> `internal` (jedno slowo) |
| `MarketCarts.cs` | `PerDay(Settings)`: `private` -> `internal` (jedno slowo) |
| `PeopleLedger.cs` | echo ROT pomijane i liczone osobno (A1) |
| `ArmouryBehavior.cs` | konstruktor (Reset x4), `SyncData` (blok 171, dwa osobne `try`), `OnSessionLaunched` (3 wywolania), `OnDailyTick`: tuz PRZED `SupplyDemand.DailyTrade()` (:1260) trzy linie `try { GarrisonCarts.Daily(); } catch ...`, `try { GarrisonArmory.Daily(); } catch ...` i `try { ArmsDrill.Daily(); } catch ...` (zawrocony towar i nadwyzki zalog na polkach przed handlem kupcow - kupcy wywioza nadwyzke zamkow tego samego dnia) |
| `SubModuleMain.cs` | po `RecruitCost.ApplyAll(_harmony);`: `try { RecruitSources.ApplyAll(_harmony); } catch ...`, `try { GarrisonArmory.ApplyAll(_harmony); } catch ...` i `try { ArmsDrill.ApplyAll(_harmony); } catch ...` |
| `Settings.cs` | blok rozdz. 9; potem `python tools/gen_mcm.py` (McmSettings.cs generowany) |
| `CHANGELOG.md` | wpis na gorze wedlug wzoru (Problem z dowodem z logu, Przyczyna, Zmiana, Ryzyko / co sprawdzic, Status DO SPRAWDZENIA) |

---

## 12. Gracz - osobno (co sie zmienia, a co nie)

- **Jego werbunek:** bez zmian - komplet DTE jak dotad; RecruitKit tylko zdejmuje zapisy u notabla. ROT nie zamienia jego ludzi, wiec echo go nie dotyczy.
- **Jego zalogi:** autowerbunek z kompletem (A5), zamowienia zamku w miescie (C2), cwiczenia wedlug broni (D2) - TYLKO przy `GarrisonBuysGearPlayer` = true
  (domyslnie false, czyli jak dzis). **Wyjatek - naprawa bledu, dla niego tez:** zbrojownia jego zalogi przetrwa zapis gry (C9). Sprzedaz nadwyzek (C10) -
  tez tylko przy `GarrisonBuysGearPlayer`. Jednorazowe odtworzenie po starym zapisie (C9a) jego zalog nie obejmuje (jak ColdStart).
  Lordowie AI nie zostawiaja ani nie zabieraja ludzi z jego twierdz (gra, GarrisonTroopsCampaignBehavior.cs:249 - poza swiezo zdobyta twierdza), a ludzie,
  ktorych on sam zostawia na ekranie druzyny, przechodza jak dzis (A7 pkt 4). Rozwiazana partia jego rodu wchodzaca do twierdzy jego frakcji - jej sprzet
  idzie z ludzmi do zalogi (A7 pkt 2; dzis przepadal).
- **Jego druzyna:** cwiczenia bez zmian (BK i tak nie daje podstawowego doswiadczenia rodowi gracza; D3 omija rod gracza).
- **Partie towarzyszy jego rodu:** ida sciezka AI RecruitKit juz dzis (KitFromNotable) - dostana regule "z tym, co ma" i dozbroja sie z kiesy towarzysza (pytanie 3).
- **Co zobaczy na mapie:** zamki przestana miec bron na polkach (po splynieciu zapasu do miast); konie i rzedy kupcy woza do zamkow jak dotad (po recenzji
  kodu, uwaga 10); w NOWEJ kampanii przez pierwsze tygodnie przeciwnicy AI w jego bitwach czesciej w sprzecie chlopa na brakujacych miejscach i z kara morale (DTE);
  pule ochotnikow w miastach i wsiach takie same jak dzis (wariant lagodny A3).
- **Po recenzji kodu - co jeszcze zmienia jego gre (uwagi 11, 13-16, 18):**
  - **Pule zamkow BK** (A4, `VolunteerKitCastles`): ta sama regula co w miastach i wsiach - ochotnik zamku awansuje tylko ze sprzetem kupionym przez notabla;
    gdy notabl nie ma zlota albo miasto handlowe nie ma towaru, awans sie cofa. Gracz werbuje w zamkach BK z tych samych pul, wiec w nowej kampanii przy pustych
    polkach zobaczy tam mniej wyzszych tierow niz dzis (pytanie 7).
  - **Nowa kampania:** polki miast na starcie z zapasem 60 dni pracy rzemieslnikow zamiast 14 (bron i zbroje tansze i latwiej dostepne takze dla niego).
  - **Przeciwnicy AI awansuja wolniej, gdy brakuje broni na ich szczebel** (D3 wedlug szczebla) - w nowej kampanii mniej t4-t5 u AI przez pierwsze miesiace (pytanie 4).
  - **Lucznicy AI moga zostac bez strzal**, gdy strzaly na polkach sie skoncza (warsztaty ich nie robia) - w jego bitwach DTE da im sprzet chlopa bez kolczana (pytanie 9).
  - **Jego werbunek zostaje przy pelnym komplecie DTE** (najemnik i szlachcic t2+), a lord AI za tego samego czlowieka dostaje tylko dobytek i dokupuje reszte -
    nierownosc na korzysc gracza (pytanie 8). Bez zmian w kodzie, dopoki Jeff nie zdecyduje.

---

## 13. Testy (wykonawca NIE uruchamia gry - test robi sesja glowna z autotestem, zasady w CLAUDE.md / pamiec "zgoda na autotest")

Porownanie: log nowej kampanii 161 `Armoury-2026-10-08_11-40-05.log` (doby 108836-108839) i rok T3 `Armoury-2026-10-08_08-18-38.log`.

A. **Autotest 40 dob, nowa kampania** (probny DLL):
| Co | Musi byc |
|---|---|
| "Komplet rekruta": duplikat ROT pominiety | ok. tyle, ile dzis "bez osady" minus jency (start 2 000-3 000/dobe, dzien 40 kilkaset) |
| "Komplet rekruta": notabl bez zapisu | od doby 3: najwyzej 5% werbunku AI tieru 2+ od notabli (dzis ok. 85-95%) |
| "Komplet rekruta": "bez zapisu (wzorzec)" | nie wystepuje (0) przy wlaczonym wylaczniku |
| "Ludzie": zwerbowani | ok. o polowe mniej niz w 11-40-05 (bez duplikatow); "bez osady" = jency + ochotnicy z mapy (dziesiatki, nie tysiace) |
| Uzgodnienie kompletow 1. doby: "nadmiar wobec puli" | ponizej 300 (dzis 1 257) |
| "Pule ochotnikow (171)": HouseLevies przeniesione | rzad 300-900/dobe na starcie; "bez zapisu" mala czesc |
| "Pule ochotnikow (171)": autowerbunek zalog | > 0 w czasie wojny; ludzi na dobe <= liczba twierdz z zaloga (ok. 227); szt. tieru 1 do zalog na dobe - zapisac srednia z 40 dob (pozycja A5 w 7.2: oczekiwane 0.2-0.7 tys.; > 1.0 tys. = blad, sprawdzic podwojne wpiecie A1) |
| "Komplet rekruta" (dzien 1 nowej kampanii) | brak zakupow VolunteerKit przed startem gry: linia "Ochotnicy:" doby 0 bez awansow i swiezych t2+; ColdStart: "zapisy sprzed startu zamienione na dorobek" = 0 |
| "Zaopatrzenie zamkow (171)" | codziennie zamowienia > 0, "dojechalo" > 0 od doby 2-4, srednio 1-4 doby drogi; srednio szt. na zamowienie - zapisac (w pierwszych dobach blisko limitu 180 to znak, ze limit dalej ogranicza); "bez drogi" i "bez miasta" - lista zamkow do sprawdzenia, jesli > 5 |
| "Zbrojownie zalog (171): dzien" (A7, C10) | "zostawili w zalogach" i "zabrali z zalog" > 0 codziennie (lordowie wjezdzaja do twierdz); szt. na czlowieka 3-5 przy pelnych zbrojowniach; nadwyzki zalog: sprzedaz w ok. 1/7 zalog dziennie, szt. maleja z tygodnia na tydzien (zapas po ColdStart splywa, potem tylko awanse i zamiany) |
| "Sakiewka ludzi: nadwyzki sprzedane" lordow | nizej niz w 11-40-05 takze dlatego, ze sprzet zostawionych ludzi idzie z nimi do zalogi (A7) |
| "Przeplywy osad (kasy zamkow)": zakupy sprzetu AI | spada do ok. 0 po splynieciu zapasu zamkow (zamek nie jest targiem); "Przeplywy osad (kasy miast)" - zakupy sprzetu rosna o zloto zamowien |
| "PodazPopyt: kupcy wywiezli" | bez transakcji DO zamkow |
| "Sakiewka ludzi: nadwyzki sprzedane" | wyraznie mniej niz 1.1-5 tys./dobe (koniec drugiego kompletu) |
| "Rynek broni": polki | nie rosna jak w T3 (+1.2 tys./dobe w 1. miesiacu); zaden typ sposrod BodyArmor, OneHandedWeapon, Polearm, Bow, Shield nie spada do 0 w skali swiata |
| "Pokrycie zbrojowni AI (171)", doba 40 (linia co 5 dob) | po recenzji kodu: "glowna bron (wedlug szczebla)" partii rodow >= 70%, korpus >= 60%, tarcza >= 50%, strzaly i belty >= 70%, "przedmioty t3+: korpus" >= 50%, "bron" >= 60% (ponizej - raport dla Jeffa przed "wgraj"); "zamki z pokryciem glownej broni < 50%" najwyzej 15% zamkow; trend z doby 20 do 40 nie spadajacy |
| "Rynek broni: na polkach" (po recenzji kodu, uwaga 15) | minima swiata z 40 dob bez 171 (17-44-34): tarcze >= 236, luki >= 324, strzaly >= 819, helmy >= 522; nizej = 171 pogorszylo stan wyjsciowy - raport przed "wgraj" (start z `ColdStartMarketDays` 60 - w linii "ColdStart: zapas kupiecki" ok. 40-50 tys. szt. "60 dni pracy") |
| "Komplet rekruta": "echo: brak Y wobec X" (uwaga 17) | zapisac srednia dob 20-40; (brak Y wobec X + "z tym co ma" x 3.9) ponizej 2x "Warsztaty: wykonano" tych dob; wyzej - niedobor rosnie szybciej niz produkcja, raport przed "wgraj" |
| "Zbrojownie zalog (171): dzien" - rozwiazane partie (uwaga 1) | "rozwiazane partie do zalog N (M szt.)": M/N rzedu setek (cala zbrojownia partii), nie ~0; "zbrojownia zachowana" > 0 w dniach z rozwiazaniami |
| "Cwiczenia (171)" | mnozniki 0.5-1.0 (po recenzji - wedlug szczebla, wiec nizsze niz w pierwszej wersji); liczba "zero" mala; partie nadal awansuja (sklad tierow w "Zold konnych (160)"/"Ludzie" nie zamarza na tierze 1; dopuszczalne skupienie na t2-t3 przy braku sprzetu t4+) |
| "Pieniadz swiata (bilans)" | zmiana sumy bez nowej pozycji 171 (wszystkie przeplywy 171 sa przelewami) |
| Bledy | 0 linii `Error` z nowych klas; potkniecia 0 |

B. **Zapis i wczytanie** (najwazniejszy test 1.1): nowa kampania 5 dob -> zapis -> `-LoadSave` tego zapisu, 3 doby:
- linia `Zbrojownie zalog (171): po wczytaniu przywrocone ...` z liczba zalog ok. tyle, ile twierdz z zaloga, szt. ok. tyle, ile w linii zapisu;
- "ZakupyAI: ... w tym garnizony" w dobach po wczytaniu najwyzej 2x doby przed zapisem (dzis po wczytaniu x6-x14);
- "Zaopatrzenie zamkow (171)": zamowienia w drodze z zapisu dojezdzaja ("dojechalo" > 0 w dobie 1 po wczytaniu, gdy byly w drodze).
C. **Stary zapis** (`-LoadSave autotest-161-kawalki`, 5 dob): linia "stary zapis bez zbrojowni zalog - odtworzone po bledzie DTE N zalog AI, M szt." z N ok. liczby
   zalog AI i M ok. ludzi zalog AI x 4.45; "ZakupyAI: ... w tym garnizony" w dobach po wczytaniu w rzedzie roku ciaglego T3 (60-130 tys. zl/dobe), NIE 490-870 tys.
   jak w 10-54-00; "Bank: dzien" - kapital Banku bez spadku o setki tysiecy w 5 dob; komplety rekrutow wczytane jak w 161 (12 624 z 12 927); bez bledow.
   Drugi przebieg z `GarrisonArmoryRestoreOldSave` = false: linia "odtworzenie wylaczone w MCM" i zakupy garnizonow jak dzis po wczytaniu (kontrola wylacznika).
D. **Wydajnosc** (`-Profile`, ten sam zapis co C): `AiGear.OnDailyTickParty` i dzienny tik Armoury nie wiecej niz +10% wobec pomiaru 08.10; "czas przegladu"
   w linii "Pokrycie" ponizej 10 ms.

---

## 14. Ryzyka / co sprawdzic (kontrola wedlug zasady 0 CLAUDE.md)

1. **Niedobor sprzetu w nowej kampanii** (rozdz. 7.2-7.3): spodziewany i opisany; miara - linia "Pokrycie". Jesli Jeff zacznie nowa kampanie przed decyzja z 7.4,
   pierwsze tygodnie bedzie widzial gorzej uzbrojonych przeciwnikow.
2. **Mniej zlota z zakupow sprzetu do kas miast**, gdy polki puste (dzis 115-190 tys./dobe) - wplywa na renty miast w projekcie ekonomii; patrzec w "Przeplywy osad".
   Za to po naprawie 1.1 panowie nie traca kilku mln po kazdym wczytaniu (mniej bankructw w grze Jeffa).
3. **Regresja AiGear (refaktor C2):** BuyLoop/Deficit musza dawac 1:1 to samo dla lordow i zalog miast - recenzja porownuje stary i nowy kod linia w linie;
   w tescie ZakupyAI lordow (bez garnizonow) w tym samym rzedzie co w 11-40-05.
4. **Kolizje latek:** `TickAutoRecruitmentGarrisonChange` (RecruitCost prefiks/finalizer + nasz prefiks/postfiks - niezalezne); `OnTroopRecruited` DTE (tylko nasz prefiks);
   `UpdateVolunteers` BK (tylko my); `CalculateGarrisonXpBonusMultiplier` (BK postfiks + nasz, mnozenie przemienne); `GetEffectiveDailyExperience` (NavalDLC przekazuje do BK -
   licznik zagniezdzenia); `DailyTrade` (jedna linia); `PeopleLedger` (paczka 169 moze dopisywac do tej samej linii - scalac recznie);
   `LeaveTroopsToGarrison`, `TakeTroopsFromGarrison`, `OnPartyDisbanded` (A7) - grep w dekompilacjach BK, ROT, BEE i wszystkich modow z `fa2fd7a6...\scratchpad\dekmods`
   (DTE, Diplomacy, NavalDLC, BKROTPatch i in.): nikt ich nie lata; NavalDLC ma tylko wlasnego sluchacza zdarzenia `OnPartyDisbandedEvent` (statki,
   NavalShipDistributionCampaignBehavior.cs:22, :40) - niezalezny. Nasze latki nie blokuja metody gry (prefiks bez `return false`).
5. **Martwy kod ozywa / zywy martwieje:** wzorzec "bez zapisu" przestaje dzialac (zamierzone); `KeepGarrisonArmory` zostaje (chroni w trakcie gry); zakupy lordow w zamkach
   znikaja (zamierzone); `VolunteerKit` zaczyna pracowac w zamkach BK (ok. 130 notabli - kilka zakupow dziennie, koszt pomijalny). Ile pracy naraz po wlaczeniu na
   trwajacej kampanii (poprawione po krytyce 7 i 13): **pierwsze wczytanie zapisu sprzed 171 to dokladnie sytuacja bledu 1.1** - bez C9a zalogi stalyby puste i panowie
   wydawaliby 0.5-0.87 mln zl dziennie przez tygodnie (log 10-54-00: 12 dob, ok. 7.8 mln zl, Bank 700 -> 70 tys.). Z C9a: jedno przejscie zalog przy wczytaniu
   (ok. 227 zalog, `FillToTemplate`), potem zakupy zalog jak w roku ciaglym. Sprzedaz nadwyzek (C10) zaczyna sie od nastepnej doby - ok. 1/7 zalog dziennie,
   pierwszy tydzien wiecej sztuk (zapas z awansow i zamian ROT), potem malo. Nowe latki A7 dzialaja przy kazdym wjezdzie lorda do twierdzy wlasnej frakcji -
   migawka rosteru 10-30 pozycji, tylko gdy gra naprawde przenosi ludzi.
6. **Cudzy kod:** ROT moze zmienic nazwe `ExchangeClanTroops` - wtedy log startu "BRAK" i echo wraca jako "notabl bez zapisu"/"bez zrodla" (dobytek, nie wzorzec - nic z niczego);
   DTE moze kiedys zaczac zapisywac zalogi - `Restore` zastepuje wpis zapisem (te same dane), bez dubla. Lup z przegranych (1.4): sprawdzone, ze DTE kopiuje
   zbrojownie przegranych, a gra zaraz potem niszczy pusta partie przegranych (takze zaloge) - przeniesienie, nie dubel; gdyby inny mod zostawial przegrana
   zaloge przy zyciu, dubel by powstal - miara: linia "Pokrycie" (zalogi z pokryciem > 150%) i nadwyzki C10 po bitwach przy twierdzach.
   Gra moze zmienic nazwy prywatnych metod `LeaveTroopsToGarrison`, `TakeTroopsFromGarrison`, `OnPartyDisbanded` - log startu "BRAK" (A7 nie dziala, reszta tak).
7. **Zamki bez drogi:** wyspy bez miasta na tej samej wyspie i bez portu - "bez drogi", zaloga zyje z polki zamku i dobytku rekrutow; lista z logu do decyzji.
8. **Cofniecie do c01a54ba:** zamowienia w drodze przepadaja (oplacone - zloto juz w kasach miast; ok. kilkadziesiat tys. zl), zbrojownie zalog wracaja do bledu 1.1,
   dobytek w kompletach rekrutow ginie (kupione zostaja). Zapis zrobiony z 171 i wczytany po cofnieciu: klucze 171 ignorowane; ponowne wgranie 171 na taki
   zapis (zapisany juz po cofnieciu, bez klucza) uruchomi C9a jeszcze raz - to znowu naprawa tego samego bledu, nie dubel (zbrojownie byly wtedy puste).
9. **Zapis:** +0.2-1 MB (zbrojownie zalog) - przez SaveText w kawalkach, bez napisu > 32 KB.
10. **Gracz:** pytania 1-4; zalogi gracza poza systemem przy domyslnym `GarrisonBuysGearPlayer` = false (z wyjatkiem naprawy C9).
11. **Szkolenie AI wolniejsze** (D3): armie AI moga miec mniej wysokich tierow niz dzis - to swiadomy skutek "bez broni nie ma musztry"; wylacznik `PartyDrillNeedsArms`.
12. **Liczby bilansu** to rzad wielkosci (+-30%); zmierzy je autotest (linie 1-10).
13. **C9a to przyblizenie z gory:** stan zbrojowni zalog sprzed zapisu jest nieznany (DTE go nie zapisal); wzorzec ich ludzi to gorna granica - w kampanii T3
    do ok. 0.35 mln szt. jednorazowo do zbrojowni (nie na targ). Gdyby zalogi przed zapisem byly niedozbrojone, C9a daje im wiecej, niz mialy. Swiadomie: to
    mniejsze zlo niz 7.8 mln zl zakupow i upadek Banku; nadwyzek nie bedzie (wzorzec = potrzeba, C10 nic nie sprzeda).
14. **A7 - proporcja:** gdy zbrojownia dawcy ma braki, przeniesieni ludzie biora udzial proporcjonalny, nie swoj pelny komplet - przyblizenie (DTE nie wie,
    ktory czlowiek ma co). Konie ida z jezdzcami (160), wiec zaloga moze trzymac konie - AiGear ich nie kupuje i C10 ich nie sprzedaje (jak MenPurse).
15. **Patrole BK** (A7/C10): BK zabiera na patrol do polowy zdrowych ludzi zalogi bez sprzetu (sprzet zostaje w zamku), a w Twoich bitwach z patrolem DTE da
    im sprzet awaryjny. C10 liczy ich potrzebe do zalogi, wiec nie sprzedaje ich rzeczy. Sprzet patroli - poza 171 (BK).
16. **Rozwiazana partia, ludzie odchodza** (A7 pkt 2): ich komplety znikaja razem z nimi (tak jak sami ludzie - pozycja dla 108); sprzedaje sie tylko tabor ponad
    komplety. Zadna sztuka nie powstaje i nie trafia na targ za darmo.
17. **(recenzja kodu) Zbrojownie partii lordow bez wodza** zyja do zniszczenia partii (DTE `OnMobilePartyDestroyed`) i sa w zapisie (rekord `@partia`); gdyby
    jakis mod trzymal partie bez wodza latami, jej zbrojownia lezy tyle samo (pamiec: kilkadziesiat partii, nie tysiace) - miara: licznik "zbrojownia zachowana".
18. **(recenzja kodu) Zapis zuzycia zalog** (AiWear): zalogi kupuja tanio sztuki obite (najlepsza skutecznosc do ceny) i ich nie naprawiaja (naprawy tylko u lordow
    w miescie) - obite czekaja w zalodze, az lord je zabierze z ludzmi (naprawi w miescie) albo C10 sprzeda je jako obite. Klucz `arm_aiwear` rosnie o wpisy zalog
    (szacunek do ok. 0.3 MB, SaveText w kawalkach). Lup zalog po obronie i zuzycie walki zalog - jak dotad (tylko lordowie), poza 171.
19. **(recenzja kodu) Pan zalogi placi notablowi** za rzeczy kupione ochotnikowi (A5) - przelew; czego nie oplaci, notabl sprzedaje na targu. Zalogi gracza tylko
    przy `GarrisonBuysGearPlayer` (wtedy placi gracz).
20. **(recenzja kodu) `ColdStartMarketDays` 60** - tylko nowa kampania; jesli klucz jest w `Armoury.json` Jeffa, obowiazuje stara wartosc (pulapka MCM).

---

## 15. Pytania do Jeffa (tylko to, co zmienia Twoja gre; praca nie czeka na odpowiedz - domyslne wartosci jak w rekomendacji)

1. **Twoje zalogi w tym systemie?** Dzis Twoje zalogi same nie kupuja broni (`GarrisonBuysGearPlayer` wylaczone) i 171 tego nie zmienia: autowerbunek z kompletem,
   zamowienia zamku w miescie i cwiczenia wedlug broni wlacza sie razem z tym przelacznikiem (zakupy z Twojej kiesy). Naprawa zapisu zbrojowni dziala u Ciebie zawsze.
   Rekomendacja: zostawic wylaczone, chyba ze chcesz, zeby Twoje zamki same sie zbroily.
2. **Szlachetny mlodzian bez sprzetu:** gdy notabl nie ma za co albo nie ma czego kupic, ochotnik szlachecki zostaje w puli "z tym, co ma" (wariant lagodny - pule bez zmian,
   takze dla Ciebie). Wariant scisly: zostaje zwyklym rekrutem tieru 1 (mniej szlachty do werbunku, zwlaszcza na poczatku kampanii, takze dla Ciebie).
   Rekomendacja: lagodny.
3. **Partie Twoich towarzyszy:** werbuja jak lordowie AI - najemnik i ochotnik bez zapisu przyjda do nich "z tym, co maja", a towarzysz dokupi reszte ze swojej kiesy.
   Rekomendacja: tak (tak dzialaja juz od wpisu 92).
4. **Wolniejsze awanse AI, gdy brakuje broni** (`PartyDrillNeedsArms`): w nowej kampanii przez pierwsze miesiace przeciwnicy beda mieli mniej wysokich tierow.
   Po recenzji kodu liczy sie bron na szczebel czlowieka (wlocznia rekruta nie wystarcza weteranowi) i hamuje tylko oddzial, ktoremu brakuje - hamulec dziala
   naprawde, wiec skutek bedzie wyrazniejszy niz w pierwszej wersji (AI skupi sie na t2-t3, dopoki nie ma sprzetu t4+).
   Rekomendacja: wlaczone.
5. (Na pozniej, nie w 171) Czy Twoja wlasna druzyna tez ma cwiczyc tylko tyle, ile ma broni w Twojej zbrojowni?
6. **Twoja obecna kampania przy pierwszym wczytaniu po wgraniu 171** (C9a, `GarrisonArmoryRestoreOldSave`): zalogi AI dostana raz z powrotem sprzet, ktory
   DTE zgubil przy zapisie (do wzorca ich ludzi). Skutek dla Ciebie: obroncy zamkow AI w Twoich oblezeniach od razu w pelnym sprzecie, a panowie AI nie wydaja
   ok. 7.8 mln zl w dwa tygodnie (bez fali bankructw i upadku Banku jak w logu 10-54-00). Alternatywa (wylaczone): zalogi puste, panowie kupuja od nowa.
   Rekomendacja: wlaczone. Twoich wlasnych zalog to nie dotyczy.
7. **(po recenzji kodu) Ochotnicy w zamkach BK** (`VolunteerKitCastles`): ta sama regula co w miastach i wsiach - awans tylko ze sprzetem kupionym przez notabla.
   Gdy notabl nie ma zlota albo miasto handlowe zamku nie ma towaru, ochotnik zostaje na nizszym szczeblu - w nowej kampanii przy pustych polkach zobaczysz
   w zamkach BK mniej wyzszych tierow do werbunku. Rekomendacja: wlaczone (jedna regula dla wszystkich pul; inaczej zamki BK bylyby jedynym miejscem, gdzie
   awans i sprzet powstaja z niczego).
8. **(po recenzji kodu) Twoj werbunek najemnikow i szlachty t2+:** dzis dostajesz pelny komplet z niczego (DTE), a lord AI za tego samego czlowieka tylko jego
   dobytek i dokupuje reszte. Po zwolnieniu ludzi komplet zostaje u kwatermistrza jako nadwyzka do sprzedania. Czy Twoj rekrut tez ma przychodzic "z tym, co ma"
   (dokupujesz reszte)? Rekomendacja: tak - ta sama regula co dla AI (osobna mala paczka); minimum: komplet z Twojego werbunku nie liczy sie jako nadwyzka
   do wyjecia u kwatermistrza. Do decyzji 171 tego nie zmienia.
9. **(po recenzji kodu) Strzaly i belty:** zaden warsztat ich nie robi. Po 171 lucznik AI t2+ przychodzi zwykle bez lukow i strzal, a pan kupuje je z polek -
   w Twojej kampanii strzaly skoncza sie szybciej niz w ok. 50 dob, a potem lucznicy AI w Twoich bitwach beda bez kolczana. Rekomendacja: wgrac 171 razem
   z produkcja strzal i beltow (luczarze i grotnicy w paczce produkcji) albo po niej; do tego czasu 171 tylko w tescie.

---

## 16. Pozycje dla innych paczek (zapisane, nie ruszane w 171)

- **164a (szczelnosc pieniadza):** (a) oplata za awans w grze w nicosc - `PartyUpgraderCampaignBehavior` :139-150, takze zalogi (:54-60, 168-186); w projekcie juz jest
  ("awanse -> sakiewka partii"), dopisac zalogi -> kasa ich osady; (b) NOWE: ROT przy zamianie oddzialu na inny tier - roznica ceny werbunku `GiveGoldAction(null, owner, ...)`
  (ROTTroopRecruiter.cs:205-212) - zloto z/w nicosc; (c) NOWE: autowerbunek zalogi placi w nicosc - `Clan.AutoRecruitmentExpenses` (GarrisonRecruitmentCampaignBehavior.cs:91)
  zdejmowane co dobe 1/5 w `DefaultClanFinanceModel.AddExpensesForAutoRecruitment` (:800-808) bez odbiorcy; powinno isc do notabla jak w LevyGold.
- **166 BUDZET RODU:** budzet sprzetu zalog wspolny dla wszystkich lenn pana (dzis 25% kiesy na kazda zaloge, C7).
- **167 WETERANI:** kon i rzad najemnika z wzorca (licznik w linii 1); dobytek najemnika z puli weteranow zamiast przeciecia wzorcow; NOWE: "konni bez konia"
  (oddzial konny wzorca, ktory przyszedl bez konia - przypadki 2, 6, 8; licznik w linii 1): MountedWage placi mu zold konnego (sprawdza `IsMounted` wzorca),
  a nikt nie kupuje mu konia - regula 160 "bez konia = pieszy" wymaga zoldu i skladu wedlug konia w zbrojowni, nie wzorca.
- **108 (ludzie jako jednostka):** ludzie bez zdarzenia i bez kompletu: przyrost podstawowy garnizonu (`TickGarrisonChangeForTown` - BasicTroop frakcji), elita BK w zalogach
  zamkow (`BKSettlementBehavior` ok. :723), BK +3-5 ludzi dziennie dla partii rodow bez krolestwa (`BKClanBehavior` :1058-1066). Sprzetu nie dostaja - kupuje pan (zgodne z Z3).
  NOWE: ludzie rozwiazanej partii, ktorzy nie wchodza do zalogi, znikaja ze swiata (obca twierdza - `MemberRoster.Clear`, DisbandPartyCampaignBehavior.cs:361;
  wies - tylko polowa staje sie milicja, :382-383; wies zlupiona albo osada null - wszyscy) - ich komplety znikaja z nimi (A7); powinni wrocic do ludnosci.
  Patrole BK (`GarrisonPartyComponent.CreateParty`) wychodza z zalogi bez sprzetu.
- **Produkcja (plan K13 / ruda):** warsztaty ograniczone ruda (rozdz. 7.4); po 171 to ruda, nie zalew polek, bedzie wyznaczac ilosc zbroi i broni.
  NOWE po recenzji kodu: (a) **strzaly i belty - 0 sztuk dziennie** w logach 17-44-34 (40 dob) i 11-32-39 (kampania Jeffa: 2 960 -> 2 900 strzal w dobe) -
  warsztaty (Forge/WorkshopLaw) ich nie robia; potrzebni luczarze i grotnicy (drewno + zelazo/ruda), przed wgraniem 171 albo razem z nim (pytanie 9);
  (b) **tarcze** 27-53 dziennie wobec 150-350 popytu po 171 - udzial cechu tarczownikow (0.05) za maly wobec potrzeb kompletow AI; (c) luki - jak tarcze.
- **164c (szczelnosc pieniadza):** do czasu przekierowania `AutoRecruitmentExpenses` do notabla - pan zalogi placi notablowi za rzeczy kupione ochotnikowi (171, recenzja kodu, uwaga 5).
- **Gracz - werbunek t2+ "z tym, co ma"** (pytanie 8) - osobna mala paczka po decyzji Jeffa.
- **Ekonomia - do zmierzenia:** dobytek tieru 1 (DTE, przyjety 05.10; od 171 takze przy autowerbunku zalog, A5) zostaje jedynym duzym doplywem sztuk do armii;
  ile z niego wraca na targ jako nadwyzka (MenPurse, C10) - osobny licznik w przyszlej paczce, jesli 169 tego nie pokaze.

---

## 17. Krytyka i odpowiedzi (08.10, noc)

Kazda uwage sprawdzilem sam w dekompilacji gry 1.4.8 (`ore-supply\cs`), DTE (`fa2fd7a6...\scratchpad\dte`), BK, ROT i w logach. Wynik: 15 przyjetych (w tym 2
czesciowo, z inna liczba albo inna droga), 1 przyjeta swiadomie jako zachowanie (6), 1 odrzucona (2).

| Nr | Waga | Uwaga (skrot) | Werdykt | Co zmienione (gdzie) / dlaczego |
|---|---|---|---|---|
| 1 | krytyczne | Rozwiazana partia wchodzi do zalogi, a jej sprzet znika | **PRZYJETE**, inna latka | Potwierdzone: `MergeDisbandPartyToFortification` :343-361, potem `OnMobilePartyDestroyed` DTE. Latka na `OnPartyDisbanded` (jedyne wejscie do obu scalen, takze wsi, obcej twierdzy i osady null) zamiast samej `MergeDisbandPartyToFortification`; zaloga dostaje cala zbrojownie; gdy ludzie odchodza, biora komplety, a tabor sprzedaje sie dla rodu zamiast "na polke miasta bez zaplaty" (towar za darmo dla miasta bylby przelewem bez platnika). Nowy wylacznik `KitMovesWithMen` zamiast `GarrisonRecruitKeepsKit` (inne zjawisko). A7 pkt 2, linia 10. |
| 2 | krytyczne | Przegrana zaloga zostaje, zwyciezcy dostaja kopie jej zbrojowni (dubel) | **ODRZUCONE** | Przegrana partia nigdy "nie stoi dalej": `CaptureDefeatedPartyMembers` (MapEvent.cs:1955-2032) bezwarunkowo zdejmuje z jej rosteru kazdego zwyklego czlowieka, a `HandleMapEventEndForPartyInternal` (MapEventSide.cs:437-444) niszczy ja zaraz po `OnMapEventEnded` (MapEvent.cs:2079 przed :2157), wiec DTE kasuje jej zbrojownie - lup jest przeniesieniem, nie dublem; opisane w 1.4 i 14.6. |
| 3 | wazne | Ludzie miedzy partia lorda a zaloga bez sprzetu; nadwyzki zalog bez ujscia | **PRZYJETE** | Potwierdzone: `LeaveTroopsToGarrison` :542-581, `TakeTroopsFromGarrison` :583-610 (tylko `AddToCounts`), `MenPurse.SellAiSurplus` tylko dla `IsLordParty` (:168), zamiany ROT w zalogach (:51-75). A7 pkt 1 i 3 (`MoveKits` - udzial proporcjonalny po typach, z konmi), C10 (tygodniowa sprzedaz nadwyzki zalog po cenie skupu, zloto do pana; zamek - na wlasna polke, z ktorej wywoza kupcy), Z2, Z9, linia 10. Patrole BK liczone do potrzeby zalogi. |
| 4 | wazne | Zamowienie co 3 doby z limitem 60 szt. i budzetem jednej doby - zamki uzbrajaja sie ok. 2 miesiace | **PRZYJETE**, inna formula budzetu | Limit sztuk x D (180); budzet `1 - (1-p)^D` kiesy ponad rezerwe (57.8% przy 25% i D = 3) zamiast "x D, najwyzej 75%" - dokladnie tyle, ile wydalyby D dziennych zakupow, nie wiecej. C2.4, C6, linia 6 (srednio szt. na zamowienie, na limicie), test 13A. |
| 5 | wazne | A3 kupuje przy tworzeniu kampanii (przed ColdStart), dubel z B5 | **PRZYJETE** | Potwierdzone: `OnNewGameCreatedPartialFollowUpEnd` (RecruitmentCampaignBehavior.cs:166-176) raz na osade, przed `OnSessionLaunched`; `GameStarted` = true dopiero w `RealTick` (Campaign.cs:894-896). Warunek `GameStarted` na poczatku calego `VolunteerKit.Postfix` (obie latki), opis B5 poprawiony ("raz na osade"), warunek bezpieczenstwa `SellOff` dla zapisu z `Items`. A3, B5, test 13A. |
| 6 | wazne | A5 tier 1 do zalogi = nowy doplyw z niczego, nieliczony; przyklad linii 3 niemozliwy | **PRZYJETE swiadomie** (wariant "przyjac") | Potwierdzone: najwyzej 1 autowerbunek na twierdze na dobe (`GetMaximumDailyAutoRecruitmentCount` = 1, ROT przekazuje, BK nie zmienia). Zostaje regula wpisu 92 (tier 1 = wlasny dobytek, jak przy werbunku do partii) - jedna regula dla jednego zjawiska; pozycja w 7.2 (+0.2-0.7 tys./dobe, gorna granica 1.0), liczba w linii 3 i w tescie 13A, przyklad linii 3 poprawiony. Bez pytania do Jeffa: to parametr ekonomii oparty na przyjetej regule, nie zmiana gry gracza. |
| 7 | wazne | Obecna kampania Jeffa: pierwsze wczytanie = blad 1.1 (7.8 mln zl, Bank 700 -> 70 tys.) | **PRZYJETE** | Liczby potwierdzone w logu 10-54-00 (12 dob: 834, 868, 742, 765, 719, 635, 662, 540, 538, 514, 496, 489 tys. zl; Bank 699 928 -> 70 352 w dobie 109207; dlug rodow 6.77 -> 7.53 mln). C9a: jednorazowe odtworzenie zalog AI algorytmem ColdStart (`FillToTemplate`), znacznik wersji "v1" na poczatku klucza w zapisie, wylacznik `GarrisonArmoryRestoreOldSave`, pytanie 6, test C, ryzyko 14.13, rozdz. 0. |
| 8 | drobne | `Export` przy wczytaniu w SyncData | **PRZYJETE** | `dataStore.IsSaving ? Export() : null`, kazdy klucz we wlasnym `try` (rozdz. 10). |
| 9 | drobne | Kolejnosc warunkow C4; rod wymarly a zamek u wroga | **PRZYJETE** | Limit 30 dob przed "czeka"; pole `FactionAtOrder` (takze w zapisie); rod wymarly i zamek innej frakcji niz przy zamowieniu - zawrot bez zwrotu (C3, C4, rozdz. 10, linia 6). |
| 10 | drobne | `private Line` w `internal Place` (CS0051) | **PRZYJETE** | `internal sealed class Line`; AiGear buduje `List<GarrisonCarts.Line>` w lambdzie (C2.4, C3). |
| 11 | drobne | Podwojne wpiecie latek po `Reset` | **PRZYJETE** | Flagi wpiecia statyczne na proces, nieczyszczone w `Reset`; `ApplyLate` tylko latka ROT i tylko gdy jej flaga = false; ta sama zasada w GarrisonArmory i ArmsDrill (A1). |
| 12 | drobne | Notabl zamku kupuje na `MarketOf` (linia prosta, bez wojny) | **PRZYJETE** | Dla zamkow `ArmyClothing.MarketTown(castle) ?? MarketOf(castle)`; `MarketOf` zostaje dla miast, wsi i wyceny konia 143 (A4). |
| 13 | drobne | "Przez kilka dob" zanizone | **PRZYJETE** | 0, 7.2, 14.5: "tygodnie (log 10-54-00: 12 dob, ok. 7.8 mln zl, Bank 700 -> 70 tys.)". |
| 14 | drobne | Falszywy opis MCM `RotSwapSameMan` | **PRZYJETE** | Opis: "off = the swap is treated as a new man from nowhere: what he owns, or a full kit when Recruit Brings What He Has is off" (rozdz. 9). |
| 15 | drobne | `onShelf` zamowien dla warsztatow na polce zamku zamiast miasta | **PRZYJETE** | W galezi zamku `onShelf` na `market.ItemRoster`, tylko w dzien zamowienia; bez miasta - nic (C2.4). |
| 16 | drobne | Konny bez konia (przypadki 6 i 8) placony jak konny | **PRZYJETE** (licznik) | Potwierdzone: MountedWage sprawdza `IsMounted` wzorca (MountedWage.cs:60). W 171 licznik "konni bez konia" w linii 1 (takze jency - przypadek 2) i pozycja dla 167; kon z wzorca odrzucony (kon z niczego, wbrew 160) (B6, rozdz. 16). |
| 17 | drobne | Linia "Pokrycie" - pelny przeglad co dobe tylko dla logu | **PRZYJETE** | Pelny przeglad raz na 5 dob, czas przegladu w linii, pomiar w tescie D (rozdz. 8 pkt 8, rozdz. 9 opis `ArmsCoverageLog`). |

---

## 18. Recenzja kodu (commit ebf0047, 08.10 noc) - werdykty i poprawki

Kazda uwage sprawdzilem w kodzie i dekompilacji (DTE `EveryoneCampaignBehavior`, gra `DisbandPartyCampaignBehavior`, ROT `ROTTroopRecruiter`) albo w logach
(17-44-34, 11-32-39). Wynik: 19 uwag (1 i 8 to ta sama dziura) - 17 przyjetych z poprawka w kodzie, 2 przyjete jako opis/pytanie bez zmiany kodu (11, 18),
0 odrzuconych. Build kod 0.

| Nr | Waga | Uwaga (skrot) | Werdykt | Co zmienione |
|---|---|---|---|---|
| 1, 8 | krytyczne | DTE `GarbageCollectParties` kasuje zbrojownie partii bez wodza / `IsDisbanding` w dobie czekania na rozwiazanie, wiec A7 pkt 2 przenosi 0 szt.; DTE ich tez nie zapisuje | **PRZYJETE** | Potwierdzone: GC (EveryoneCampaignBehavior :338-360) zostawia zbrojownie tylko partii z czynnym wodzem i `!IsDisbanding`; `OnPartyDisbandStarted` - doba czekania (`DaysFromNow(1)`) albo `ApplyDelayedTeleportToPartyAsPartyLeader`. `AiGear.KeepGarrisonArmory` chroni teraz kazda aktywna partie lorda (poza gracza) - z dzialajacym wodzem DTE i tak zwraca false, wiec zmiana dotyczy tylko partii bez wodza / rozwiazywanych / z nieczynnym wlascicielem; licznik "zbrojownia zachowana". `GarrisonArmory.Export/Restore`: rekord `@StringId` dla partii lordow, ktorych DTE nie zapisuje (warunek `ShouldPersistParty` przepisany 1:1 - `DteSaves`). |
| 2 | wazne | Zalogi pierza zuzycie (obita sztuka wychodzi z zalogi jako sprawna) | **PRZYJETE**, droga "zapis zuzycia dla zalog" | Zakup zalogi (`AiGear`), dostawa wozem (`GarrisonCarts`), autowerbunek (`RecruitKit.OnGarrisonTook`) - `AiWear.NoteBought` z modyfikatorem; przeniesienie ludzi (`MoveKits`, `MoveAll`) - `AiWear.MoveWorn`: udzial obitych proporcjonalny, nie mniej niz trzeba (u dawcy obitych <= sztuk) i nie wiecej niz n; C10 sprzedaje ze stanem z zapisu (`TakeCondition`, spis raz na sprzedaz). Przy okazji: sprzedaz przerwana brakiem zlota kasy oddaje stan sztuki do zapisu (dotad gubila jeden stan). Wariant "zaloga kupuje tylko sprawne" odrzucony - nie zamyka przeniesien lord -> zaloga. |
| 3 | drobne | Zamowienie zamku nieatomowe przy wyjatku w BuyLoop | **PRZYJETE** | `GarrisonCarts.Place` w `finally` (gdy sa linie); wydane = suma oplaconych linii. |
| 4 | drobne | Woz dojezdza do zamku, ktory po zdobyciu i pokoju nalezy do obcych | **PRZYJETE** | Zywy rod placacy: zamek innej frakcji niz przy zamowieniu i innej niz rod placacy - zawrot ze zwrotem; licznik "zamek u obcych po pokoju". |
| 5 | drobne | A5: notabl traci kupione rzeczy bez zaplaty | **PRZYJETE** | Pan zalogi placi notablowi cene skupu jego targu za kazda kupiona sztuke (tyle, ile notabl dostawal dotad ze sprzedazy); czego nie oplaci - notabl sprzedaje na targu. Liczniki w linii 3. |
| 6 | drobne | `SellOff` przerywa przy pustej kasie, reszta przepada | **PRZYJETE** | Niesprzedane sztuki ida na polke targu bez zaplaty (jak rzeczy zmarlego notabla); licznik "na polke bez zaplaty". |
| 7 | drobne | `MoveKits` nie liczy ludzi zalogi na patrolach BK | **PRZYJETE** | `TakePostfix` dolicza do potrzeby zostajacych potrzebe patroli BK osady; mapa patroli raz na dobe (wspolna z C10). |
| 9 | wazne | `ArmsDrill.Gated` nie sprawdza zakupow AI | **PRZYJETE** | Zaloga: `AiGear.On && GarrisonBuysGear`; partia: `AiGear.On`; opisy MCM uzupelnione. |
| 10 | drobne | C8 wycina z wywozu do zamkow takze konie i rzedy | **PRZYJETE** (wariant kodu) | C8 tylko dla broni i zbroi - konie i rzedy kupcy woza do zamkow jak dotad (gracz bez zmian); opis MCM `GarrisonGearFromTown`. |
| 11 | drobne | A4 zmienia pule zamkow BK, a rozdz. 12 mowi "pule bez zmian" | **PRZYJETE** (opis) | Rozdz. 12 i pytanie 7; kod bez zmian (wylacznik `VolunteerKitCastles`). |
| 12 | drobne | `MarketOfNotable` dla notabla zamku - linia prosta zamiast miasta handlowego | **PRZYJETE** | Zamek: `ArmyClothing.MarketTown` (bez miast wroga), inaczej `MarketOf` jak dotad. |
| 13 | wazne | Brama testu "glowna bron >= 70%" nie wykrywa braku na szczeblu; brak strzal i beltow w linii | **PRZYJETE** | "glowna bron (wedlug szczebla)" (D4 po zmianie), "strzaly", "belty", blok "przedmioty t3+: korpus, bron"; progi 13A. Odchylenie: wiersz t3+ liczony wedlug tieru PRZEDMIOTOW (koszyki Deficit), nie ludzi - tanio i bez drugiego przebiegu Deficit. |
| 14 | wazne | Hamulec awansow D3 liczy kazda sztuke typu (wlocznia t1 uzbraja t5) | **PRZYJETE**, droga kodu | `ArmedShare` wedlug szczebla (sztuka typu o tierze >= t-1, przydzial od najwyzszego szczebla); w D3 udzial oddzialu (element rosteru), nie calej druzyny. Pamiec na dobe, koszt bez zmian. |
| 15 | wazne | Nowa kampania: tarcze, luki, strzaly, helmy na dnie; plan B (60 dni) decydowac przed testem | **PRZYJETE** | `ColdStartMarketDays` 14 -> 60 juz teraz (7.4, uzasadnienie tam); minima polek swiata w 13A (zmierzone w 17-44-34: tarcze 236, luki 324, strzaly 819, helmy 522 - recenzja podala 265 i 971); tarcze/luki/strzaly - paczka produkcji (rozdz. 16). |
| 16 | wazne | Strzaly i belty nie powstaja wcale; 171 przyspiesza ich znikanie | **PRZYJETE** (czesciowo w 171) | Potwierdzone: 0 w "Warsztaty" w obu logach, 2 960 -> 2 900 strzal w kampanii Jeffa. W 171: strzaly i belty w "Pokryciu", prog w 13A (alarm - linia "Rynek broni" juz podaje stan swiata codziennie, bez nowego przegladu). Produkcja - rozdz. 16, pytanie 9 (wgranie 171 razem z produkcja albo po niej). |
| 17 | wazne | Popyt z zamiany ROT X -> Y niemierzony | **PRZYJETE** | Prefiks `ExchangeClanTroops` zapamietuje X (stos); `RecruitKit.NoteEcho` liczy sztuki wzorca Y niepokryte wzorcem X (koszyki i zastepstwo jak Deficit, pamiec na pare); linia 1 "echo: brak Y wobec X N szt. u M ludzi"; prog w 13A. |
| 18 | wazne | Werbunek gracza (pelny komplet DTE) wobec AI ("z tym, co ma") - nierownosc | **PRZYJETE** (pytanie) | Pytanie 8 z rekomendacja; rozdz. 12 i 16. Kod gracza bez zmian (CLAUDE.md: gracz osobno, bez zmian bez potrzeby). |
| 19 | drobne | Zamki zamawiaja wszystkie tego samego dnia | **PRZYJETE** | Brak stempla = zamowienie w dobie `dzien % D == Id % D` (rozlozenie bez zapisu i bez kosztu). |
