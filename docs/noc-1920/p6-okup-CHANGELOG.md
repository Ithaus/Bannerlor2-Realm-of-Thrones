# p6-okup - okup okolicy zamiast palenia wsi (R3-03a, POMYSLY-35 pkt 4), galaz w-toku/p6-okup na 91bf9c63 (fala 2)

## Dla Jeffa (prostymi slowami)
Lord albo armia, ktora doszla do wrogiej wsi, zeby ja spalic, moze teraz zamiast tego wziac od niej okup: starszyzna placi z kiesy wsi od 10%
(gdy napastnikow jest niewiele wiecej niz milicji) do 20% (gdy jest ich piec razy wiecej), a wies nie plonie - ludzie, plony i stada zostaja.
Lord liczy sam: bierze okup, gdy daje tyle co rabunek po odjeciu zoldu za dni rabowania; rabuje dalej, gdy jego ludzie sa glodni, rod nie ma
na zold, okup bylby mniejszy niz dzien zoldu albo wies chce walczyc - a Zelazni Ludzie i Wolni Ludzie rabuja zawsze. Ty masz w menu wrogiej
wsi ("Take a hostile action") nowa opcje "Demand tribute" z kwota w dymku. Wies, ktora zaplacila, przez 30 dni nie jest rabowana przez to
krolestwo (ani przez Ciebie, jesli to Twoje krolestwo) i nie placi mu drugi raz; pan wsi i starszyzna lubia napastnika troche mniej, ale mniej
niz po spaleniu wsi.

## Problem / zrodlo pomyslu
- POMYSLY-35 pkt 4 (R3-03a), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", potem "Rob od razu fale 2" i (po wstrzymaniu) "zacznij fale 2".
  Zrodla: `docs/audyt-2026-10-09/10-pomysly.md` (rozdz. 2 "Okup za oszczedzenie (patis / appatis)", Z10 - decyzja Jeffa "tak"),
  `04-wojna-armie-lupy-okupy.md` N4 (wies placi 10-20% kiesy, plony zostaja), `00-AUDYT-SWIATA` 5.3 poz. 7.
- Dowod z biegu 160 dob na WGRANIU 91 (`logi-wzorcowe/Armoury-2026-10-20_00-44-29.log`, 161 linii "Rabunek osada po osadzie (E7/R1): dzien"):
  614 sesji rabunku AI, 139 pelnych krokow, 475 przerwanych (77%); lup razem 15 470 zl zlota z kies wsi i 2 928 zl towaru - ok. 25 zl na sesje.
  Kiesy wsi razem 5 575 636 zl w 571 wsiach (linia "Daniny wsi (DS)": skrzynia). Rabunek AI daje dzis malo, a niszczy ludzi (uchodzcy) - okup
  bierze pieniadz tam, gdzie jest, i zostawia wies.
- Dotad: okupu wsi nie bylo; byl tylko okup oblezenia (SiegeRansom - miasto i zamek) i zelazna cena najezdzcow (ReaverLaw WW1).

## Zmiana (Armoury; nowy plik `RaidTribute.cs`)
1. **Regula (jedna dla AI i gracza)**: q = zdrowi ludzie napastnika (partia albo cala armia wodza) / obroncy wsi (milicja + zdrowi ludzie partii
   stojacych we wsi, wrogich napastnikowi). q < 1 - wies stawia opor, okupu nie ma. Stawka r = Low + (High - Low) x min(1, (q - 1) / 4):
   1:1 - 10%, 3:1 - 15%, 5:1 i wiecej - 20% (`RaidTributeShareLow` 0.10, `RaidTributeShareHigh` 0.20). Okup T = floor(r x kiesa wsi) -
   `Village.Gold`, ta sama kiesa, z ktorej placi lup rabunku (`SmallGold.LootPayer`, utarg wsi zostaje we wsi - paczka 112).
2. **Przelew**: `GiveGoldAction.ApplyForPartyToCharacter(strona wsi, wodz, T)` - jak lup E7/R1 (kiesa wsi -> wodz partii/armii, przy graczu ->
   Hero.MainHero). Kontrola w linii: przyrost kiesy wodza - ubytek kiesy wsi (= 0).
3. **AI - chwila decyzji**: prefiks `EncounterManager.StartSettlementEncounter(MobileParty, Settlement)` (Priority.Normal, `__runOriginal` -
   gdy inny prefiks, np. brama Innych CrashScribe, juz zatrzymal spotkanie, nic nie robimy) z TYM SAMYM warunkiem co galaz rabunku gry
   (G EncounterManager :221-229: partia AI, ShortTermBehavior RaidSettlement na te wies, wojna, wies Normal, punkty > 0.001, bez zdarzenia na wsi),
   bez gracza we wsi i bez spotkania gracza w tej wsi, tylko wodz (czlonek armii - decyduje wodz), lord z bohaterem, nie Inni (Undead).
   Kolejnosc: wies juz chroniona przed tym krolestwem -> partia staje i mysli od nowa (licznik "odprawione"); Zelazni Ludzie / Wolni Ludzie
   (`ReaverLaw.IsReaver`) -> rabunek; q < 1 -> rabunek (bitwa z milicja jak w grze); jedzenie srednio na druzyne < 5 dob -> rabunek (spichlerz okregu
   karmi napastnikow, E7/R1 K4); `CitySack.NeedOf(...).InNeed` (zalegly zold albo kiesa rodu < 14 dob zoldu druzyn rodu - ta sama miara co okup
   oblezenia) -> rabunek; T < doba zoldu armii (`NeedOf.ArmyWage`) -> rabunek; `DistrictRaid.StepEstimate` (nowa, tylko odczyt, ta sama arytmetyka
   Plan co krok R1): E = s x (kiesa + wartosc magazynu wsi), D = doby kroku; T < E - C x D -> rabunek; inaczej **okup**: przelew, ochrona, relacja,
   `SetMoveModeHold` + `Ai.RethinkAtNextHourlyTick` (jak gra przy wsi juz spladrowanej, G :147-150) i spotkanie nie zachodzi (false).
4. **Ochrona**: para (wies, frakcja napastnika - `MapFaction`, krolestwo albo rod bez krolestwa) na `RaidTributeSpareDays` (30) dob. Mysl AI:
   postfiks `CampaignEventDispatcher.AiHourlyTick` (Priority.VeryLow - po WW3 ReaverLaw, Strazy i odsieczy) daje opcjom `RaidSettlement` na wies
   chroniona przed krolestwem partii wynik -1 (petla gry wybiera tylko wynik > -1; ten sam sposob co `WatchLaw` "Straz nie rabuje"). Odczyt
   z migawki (slownik wies -> [frakcja, do doby], podmieniany w calosci przy zmianie - bez blokad i bez alokacji w mysli). Gracz: postfiksy warunkow
   gry "Raid the village" i "Force peasants to give you goods" (`VillageHostileActionCampaignBehavior`) - opcja zablokowana z dymkiem, gdy wies
   zaplacila krolestwu gracza. Drugi okup od tego samego krolestwa w tym czasie - nie (AI: wies chroniona, gracz: opcja zablokowana).
5. **Relacja**: wodz z panem wsi -2, z kazdym notablem -1 (rabunek gry: -6 i -3, `CharacterRelationCampaignBehavior.OnRaidCompleted`); wies rodu
   gracza okupiona przez AI - bez zmiany relacji, jak w grze przy rabunku.
6. **Gracz**: opcja "Demand tribute" w menu `village_hostile_action` (tylko w wojnie z panem wsi; poza wojna - opcje gry z ostrzezeniem).
   Dymek: kwota, stawka, kiesa wsi, sila (ludzie : obroncy), dni ochrony. Zablokowana z powodem: krolestwo jeszcze nienapadalne, wies juz zaplacila
   (ile dni ochrony zostalo), wies stawia opor (obroncy wobec ludzi), pusta kiesa. Wynik: zloto, ochrona, relacja, komunikat, powrot do menu wsi
   (`GameMenu.SwitchToMenu("village")` - zwykle menu, jak "Forget it" gry; nie menu oczekiwania - CLAUDE.md 7). Opcja dodana w OnSessionLaunched
   (gra dodaje swoje w OnAfterSessionLaunched), wiec zwykle stoi na gorze listy.
7. **Komunikaty**: wies rodu gracza zaplacila okup AI - "X paid N denars of tribute to Y (realm) to be spared."; armia, w ktorej jest gracz,
   wziela okup - "Your army took N denars of tribute from X and spared it."
8. **Zapis** `arm_raidtribute` (`SaveText.Sync`): "idWsi,idFrakcji,doDoby,doba,zloto;..." - brak klucza (stary zapis, nowa kampania save040)
   = brak ochron. Migawka budowana przy starcie sesji (obiekty swiata juz sa), rekordy z nierozpoznana wsia/frakcja zostaja (licznik w linii startu).
   Reset: osobna linia `_p6OkupReset` w ArmouryBehavior (wzorzec p5).
9. **Linie logu**: "Okup okolicy (R3-03a): start sesji | klucz ... | latki n/4 | opcja menu wsi tak | ochron N ..." i co dobe "Okup okolicy (R3-03a):
   dzien N | okupy: AI n (zl), gracz n (zl), stawka srednio, kiesy tych wsi, szac. lup rabunku tych wsi | rabunki AI zaczete n (najezdzcy, opor wsi,
   glod, rod w potrzebie, okup ponizej doby zoldu, rabunek oplaca sie bardziej - szac. lup) | ochrona: odprawione u wsi, opcje rabunku AI wyciete,
   gracz zablokowany, par wies-krolestwo (wygaslo) | relacja | kontrola: zloto wodzow - kiesy wsi | potkniecia | przyklady (do 3)". Prawdziwy lup
   rabunkow - jak dotad linia "Rabunek osada po osadzie (E7/R1)".
10. **Klucze MCM** (nowe, w Armoury.json Jeffa ich nie ma - wchodza domyslne): `RaidTributeEnabled` (TAK), `RaidTributeShareLow` 0.10,
    `RaidTributeShareHigh` 0.20 (suwaki 0..0.5), `RaidTributeSpareDays` 30 (suwak 1..84). Pola w Settings.cs zaraz za `RaidPlayerStepPopup`.
11. **Wpiecia w plikach wspolnych** (minimalne): `ArmouryBehavior.cs` - 3 miejsca po 1-3 linie (reset, SyncData, `RaidTribute.RegisterEvents(this)`
    za VillageHerds); `Settings.cs` - 4 pola; `DistrictRaid.cs` - nowa metoda `StepEstimate` (tylko odczyt, nic w istniejacym kodzie);
    `tools/gen_mcm.py` - 3 zakresy suwakow; `tools/sprawdz_logi.py` - grupa `p6okup` (def `p6okup_main`, stale `P6OKUP_*`). SubModuleMain bez zmian
    (latki zaklada RaidTribute w starcie sesji, wlasna instancja Harmony "com.jeff.armoury.raidtribute", raz na proces).

## Ryzyko / kontrola calosci (w kodzie)
- **Zamknieta ekonomia**: jedyny przeplyw - kiesa wsi -> bohater przez GiveGoldAction ze strona wsi (platnik i odbiorca). Ksiega pieniadza widzi go
  jak lup E7/R1 (`MoneyLedger.OnGoldTraded`: `_goldOut[kiesy wsi, lordowie/gracz]`), ksiega dochodu rodow - jako wplyw spoza rodu (ClanIncomeBook.OnEvent).
  To ten sam rodzaj przeplywu co lup rabunku, wiec bez nowej pozycji N169 w Obiegu (N169 jest dla przeplywow bez zdarzenia gry). Zloto nie powstaje:
  kwota przycieta do kiesy wsi; `SmallGold.GivePrefix` nie rusza (dajacy nie jest "nic").
- **Regresje**: gdy klucz NIE albo zadna regula okupu nie zachodzi, prefiks zwraca true i gra biegnie bez zmian (rabunek E7/R1, E4/5, zelazna cena,
  stada, jency BK). Postfiks mysli niczego nie robi bez ochron (pusta migawka - wyjscie na pierwszej linii). Rabunek wsi chronionej przez INNE
  krolestwo - bez zmian.
- **Kolizje**: brama Innych CrashScribe (`NightKingGate.EncounterPrefix`) na tej samej metodzie - Inni sa wykluczeni u nas, a `__runOriginal` szanuje
  ich zatrzymanie. W mysli AI po nas biegnie tylko `SiegeWalls2.ThinkPost` (Priority.Last) - dotyczy trwajacego szturmu/zdarzenia, nie ochrony wsi.
  NightRest przywraca rozkaz rabunku sprzed snu tylko przy wojnie - jesli wies tymczasem zaplacila, partia dojdzie, stanie i pomysli (licznik
  "odprawione"). StrategicCampaignAI liczy wsie "w rabunku" z `IsUnderRaid/IsRaided` - okupiona wies nie jest liczona (zgodnie z regula).
- **Rownowaga AI (najwieksze ryzyko - do testu)**: przy dzisiejszym lupie rabunku (~25 zl na sesje) okup wyjdzie zwykle korzystniejszy, wiec moze
  byc "wojna prawie bez palenia". Hamulce w regule: najezdzcy zawsze rabuja, glod, rod w potrzebie, okup ponizej doby zoldu (biedne wsie dalej
  plona), opor milicji. Grupa `p6okup` PO-5 daje DECYZJA, gdy rabunkow zostaje < 10% decyzji AI. Gdyby tak bylo - pierwszy krok to wyzsza
  `RaidTributeShareLow/High` albo prog doby zoldu, bez zmiany kodu.
- **Szacunek rabunku** pomija stada (VillageHerds), jencow BK i zelazna cene (najezdzcy i tak rabuja) - E jest dolna miara lupu.
- **Watki**: postfiks mysli - tylko odczyt migawki (`Volatile.Read`), `SetBehaviorScore` na liscie tej mysli, licznik `Interlocked`; reszta na watku
  kampanii (spotkanie, menu, doba).
- **Zapis**: stary zapis Jeffa (save040 na WGRANIU 92) nie ma klucza - start bez ochron; starsza DLL klucz pomija.
- **Menu**: opcja zwyklego menu, bez `SwitchToMenu` z menu oczekiwania; warunki opcji gry tylko blokowane (IsEnabled false + dymek), nie ukrywane.

## Status
DO SPRAWDZENIA (zbudowane: `dotnet build Armoury` kod 0, 0 Error(s); nie wgrane, nie testowane w grze - fala 2 idzie do wspolnego scalenia).

## Decyzje (moje, Jeff: "ekonomie projektuje Claude")
- Stawka 10-20% wedlug przewagi (1:1 -> 10%, 5:1 -> 20%) - liczby z audytu N4; sila jako liczba ludzi (prosta do wyjasnienia), nie "sila" modelu gry.
- Ochrona 30 dob przed calym krolestwem napastnika (jego lordowie i gracz z tego krolestwa) - inaczej okup bylby darmowym dodatkiem do rabunku.
- "Rod w biedzie - rabunek": ta sama miara potrzeby co okup oblezenia (`CitySack.NeedOf`) - wojsko bez zoldu rabuje samo.
- Okup ponizej jednej doby zoldu armii - wies plonie (nie warto sie targowac o grosze; biedne wsie dalej sa palone).
- Zelazni Ludzie i Wolni Ludzie zawsze rabuja (zelazna cena); Dothrakowie - jak inni (Jeff o nich nie decydowal przy WW).
- Relacja -2 / -1 (trzecia czesc kary za pelny rabunek gry).
- Gracz: tylko w wojnie (poza wojna okup bylby wypowiedzeniem wojny - gra ma na to swoje ostrzezenia przy rabunku).
- Bez osobnej pozycji w ksiedze Obieg - przeplyw jest przelewem gry z platnikiem i odbiorca (jak lup E7/R1).

## Test w grze
- Autotest (120-160 dob, wojny AI): `python -I tools/sprawdz_logi.py --grupa p6okup <Armoury-*.log>` - PO-0 latki 4/4 i opcja menu, PO-2 okupy AI > 0,
  PO-3 kontrola 0, PO-4 potkniecia 0, PO-5 udzial rabunkow (INFO / DECYZJA < 10%), PO-6 stawka 10-20%, PO-7 odprawione <= okupy, PO-8 rabunki E7/R1 dalej sa.
- Grep: `Okup okolicy (R3-03a): start sesji` z `latki 4/4 | opcja menu wsi tak`; `Okup okolicy (R3-03a): dzien` z `okupy: AI [1-9]`;
  `kontrola: zloto wodzow - kiesy wsi 0 |`; `ERROR in RaidTribute.` - 0.
- Recznie (Jeff): wrogia wies w wojnie -> "Take a hostile action" -> "Demand tribute": dymek z kwota; po kliknieciu zloto w kiesie, komunikat, powrot do
  menu wsi; drugi raz "Take a hostile action" - "Raid the village", "Force peasants to give you goods" i "Demand tribute" zablokowane z dymkiem
  o ochronie. Wies, w ktorej milicja jest liczniejsza niz Twoi ludzie - opcja zablokowana ("fight you off").
