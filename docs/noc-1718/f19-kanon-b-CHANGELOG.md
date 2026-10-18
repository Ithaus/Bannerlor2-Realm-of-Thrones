## 2026-10-18 (paczka f19-kanon-b - Jeff: "czy mozesz w savie przywrocic kazdej ze stron posiadlosci jakie powinni miec, czyli oddac wlosci tak jak to wygladalo w ksiazce"; "naprawa tych wydarzen i cofniecie polnocy")

**Mod:** Armoury (nowy `BookHoldings.cs` - klasa `BookHoldings` i zachowanie `BookHoldingsBehavior`; `SubModuleMain.cs` +1 linia `AddBehavior`; `Settings.cs` nowy klucz
`BookHoldingsRestore` w grupie "Book holdings (night 18.10)"; `McmSettings.cs` z `tools/gen_mcm.py`).

**Problem:** w Ironmanie Jeffa (dzien ~109056, dzien kampanii ~220) wlosci rozjechaly sie z ksiazka (analiza-kanon.json, czesc 1 i 4): Robb wzial King's Landing + Red Keep
(dzien 210, szturm; w ksiazce nigdy tam nie szedl - i bez Przystani u rodu Joffreya ROT nie odpali Czarnego Nurtu: `BlackwaterSiegeEvent.SiegeLeaderReadyToAttackTarget`
l.68 wymaga `KingsLanding.OwnerClan == clan_nord_1`, Fabula pominie go po 6 dniach), Renly zabral Stannisowi 5 zamkow (Sharp Point, Stonedance, Sweetport, Cape Wrath,
Fellwood), Tyrellowie zabrali Daynom Starfall. Przy zdobyciu Przystani Polnoc wziela ze skarbca Korony 1 125 434 zl (Armoury-2026-10-18_13-50-06.log l.1636, L5b-1).
Ironman - bez konsoli, recznie sie nie da.

**Przyczyna:** wojna w grze (AI), nie kod - ale bez narzedzia w modzie zapisu Jeffa nie da sie wyprostowac.

**Zmiana:** jednorazowe przywrocenie, samo przy wczytaniu, raz na zapis (flaga `arm_book_holdings` w zapisie, wlasne zachowanie kampanii - bez dopisku do wspolnego
`ArmouryBehavior.SyncData`). Tabela w kodzie (osada -> rod, oczekiwane krolestwo, blizniak, promien, zwrot):
`town_EW4` King's Landing -> `clan_nord_1` (vlandia), blizniak `ROT_castle61` Red Keep, promien 60, zwrot 1 125 434 od `battania`; `ROT_town31` Sharp Point -> `ROTclan_15`;
`castle_EW5` Stonedance -> `ROTclan_53`; `castle_EW8` Sweetport -> `ROTclan_138`; `ROT_castle18` Cape Wrath -> `clan_empire_west_9`; `ROT_castle17` Fellwood -> `ROTclan_17`
(wszystkie dragonstone); `ROT_town9` Starfall -> `clan_sturgia_4` (aserai). Harrenhal i Essos - nie ma ich w tabeli.
- Kiedy: pierwsza godzina gry (HourlyTick), w ktorej kampania jest gotowa, stan gry = mapa, a gracz: nie w osadzie, nie w bitwie/oblezeniu, nie w armii, nie w niewoli, bez
  spotkania (PlayerEncounter), bez menu i bez rozmowy. Inaczej czeka (linia "czeka" raz na dobe). Nigdy `GameMenu.SwitchToMenu` (pulapka 7: gra sama wola go z
  `PartyDiplomaticHandlerCampaignBehavior.CheckSettlementSuitabilityForParties`, gdy gracz jest w osadzie albo w armii w niej - dlatego te warunki).
- Na osade (wlasny try; 3 wyjatki na tej samej osadzie = pominieta, bez globalnego wylacznika): rod docelowy zywy, glowa zywa i wolna, rod w oczekiwanym krolestwie; stary
  pan ma glowe rodu; nic gracza; brak oblezenia, bitwy w osadzie / zalodze / wsiach / 5 jedn. od bramy. Trwale braki - pominieta z linia; chwilowe - ponowna proba co godzine,
  po 30 dobach - pominieta ("czekanie").
- Przed zmiana (osada i blizniak): bohaterowie bez druzyny, ktorych gra wzielaby do niewoli (warunek 1:1 z `PrisonerCaptureCampaignBehavior.SettlementHeroCaptureCommonCondition`
  wobec frakcji NOWEGO pana) - `TeleportHeroAction.ApplyImmediateTeleportToSettlement` do najblizszej twierdzy wlasnej frakcji (zarzadca najpierw zdjety). Zaloga starego pana
  (szeregowi, ranni proporcjonalnie) - do najblizszych partii lordow i zalog krolestwa starego pana do wolnego miejsca (PartySizeLimit), reszta do druzyny glowy starego rodu,
  bez niej do najblizszego odbiorcy ponad limit; ksiega ludzi `PeopleBook.Take/Give` (FFromGarrison / FToGarrison) i `GarrisonArmory.KitsWithMen` - jak nowa druzyna z zalogi.
  Zadnego odbiorcy - osada czeka. Gracz i jego partie nigdy nie sa odbiorca. Zwykli jency w lochu zostaja przy osadzie (jency osady; linia podaje liczby).
- Zmiana: `ChangeOwnerOfSettlementAction.ApplyByDefault(glowa rodu, osada)` we wlasnym try (wyjatek cudzego nasluchu nie gubi krokow po zmianie). King's Landing tylko jako
  miasto - Red Keep przenosi ROT (`ROTOthersCampaignBehavior.OnSettlementOwnerChanged` l.850-856: `ApplyByDefault(newOwner, RedKeep)`, gdy frakcje sie roznia); potem
  kontrola: Red Keep poza krolestwem docelowym - zmieniamy sami.
- Strategic Campaign AI 1.4.7 (`StrategicAiState.MarkLostClaim` w jego nasluchu zmiany pana) zapisalby kazda oddana twierdze jako "roszczenie" krolestwa starego pana:
  cel wojny ReclaimLostFief i x1.65 x1.65 do wyboru celu oblezenia (`StrategicAiHelpers` l.193, 411) - czyli Polnoc celowalaby w Przystan od razu. Wpis
  `LostSettlementClaimByFaction` krolestwa starego pana wraca do stanu sprzed zmiany (refleksja; brak typu - linia, bez skutku).
- Po oddaniu King's Landing: partie lordow Polnocy w promieniu 60 jedn. od bramy (cale armie, gdy wodz w promieniu - "armia Robba") - `Position = brama` najblizszej
  wlasnej twierdzy poza promieniem (Harrenhal: 102 jedn. od bramy KL wedlug settlements.xml; w promieniu 60 jest tylko Rosby, 29.5) + `SetMoveGoToSettlement` (wzor Mends/Harrenhal z CrashScribe); partie na
  morzu pominiete; samodzielne partie rodu gracza nie ruszane. Warunek liczony PRZED zmiana: zadna z nich nie jest w bitwie ani oblezeniu - inaczej King's Landing czeka.
- Zwrot skarbca (raz na zapis, tylko gdy King's Landing zmienil pana wlasnie teraz ze stolicy "battania"): min(1 125 434, skarbiec Polnocy) skarbiec -> skarbiec Korony,
  zdjete = dopisane (kontrola w linii), `CrownIncome.ShiftSnapshot` u obu stron (jak L5b-1: nie "wplyw dnia" Korony i nie "ujemny wplyw" Polnocy).
- Pusta zaloga nowego pana: zadnych ludzi z niczego - werbunek korony i nasze reguly zalog (linia podaje zaloge i milicje po zmianie).
- Wiadomosc dla gracza (angielski, raz na partie zmian - zwykle jedna), np. "The chronicles are set right: King's Landing and Red Keep return to Joffrey; Sharp Point,
  Stonedance, Sweetport, Cape Wrath and Fellwood return to the bannermen of Stannis; Starfall returns to House Dayne. The forces of The North near King's Landing fall back
  to Harrenhal. The North pays back 1,125,434 denars to the royal treasury of House Baratheon of King's Landing."
- Linie: "Wlosci z kronik (f19-kanon-b): start sesji | ..." (stan flagi), na osade "... dzien D | <osada> | <stary pan> -> <nowy> | bohaterowie bez druzyny wyprowadzeni N [...]
  | zaloga X -> [odbiorcy] | loch: zwykli, bohaterowie | blizniak | roszczenie Strategic Campaign AI | po zmianie: pan, zaloga, milicja | korekta fabuly [...] | zwrot skarbca",
  "czeka - dzien D | powod", "KONIEC - dzien D | zmienione / bez zmian / pominiete | zwrot | potkniecia".
- Nowa kampania (OnNewGameCreated) - flaga "new" od razu: startuje z wlosci ROT, zdobycze w niej sa juz gra.

**Co sie stanie w zapisie Jeffa przy pierwszym wczytaniu (stan z logow 2026-10-18b, dzien 109054):** klucz nowy, MCM da TAK. Linia "start sesji ... czeka (zapis sprzed paczki)".
Jesli zapis jest w miescie/armii - nic do wyjscia na mape. W pierwszej godzinie na mapie:
1. King's Landing (Stark, Polnoc, zaloga 529) + Red Keep (Stark, zaloga 434): bohaterowie Polnocy/Dorzecza bez druzyny w KL/RK -> najblizsza twierdza swojej frakcji
   (Polnoc: Harrenhal); 963 ludzi zalog -> najblizsze partie lordow Polnocy (armia Robba, jesli stoi obok) do ich wolnego miejsca; ApplyByDefault(Joffrey, KL) -> ROT przenosi
   Red Keep; gra wyprowadza partie Polnocy z KL/RK; partie lordow Polnocy w promieniu 60 (z cala armia Robba) -> brama Harrenhal; 1 125 434 zl ze skarbca Polnocy (po zdobyciu
   4 597 022) do skarbca Korony; roszczenie SCA Polnocy jak przed zmiana. Gdy ktoras z tych partii Polnocy jest w bitwie/oblezeniu - KL czeka (reszta tabeli idzie dalej).
2. Sharp Point (Baratheon, Renly, 516), Stonedance (Buckler, 259), Sweetport (Estermont, 307), Cape Wrath (Selmy, 329), Fellwood (Connington, 335): bohaterowie Stormlands
   bez druzyny -> najblizsza twierdza Stormlands (wojna Dragonstone vs Stormlands w kronice dnia 220); 1746 ludzi zalog -> partie i zalogi Stormlands; ApplyByDefault do
   glow rodow bar Emmon / Massey / Sunglass / Seaworth / Fell (wszyscy w Dragonstone wedlug budzet-rodow.csv dnia 109054).
3. Starfall (Florent, Reach, 455): bohaterowie Reach bez druzyny -> Reach (wojna Reach vs Dorne); zaloga -> partie Reach; ApplyByDefault(glowa Dayne, Starfall).
4. Wiadomosc dla gracza, linia KONIEC, flaga "done" - przy nastepnym zapisie gry nie powtorzy sie.
Skutki gry przy zmianie pana (Default, sprawdzone w dekompilacji): kolejka budow czyszczona (BuildingsCampaignBehavior), zarzadca zdjety, siedziby rodow przeliczone
(ClanVariables), decyzje o lenno tej osady usuniete, bohaterowie-jency strony nowego pana w lochu wypuszczeni (PrisonerRelease), warsztaty wlascicieli z wrogiej frakcji
przejete (BK SeizeWorkshops - jak przy kazdej zmianie pana miedzy frakcjami; okno BK tylko, gdy dotyczy gracza), uczta BK w osadzie konczona. Brak: kar relacji
(CharacterRelation / SiegeAftermath / BKRelations - tylko BySiege), ekspansjonizmu Diplomacy (tylko BySiege), przeniesienia tytulow BK (tylko ByGift/ByBarter), naszych
L5b-1 / KingdomBalance / WarLedger / Rumours / SmallGold / CitySack (tylko BySiege), glosowania o lenno (openToClaim false).

**Ryzyko / co sprawdzic (kontrola calosci 05.10):**
- Regresje: nic nie latane; nasluchy tylko HourlyTick (po "done" - jedno porownanie), OnNewGameCreated, OnSessionLaunched; nowy klucz zapisu w nowym zachowaniu
  (`CampaignBehaviorDataStore.LoadBehaviorData`: zachowania bez danych w zapisie SyncData nie dostaja - stan z konstruktora = "czeka"; nazwa "BookHoldingsBehavior" nie jest
  podnapisem innego zapisanego zachowania). Test offline (harness net472 na zbudowanym Armoury.dll): Export/Import flagi - brak klucza = czeka, "new", "done" z wynikami
  bez zmian w obie strony, nieznany format = "done" (ostroznie). Gry nie uruchamialem.
- Kolizje: wszystkie nasluchy OnSettlementOwnerChanged w grze (28), ROT, BK (6), Diplomacy (1), NavalDLC (3), SandBox (5), SCA (1) i nasze (Armoury: 6 nasluchow +
  latki CitySack/SmallGold na ApplyInternal; CrashScribe: 1 nasluch + RotEventFx) przejrzane w dekompilacji; inne mody z dysku nie odwoluja sie do ChangeOwnerOfSettlementAction - przy Default tylko logi albo zachowanie gry jak wyzej. CapitalTreasury: blizniak liczony tylko w oknie KingdomManager.SiegeCompleted - poza nim nie.
  FiefGrants zaloguje "Nadanie lenna (N) zmiana pana" dla KL (nadana Starkom 109046) - tylko linia.
- E1 (CrashScribe PovertyPeace, ubytek z 7 dob historii skarbca): zwrot 1 125 434 bedzie dla Polnocy przez 7 dob "ubytkiem" ok. 160 tys./d (moze dac falszywa biede Polnocy:
  zapas = skarbiec / ubytek, prog 28-91 dob), a dla Korony doplywem - ten sam blad pomiaru co przy L5b-1, ktory poprawia osobna paczka nocy. Przy scalaniu: poprawka E1 musi objac tez ten przelew (u nas migawka 165
  jest juz przesunieta, ale E1 jej nie czyta).
- Zaloga: odbiorcy do wolnego miejsca; nadmiar (gdy wolnych miejsc za malo) idzie ponad limit - model dezercji zdejmuje nadmiar (gra: 25% na dobe, DefaultPartyDesertionModel), a nasze
  OverflowHome (W4c) odsyla tych ludzi do wsi (do domu), nie do lasu - jak w kazdej przepelnionej partii; to jest "release them home" z zamowienia. Kiesa zalogi (PartyTradeGold) i zapasy zostaja przy osadzie (jak w L5b-1 / SmallGold - kiesa zalogi zostaje z zaloga osady). XP stosow nie
  przenoszone (jak w ArmsFromPeople.FromSeatGarrison).
- Jency: zwykli zostaja przy osadzie - w lochu Przystani moga siedziec zolnierze Korony wzieci przez Polnoc (zostana jencami w miescie Joffreya; gra nie wie, czyi byli).
  Linia podaje liczbe - jesli duza, decyzja Jeffa (np. uwolnic do zalogi).
- Bohaterowie: wyprowadzany jest kazdy bohater frakcji w wojnie z nowym panem siedzacy bez druzyny (takze damy i dzieci rodu Stark, jesli sa w KL/RK) - linia wymienia imiona.
- Teleport: armia Robba (jesli w promieniu) staje pod Harrenhal - to jednorazowa korekta, nie zakaz; AI Polnocy moze potem znow ruszyc na KL (bez naszego wpisu roszczenia
  SCA - ale cel wojny ROT Crown vs North trwa). Obrona KL to dalej temat armii/terminu wojen (decyzja Jeffa, nie ta paczka).
- Stolica BK Korony zostaje tam, gdzie BK ja przeniosl po utracie KL (BKCapitalBehavior wybiera nowa tylko przy utracie) - KL nie wroci sam jako stolica BK; L5b-1 i dwor BK
  licza z tej stolicy. Do decyzji (poza paczka).
- Cofniecie DLL po przywroceniu i ponowne wgranie tej paczki: flaga ginie ze starszym DLL (zapis bez zachowania) - przywrocenie pobiegnie znowu tylko tam, gdzie pan znow sie rozni
  (zwrot skarbca tylko przy zmianie KL z rak Polnocy). Wtedy klucz Book Holdings Restore = NIE.
- Co sprawdzic w logu po wczytaniu: "Wlosci z kronik (f19-kanon-b)" - start sesji, linie osad (odbiorcy zalog, kto wyprowadzony, Red Keep "przeniesiony przez ROT", roszczenie
  SCA, korekta fabuly z liczba partii i ludzi, zwrot "zdjete - dopisane = 0"), KONIEC z potknieciami 0; CrashScribe bez nowych bledow; kronika: KL u rodu Joffreya; FABULA:
  BlackwaterSiege nie pomijany.

**Status:** DO SPRAWDZENIA (build Armoury kod 0; gen_mcm.py: 1977 ustawien, +1 klucz).
