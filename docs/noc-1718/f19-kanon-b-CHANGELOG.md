## 2026-10-18 (paczka f19-kanon-b - Jeff: "czy mozesz w savie przywrocic kazdej ze stron posiadlosci jakie powinni miec, czyli oddac wlosci tak jak to wygladalo w ksiazce"; "naprawa tych wydarzen i cofniecie polnocy")

**Mod:** Armoury (nowy `BookHoldings.cs` - klasa `BookHoldings` i zachowanie `BookHoldingsBehavior`; `SubModuleMain.cs` +1 linia `AddBehavior`; `Settings.cs` nowy klucz
`BookHoldingsRestore` w grupie "Book holdings (night 18.10)"; `McmSettings.cs` z `tools/gen_mcm.py`); po recenzji takze CrashScribe (`PovertyPeace.cs`: nowa metoda
`ShiftHistory`, wolana tylko przez Armoury).

### Po recenzji (krok poprawek, 18.10)

Cztery uwagi recenzji sprawdzone w kodzie: trzy poprawione, jedna (jency) odrzucona jako zakres - decyzja Jeffa.

1. **Zwrot skarbca dawal Polnocy falszywa biede E1 na 7 dob (POPRAWIONE).** Sprawdzone: CrashScribe `PovertyPeace.Update` liczy ubytek jako (najstarszy wpis historii z 7 dob - skarbiec
   teraz) / doby i migawki 165 nie czyta; historia jest od wczytania (bez zapisu), a przywrocenie biegnie w pierwszej godzinie na mapie - PO pomiarze startu sesji, wiec
   w zapisie Jeffa najstarszy wpis to skarbiec sprzed zwrotu. Skutek bez poprawki: nastepnego dnia ubytek = caly zwrot / 1 doba (zapas ok. 3 dob, bieda 1), potem ok. 160 tys./d
   (zapas ok. 21 dob, nadal 1) az do wypadniecia wpisu z okna - tlumienie wojen Polnocy, glosy za pokojem, wniosek E1b. Poprawka E1 z paczki f19-kanon-a (`DrainNoOneOff`,
   klucz PovertyPeaceSkipOneOff) tego nie zakrywa w pierwszej dobie: potrzebuje co najmniej 3 wpisow w oknie, a dzien po wczytaniu sa 2.
   Zmiana: CrashScribe `PovertyPeace.ShiftHistory(Kingdom, long delta)` - cala historia skarbca krolestwa przesunieta o delta (pod `_lock`, przyciete do 0..int.MaxValue,
   brak stanu krolestwa = nic, -1); Armoury `BookHoldings.RefundTreasury` po przelewie i po `CrownIncome.ShiftSnapshot` wola ja przez refleksje (`E1Shift`, wzor wywolan
   CrashScribe z Armoury) u obu stron: Korona +dopisane, Polnoc -zdjete. Ta sama zasada co migawka 165 (przeniesienie skarbu, nie ubytek ani wplyw doby), tylko dla tego
   przelewu - inne przelewy (L5b-1, FallenCrown, myto itd.) bez zmian (to zakres poprawki E1 z f19-kanon-a). Po przesunieciu nie ma skoku, wiec `DrainNoOneOff` z kanon-a
   go nie widzi - obie poprawki sie nie dubluja. Linia zwrotu dopisuje "historia skarbca E1 (CrashScribe): <Korona> przesunieta (dob N), <Polnoc> przesunieta (dob N)"
   albo "bez stanu E1" / "NIEPRZESUNIETA (brak ... stara DLL?)" - wtedy CrashScribe nie wgrany razem z Armoury.
   Test offline (harness net472 na zbudowanych DLL, oba zaladowane): Armoury `E1Shift` znajduje metode CrashScribe; brak stanu -> -1; historia [4 500 000; 4 597 022]
   po -1 125 434 -> [3 374 566; 3 471 588] (ubytek wzorem Update: bez przesuniecia 146 916/d, zapas 23,6 dob = bieda 1; po - ujemny, zapas >3000 dob); przyciecie do 0
   i int.MaxValue. Gry nie uruchamialem.
2. **Zaloga wyprowadzana takze przy zmianie w obrebie krolestwa docelowego (POPRAWIONE).** Sprawdzone: `stNeeds = st.OwnerClan != clan`, a plan i przenosiny zalogi szly
   do partii `s.MapFaction` - przy starym panu z tego samego krolestwa (np. inny rod Korony w KL) zaloga szlaby do partii wlasnego krolestwa i twierdza zostalaby pusta.
   Ten sam blad mial warunek korekty fabuly (e): `oldF == newF` odsylalby spod KL partie samej Korony. Zmiana: zaloga planowana i przenoszona tylko, gdy
   `s.MapFaction != newF` (inaczej linia "stary pan z krolestwa docelowego - zostaje z osada" - jak nadanie lenna w grze, ApplyByDefault zalogi nie rusza);
   korekta fabuly tylko przy `oldF != newF`. W zapisie Jeffa nie wystepuje (KL u Starkow, piec zamkow u Stormlands, Starfall u Reach) - luka tabeli na przyszlosc.
3. **Brak odbiorcy zalogi - czekanie zamiast "do domu" z zamowienia (POPRAWIONE).** Sprawdzone: przy `l.Count == 0 && hp == null` osada czekala, a po 30 dobach
   byla pomijana ("czekanie") - wlosc nigdy nie wracala. Zmiana: wtedy szeregowi do domu ta sama droga co przepelnienie zalogi W4c (`OverflowHome.SendHome`):
   wies `ClanBudget.OverflowVillage(zaloga)` (wsie tej twierdzy z danymi BK, potem rodu, potem krolestwa), `ClanBudget.ToVillagePop(..., PeopleBook.MW4c, udzialy, zaloga)`
   - ludnosc BK wsi + ksiega ludzi (znani pochodzeniem do DOM swojego regionu przez A16, weterani E4/10 jak W4c); kolejnosc jak `ClanBudget.ReleaseFrom` (Take z wyjsciem
   `FDisbandHome` "rozwiazane bez bitwy do domu" przed dopisaniem, roster po, porazka wsi = Untake i ludzie zostaja); wight nie idzie do ludnosci BK (jak SendHome);
   sprzet zostaje w zbrojowni osady, sakiewki ludzi bez zmian (jak W4c). Zmiana pana idzie dalej. Tylko gdy nie ma wsi z danymi BK - osada czeka jak dotad (zaloga starego
   pana nie moze przejsc na strone nowego). Linia: "zaloga X N -> [do domu N - wies W (droga przepelnienia W4c)], zostalo M". W zapisie Jeffa nie wystepuje (Polnoc,
   Stormlands i Reach maja partie).
4. **Zwykli jency nowego pana zostaja w jego lochu (ODRZUCONE w tej paczce - decyzja Jeffa, do STAN-PRAC).** Sprawdzone w dekompilacji: `PrisonerReleaseCampaignBehavior.
   OnSettlementOwnerChanged` wypuszcza tylko bohaterow nie w wojnie z nowym panem; `ChangeOwnerOfSettlementAction` (Default/Gift/Barter) zwyklych jencow nie rusza - tak samo
   przy kazdym nadaniu i oddaniu lenna w grze. Powod odrzucenia: (a) zwykly zolnierz nie ma frakcji, tylko kulture - a kultury ROT nie pokrywaja sie z krolestwami (Korona =
   rod Joffreya `crownlands`, ale w jej partiach i w lochu Przystani moga byc tez ludzie Westerlands, Dorzecza, Stormlands; Polnoc wrzucala do lochu jencow z roznych bitew),
   wiec "swoi" po kulturze uwolnilby czesc cudzych i zostawil czesc swoich; (b) loch to osobna ksiega: `PeopleBook` (slownik lochu KDungeon), jency z rabunku `ThrallLaw`
   (villager_*), okupy i praca jenca RealisticCaptivity, `PrisonerLaw` - nowa powierzchnia kolizji poza zamowieniem (CLAUDE.md 8.3). Linia osady podaje liczbe zwyklych
   jencow w lochu KL i Red Keep. **Decyzja Jeffa:** czy uwolnic zolnierzy kultury rodu nowego pana (np. `crownlands` w KL) do pustej zalogi (PeopleBook Take/Give, przyczyna
   "wcieleni jency"), czy zostawic jak w grze.

Build po recenzji: Armoury kod 0, CrashScribe kod 0; gen_mcm.py niepotrzebny (bez zmian w Settings.cs, bez nowego klucza - zachowanie dziala pod istniejacym kluczem
paczki Book Holdings Restore; CrashScribe bez nowego przelacznika - metoda bierna, wolana tylko przy zwrocie).

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
  Zadnego odbiorcy - do domu droga W4c (po recenzji, pkt 3; bez wsi z danymi BK - osada czeka). Gracz i jego partie nigdy nie sa odbiorca. Zwykli jency w lochu zostaja przy osadzie (jency osady; linia podaje liczby).
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
- E1 (CrashScribe PovertyPeace, ubytek z 7 dob historii skarbca): po recenzji POPRAWIONE w tej paczce (punkt 1 wyzej) - zwrot przesuwa historie skarbca E1 u obu stron.
  Wymaga wgrania CrashScribe.dll razem z Armoury.dll (inaczej linia zwrotu mowi "NIEPRZESUNIETA" i Polnoc ma falszywa biede jak przed poprawka). Scalanie z f19-kanon-a:
  obie paczki zmieniaja `PovertyPeace.cs` w roznych miejscach (kanon-a: `KState`, `Update`, `DrainNoOneOff`, `Desc`, linia dobowa; tu: nowa metoda przed `ResetSession`).
- Zaloga: odbiorcy do wolnego miejsca; nadmiar (gdy wolnych miejsc za malo) idzie ponad limit - model dezercji zdejmuje nadmiar (gra: 25% na dobe, DefaultPartyDesertionModel), a nasze
  OverflowHome (W4c) odsyla tych ludzi do wsi (do domu), nie do lasu - jak w kazdej przepelnionej partii; to jest "release them home" z zamowienia. Kiesa zalogi (PartyTradeGold) i zapasy zostaja przy osadzie (jak w L5b-1 / SmallGold - kiesa zalogi zostaje z zaloga osady). XP stosow nie
  przenoszone (jak w ArmsFromPeople.FromSeatGarrison).
- Jency: zwykli zostaja przy osadzie - w lochu Przystani moga siedziec zolnierze Korony wzieci przez Polnoc (zostana jencami w miescie Joffreya; gra nie wie, czyi byli).
  Linia podaje liczbe - decyzja Jeffa (punkt 4 po recenzji: uwolnic "swoich" po kulturze do zalogi czy zostawic jak w grze).
- Po recenzji - do domu bez odbiorcy: ludzie ida do wsi tej twierdzy (OverflowVillage zalogi), a A16 oddaje znanych pochodzeniem do ich regionu - przy KL bylaby to wies
  Przystani dla czesci bez pochodzenia (jak przepelnienie zalogi W4c). Tylko gdy krolestwo starego pana nie ma zadnej wolnej partii lordow ani zalogi.
- Po recenzji - zmiana w obrebie krolestwa: zaloga zostaje i przechodzi do nowego pana razem z osada (ten sam stan co nadanie lenna w grze).
- Bohaterowie: wyprowadzany jest kazdy bohater frakcji w wojnie z nowym panem siedzacy bez druzyny (takze damy i dzieci rodu Stark, jesli sa w KL/RK) - linia wymienia imiona.
- Teleport: armia Robba (jesli w promieniu) staje pod Harrenhal - to jednorazowa korekta, nie zakaz; AI Polnocy moze potem znow ruszyc na KL (bez naszego wpisu roszczenia
  SCA - ale cel wojny ROT Crown vs North trwa). Obrona KL to dalej temat armii/terminu wojen (decyzja Jeffa, nie ta paczka).
- Stolica BK Korony zostaje tam, gdzie BK ja przeniosl po utracie KL (BKCapitalBehavior wybiera nowa tylko przy utracie) - KL nie wroci sam jako stolica BK; L5b-1 i dwor BK
  licza z tej stolicy. Do decyzji (poza paczka).
- Cofniecie DLL po przywroceniu i ponowne wgranie tej paczki: flaga ginie ze starszym DLL (zapis bez zachowania) - przywrocenie pobiegnie znowu tylko tam, gdzie pan znow sie rozni
  (zwrot skarbca tylko przy zmianie KL z rak Polnocy). Wtedy klucz Book Holdings Restore = NIE.
- Co sprawdzic w logu po wczytaniu: "Wlosci z kronik (f19-kanon-b)" - start sesji, linie osad (odbiorcy zalog, kto wyprowadzony, Red Keep "przeniesiony przez ROT", roszczenie
  SCA, korekta fabuly z liczba partii i ludzi, zwrot "zdjete - dopisane = 0", "historia skarbca E1 (CrashScribe): ... przesunieta (dob N)" u obu stron), KONIEC
  z potknieciami 0; CrashScribe bez nowych bledow; linia dobowa "Pokoj z biedy (E1)" nastepnego dnia: Polnoc bez skoku ubytku od zwrotu (ubytek jak przed zwrotem, zapas mniejszy tylko o skarbiec,
  ktory oddala); kronika: KL u rodu Joffreya; FABULA: BlackwaterSiege nie pomijany.

**Status:** DO SPRAWDZENIA (build Armoury i CrashScribe kod 0; gen_mcm.py: 1977 ustawien, +1 klucz - przed recenzja; po recenzji bez zmian ustawien).
