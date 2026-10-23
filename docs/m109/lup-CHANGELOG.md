## 2026-10-23 (m109-lup: konie, uprzaz i zbroja powalonych jezdzcow w lupie gracza - Jeff 22.10 pkt 8)

**Mod:** Armoury | **Galaz:** `w-toku/m109-lup` (od `44c03a05` = WGRANIE 103.5) | **Tor:** M (jedna galaz, recenzja, biegi autotestu - gry nie uruchamialem, Jeff gra)
**Commity:** `d1b3ecb3` (klucze + gen_mcm), `66e8b042` (pomiar LootTally, linie, hak testu, linia doby), `04712090` (konie i uprzaz poza Spoils + HorseFate.TakeKill),
`63950021` (Z2), `2e4bec6b` (Z1 + kolejnosc pomiaru), `630eebd9` (pomiar padlych koni: poza lupem / brak), `f6df0aff` (grupa progow m109lup)
**Build:** Armoury 0 bledow (kod wyjscia 0), gen_mcm Armoury 2321 ustawien; `sprawdz_narzedzie.py tools/sprawdz_logi.py` bez uwag; `--grupa m109lup` bez logu: LUP-JS i LUP-JS2 OK
**Klucze (nowe, domyslnie TAK, obok Wreck Salvage Enabled; w Armoury.json Jeffa ich nie ma - wchodza domyslne z kodu):** `FallenRiderHorsesLooted`, `RiderBlowSparesHarness`, `ExtraBrokenArmorWrecks`
**Zapis:** bez zmian (stan statyczny, czyszczony w BattlefieldLaw.LReset - stare zapisy wczytuja sie jak dotad)
**Pliki:** NOWY `Armoury/src/LootTally.cs`; `BattlefieldLaw.cs`, `HorseFate.cs`, `ArmouryBehavior.cs` (1 linia doby), `Settings.cs`, `McmSettings.cs` (gen_mcm), `tools/sprawdz_logi.py` (grupa m109lup)

### Problem

Jeff 22.10 ok. 23:20: "pokonalem robber squires, bylo dwoch konnych - nie dostalem za nich koni ani ich pancerzy koni, ani zbroi". Log
`Armoury-2026-10-22_22-28-22.log`, bitwa 23:09-23:14 (banda 13 ludzi, gracz z luku): "powaleni przez partie gracza - Ty i towarzysze 13, zolnierze 0 -> dzialka
gracza 100%", "lup cialo po ciele - z cial powalonych przez Ciebie i towarzyszy 57 szt. w calosci", "ekran Spoils podmieniony na 41 pozycji prawdziwego lupu (w tym
10 wrakow przed przesianiem)". Log nie wymienial sztuk - nie dalo sie z niego powiedziec, co bylo na liscie.

### Przyczyna

**Kon i uprzaz (dowod z kodu, dekompilacja DTE v1.4.7 i Spoils 1.8.4):** DTE wklada konia i uprzaz z ciala jezdzca (sloty 10, 11) do lupu partii zabojcy;
BattlefieldLaw kladzie je na liste trofeow Spoils (AfterGenerateLoot). Spoils `LootCollectionBehavior.OnMapEventEnded` (:899-941) zaraz potem zdejmuje z listy
KAZDA sztuke `IsMountable || ItemType == HorseHarness` do `_horsesFromLoot` (50% kulawych) - oddaje je tylko przy przeszukaniu taboru (:2704), a tabor Spoils jest u nas
wylaczony na stale (`NoBaggageTrain`); `FinishCollection` zeruje je (:4172). Kazdy kon i kazda uprzaz z lupu bitwy gracza przepadaly - od zawsze, nie tylko u Jeffa.
**Uspiony blad HorseFate:** `ApplyMission` biegnie po DTE `HandlePartyItems` (postfiks OnEndMission), wiec kopia padlego konia jezdzca powalonego przez gracza lezy
juz w kolejce ekranu; `TakeOne(MainParty)` szukal jej tylko w zbrojowni wojska i zdejmowal tam konia gracza o tym samym id. Dotad niewidoczne (Spoils i tak polykal
konie) - po naprawie padly kon wyszedlby na liscie, wiec poprawka jest czescia zmiany.
**Zbroja jezdzca (hipotezy - potwierdzi nowa linia logu):** (a) czesc rozbita ciosem przychodzi jako wrak "Mangled" (u Jeffa 10 wrakow, prog 3% niczego nie odrzucil
- w logu brak linii "lup przesiany" i "wrakow ponizej progu", miara wrakow bez zmian); (b) DTE `extraBrokenArmor` (:332-369) - druga czesc zbroi wroga od tieru 3
(25/35/45/60%) znika bez sladu; (c) czesc zastepcza DTE (`MarkSlotAsTemporary`) - zbroja z niczego, DTE jej nie lupi; (d) strzal w piers wybieral ciezka uprzaz
(body_armor 55) zamiast kolczugi - wrakiem byla uprzaz.

### Zmiana

1. **Pomiar (LootTally, bez klucza - tylko czyta).** Prefiks i postfiks DTE `DynamicTroopMissionLogic.OnAgentRemoved`: dla wroga (nie bohatera) zabitego albo
   ogluszonego przez gracza, towarzysza albo zolnierza druzyny - roznica licznikow rekordow DTE (ItemsToRecover ofiary, LootedItems gracza) i przyczyna kazdej sztuki
   z ciala (lup / cios / pekniete / zastepcze / odrzut / inne). Kolejka lupu (dzialka, bagaz, send, padle, lista, sakwy, zwrot, przepadlo, zostaje) i lista trofeow
   Spoils (z kolejki, wraki, przesiane, Spoils wyjal, przetrzymane, wrocone, rezerwa, na liscie) wedlug rodzaju: kon, uprzaz, tulow, glowa, rece, nogi, peleryna,
   bron, pociski, tarcza, inne. Linie: `Lup bitwy (m109): start` (klucze, latki 5/5), `Lup bitwy (m109): bitwa N` po kazdej bitwie gracza (zamkniecie: lista trofeow
   Spoils, sakwy, zwrot do zbrojowni wojska, ekran gry albo nastepna bitwa "bez rozliczenia"), `Lup bitwy (m109): jezdzcy bitwy N` (pierwsze 3 bitwy sesji, pod flaga
   testu kazda; do 8 jezdzcow: kon, uprzaz, zbroje z przyczyna), `Lup bitew (m109): doba D` (tylko przy bitwach doby). Bledy liczone (pierwsze 3 i co 50).
2. **FallenRiderHorsesLooted.** AfterGenerateLoot (po przesianiu) przetrzymuje konie i uprzaz w `MountHold`; prefiks Spoils `EquipmentDamageModel.ApplyDamageToLoot`
   (Spoils wola go tylko w :944, zaraz po swojej petli) oddaje je na liste: kon ze znacznikiem "lucky" Spoils (zostaje taki, jak byl pod jezdzcem - bez losowej
   kulawizny), uprzaz ze zuzyciem Spoils jak kazda zbroja, wrak uprzezy zostaje wrakiem. Finalizer Spoils `OnMapEventEnded`: co nie wrocilo (wyjatek w Spoils) - do
   sakw (kon bez zmian, uprzaz ze stanem bojowym, komunikat EN "The horses and harness of the fallen are led to your baggage: N."). Dziala tylko przy obu latkach.
   Reszta rytualu Spoils bez zmian (godziny, podzial, "Leave" -> polka warowni przez SpoilsSeal.RemnantPostfix). "Send troops" tak samo.
   **HorseFate.TakeKill:** padly kon jezdzca powalonego przez gracza/towarzysza - najpierw z kolejki lupu, potem ze zbrojowni; przez zolnierza - najpierw zbrojownia,
   potem kolejka; kon, ktory wedlug pomiaru nie wszedl do lupu gracza - nic nie schodzi. Inni zabojcy i klucz NIE - `TakeOne` jak dotad.
3. **Z2 RiderBlowSparesHarness.** Prefiks DTE `ArmorSelector.GetRandomArmorByBodyPart`: lista zbroi bez uprzezy (kopia). Cios rozbija czesc zbroi czlowieka; ladry
   przychodza cale. Ta sama liczba sztuk.
4. **Z1 ExtraBrokenArmorWrecks.** Dokladnie jedna brakujaca czesc zbroi ze slotu 5-9 u wroga od tieru 3 powalonego przez druzyne gracza (po odjeciu lupu, odrzutu
   DropRate, zastepczych, czarnej listy i ciosu) = "dodatkowo peknieta" DTE -> wrak do lupu (`BattlefieldLaw.AddWreck`, bramki QueueWreck + znany stan wraka). Wiecej
   niz jedna brakujaca - "niejednoznaczne", bez wraka. Nie w bitwie morskiej. Czesci zastepczych (c) nie zmieniam - to rzeczy z niczego; pomiar je pokaze.
5. **Hak autotestu** (plik `Documents/Mount and Blade II Bannerlord/CrashScribe/armoury-lup.flag`, LogGate 1/2): prefiks `EncounterManager.StartPartyEncounter` -
   wrog bitwy testu (`autotest_bitwa_*`) w planie mocna dostaje 6 x `veteran_caravan_guard_battania` (zapasowo pierwszy wedlug StringId nie-bohater od tieru 3
   z koniem, uprzeza i zbroja) i ich zestaw do zbrojowni DTE bandy (AiGear.AddToArmory). Linia `Lup bitwy (m109): test - ...`. W grze Jeffa nic nie robi.

**Klucz NIE = zachowanie 1:1 jak 44c03a05:** bez HoldMounts (konie jak dotad zabiera Spoils - w linii "Spoils wyjal"), SpareHarness bez zmian listy, bez AddWreck,
HorseFate `TakeOne` jak dotad. Pomiar i linie logu dzialaja zawsze (tylko czytaja).

### Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0)

- **ShareQueue:** pisza AfterDtePartyItems, AfterCollectLoot, QueueSimShare i teraz HorseFate.TakeKill (zdjecie 1 sztuki padlego konia); czytaja/czyszcza
  AfterGenerateLoot, FlushShareToBaggage, DropShare, SharePending (+ MountHold), SimShareWaits, LReset. TakeKill biegnie w OnEndMission - po HandlePartyItems, przed
  MapEventEnded; w przegranej AfterDtePartyItems nic nie wklada, a padli jezdzcy sa po stronie zwycieskiej (inna galaz ApplyMission).
- **Wrecks:** dopisuja QueueWreck, Z1 AddWreck (ten sam limit 400), AppendWrecks; czyszcza DropShare (przegrana - wrak z Z1 przepada jak kazdy), AfterCollectLoot
  (symulacja), LReset. AddWreck tylko gdy modyfikator wraka Spoils znany (inaczej wrak bylby cala sztuka).
- **Zamknieta ekonomia:** kon i uprzaz to te same sztuki, ktore DTE zdjal z ciala (zbrojownia -> kolejka -> lista trofeow -> sakwy); `_horsesFromLoot` Spoils przy
  kluczu nie dostaje naszych sztuk (tabor i tak zamkniety) - bez drugiej kopii; konie z loterii symulacji ida tam jak dotad. Wrak Z1 to jedyna kopia sztuki, ktora
  DTE zdjal ze zbrojowni ofiary przy spawnie, a potem nigdzie nie dopisal (dotad znikala). Padly kon schodzi z dzialki, nie z koni gracza.
- **Rejestr "lucky" Spoils:** rejestracje koni zuzywa to samo wywolanie ApplyDamageToLoot; GenerateLootFromUnitList czysci rejestr na starcie (:518) - bez wycieku
  do nastepnej bitwy; rezerwa do sakw po rozbrojeniu (`_mountArmed = false`) - prefiks nie dubluje. Postfiks CrashScribe `Mends.GoodsUnlooted` zgodny (kon bez
  rl_looted, uprzaz to zbroja).
- **SpoilsSeal i musztra:** RemnantPostfix wiezie konie z "Leave" na polke warowni; DrillLeavePrefix nie bierze koni ani uprzezy; paser placi z kasy miasta.
- **Ksiegi:** HorseBook liczy zbrojownie i tabory, nie kolejke ani liste Spoils - gdy zbieranie przejdzie przez polnoc, "Konie (E6/0)" pokaze -N jednego dnia i +N
  nastepnego (dotad trwale -N). ArmsBook przy Z1 zobaczy wraki, ktore wczesniej znikaly.
- **Odczuwalne dla gracza:** wiecej koni na liscie trofeow (waga, kara stada - gracz wybiera na ekranie); konie bez losowej kulawizny Spoils (losowanie z niczego) -
  swiadoma decyzja; Z2 przesuwa wraki z ladrow na zbroje jezdzca przy tej samej liczbie sztuk (dotyczy tez wlasnych poleglych jezdzcow - jedna zasada); HorseFate nie
  zdejmuje juz graczowi jego konia za padlego konia wroga.
- **Pomiar:** prefiks/postfiks OnAgentRemoved z wczesnymi wyjsciami, najwyzej 22 odczyty slownika na powalonego, w try z licznikiem; bez latek na malych metodach
  (AddItemToRecover zastapiony roznica licznikow - pamiec o zawieszeniu GC). Wlasny odczyt ciosu (`_pick`) - bez zaleznosci od kolejnosci z QueueWreck. Wpis do
  kolejki liczony przed dopisaniem (otwarcie bitwy liczy zawartosc kolejki jako "wczesniej").
- **Zmiany Spoils albo DTE:** brak latki -> "BRAK" w linii startu; HoldMounts tylko przy obu latkach (ApplyDamageToLoot + finalizer) - bez nich stare zachowanie,
  bez strat i bez dubli.
- **Hak testu:** w bitwach 1 i 4 biegu bitwa wrog ma +6 ludzi (`_enemyMen` w linii CrashScribe dalej 30/40) - grupy porownujace liczby wrogow moga sie roznic;
  dlatego osobny bieg bitwa-lup, zwykly bieg bitwa zostaje odniesieniem.
- **Scalanie:** Settings.cs - klucze obok WreckSalvageEnabled; ArmouryBehavior.cs - 1 linia przy HorseFate.Daily; sprawdz_logi.py - pomoc (przed na7) i dispatch
  (po m107gosp) - konflikt z rownoleglymi galeziami mozliwy (scal.sh); AutotestBattleKit.cs i SubModuleMain.cs nietkniete.

**Odstepstwa od projektu (z uzasadnieniem):**
- Kolejnosc commitow: klucze (Settings + gen_mcm) jako pierwszy commit - kazdy commit buduje sie sam (LootTally czyta klucze).
- Blok B kolejki ma 10 kolumn zamiast 8: dodane "wczesniej" (dzialka poprzedniej bitwy jeszcze w kolejce przy otwarciu) i "przepadlo" (kolejka wyczyszczona bez
  oddania: symulacja, sluzba w armii ROT). Bez nich tozsamosc B nie trzymalaby sie przy dwoch bitwach w ciagu godziny.
- Padle konie: "z kolejki / ze zbrojowni / poza lupem / brak" - projekt mial 3 pozycje; "poza lupem" (kon nie wszedl do lupu gracza - nic do zdjecia) oddzielone
  od "brak" (wszedl, a nie ma go nigdzie), zeby LUP-J5 = 0 nie lapal odrzutu DTE. TakeKill dodatkowo nic nie zdejmuje, gdy pomiar wie, ze kon nie wszedl do lupu
  (inaczej zdjalby konia gracza - ten sam blad, ktory naprawiamy).
- Z1 wymaga tez znanego modyfikatora wraka (AddWreck) - bez niego wrak przyszedlby jako cala sztuka.
- Klasyfikacja: sztuka zastepcza sprawdzana przed ciosem (DTE pomija sloty tymczasowe zanim patrzy na cios); kandydaci "pekniete" tylko u wroga od tieru 3
  (DTE nie peka nic nizej).
- `EncounterManager` jest w przestrzeni `TaleWorlds.CampaignSystem` (nie `...Encounters`, jak w projekcie) - sprawdzone w dekompilacji 1.4.8.
- Linia bitwy zawsze (nie tylko 3 pierwsze bitwy) - tozsamosci sprawdzane w kazdej bitwie; ograniczenie 3 / pod flaga dotyczy linii jezdzcow.

### Status: DO SPRAWDZENIA

### Plan autotestu (sesja glowna, gdy gra Jeffa zamknieta; najpierw kopia logow Jeffa z dnia)

Build: `cd zb/m109-lup && dotnet build Armoury/Armoury.csproj -c Release -v q --nologo -p:GameLibs=.../lancuch/libs` (kod wyjscia), `python -I tools/gen_mcm.py`,
`sprawdz_narzedzie.py tools/sprawdz_logi.py`, `python -I tools/sprawdz_logi.py --grupa m109lup` (LUP-JS i LUP-JS2 OK).
Biegi: `testy-1033.ps1 -Dll n109lup -Tree zb/m109-lup -Prefix 109l -CsAt test/CrashScribe-at-n103.dll`

| Bieg | Parametry | Sprawdzic |
|---|---|---|
| bitwa-lup (nowy) | `-Runs bitwa -ExtraFlags armoury-lup.flag` (zapis autotest-F-d240, 4 bitwy, w 1 i 4 po 6 konnych T5) | LUP-J0..J10 TAK, w szczegolnosci J4 (Spoils wyjal 0), J5, J6, J8; linie jezdzcow: kon, uprzaz, tulow; istniejace grupy bitewne bez nowych NIE poza liczba wrogow (+6) |
| p20 | prolog 20 dob | LUP-J0, J1 TAK, reszta INFO; m105dziennik, m103lektor, m103sceny, m101prolog jak w 108d |
| wczyt | autotest-dlugi + 20 dob, stary zapis | wczytanie, LUP-J0, J1; brak nowych kluczy zapisu |
| bitwa bez flagi (opcja) | regresja grup bitewnych bez dolozonych jezdzcow | jak dotad |

Sciezki Jeffa (100% zabojstw bohatera, luk) test nie wymusi - po wgraniu poprosic Jeffa o walke z konnymi bandytami: na liscie trofeow maja byc konie i ladry,
w logu linia `Lup bitwy (m109): jezdzcy bitwy N`.
