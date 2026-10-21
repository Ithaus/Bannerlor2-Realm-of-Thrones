# m104-e5 - E5: sceny zmieniajace swiat w prologu 298 (orszak, Turniej Reki, Tyrion i Jaime jencami, klany gorskie, ludzie Gory, Beric)

Galaz `w-toku/m104-e5` (baza 756e685f = m103-sceny: prolog 298 + okna scen + obrazki; WGRANIE 101 + m103-sceny). Projekt: PROJEKT-PROLOG-298-2026-10-21.md
pkt 2 (rodzaj "S") i pkt 4 (E5). Jeff 21.10: "Kod scen zmieniajacych swiat i wojen wedlug ksiazki - rob to teraz". Rozpoznanie: wlasne (dekompilacja 1.4.8, ROT 8.1.8,
BK, GrandTourney/RC/Armoury) + rownolegle e5:A / e5:B (journal wf_d4f0c951-09f).
Tylko kampania z prologiem, kazda scena pod swoim kluczem MCM, koniec najpozniej przy koncu prologu (dzien 186-188); kampania klasyczna bez zmian.

## Stan pracy (aktualizowany po kazdym kroku)

| Krok | Co | Stan |
|---|---|---|
| K1 | straz niewoli (pkt 3 i 4): CanonCompanions.HeldRow / Freeing, tabela #m01 classic + #p10 Riverrun, latki EndCaptivityAction.ApplyInternal i RansomOfferCampaignBehavior.DailyTickHero, klucze PrologueTyrionHeld / PrologueJaimeAtRiverrun + 5 kluczy scen | ZROBIONE (build 0, gen_mcm) |

## K1 - straz niewoli: Tyrion w Orlim Gniezdzie (110-132), Jaime w Riverrun

**Problem.** Tabela kanonu (m101-postacie) wsadza Tyriona do lochu Orlego Gniazda w dniu 110 (#p01) i uwalnia w 132 (#p02), a Jaimego (#m01) do Winterfell po wybuchu
wojny Polnocy. Jeff: Jaime ma siedziec w Riverrun (jak w ksiazce) i obaj nie moga uciec ani byc wykupieni do uwolnienia z tabeli. Bez strazy gra wypuszcza ich sama.

**Przyczyna (dekompilacja 1.4.8).** RealisticCaptivity dotyka tylko niewoli gracza (wszystkie latki RC na EndCaptivityAction maja warunek MainHero). Jeniec AI w lochu
wychodzi przez: (a) ucieczke 4%/dobe (PrisonerReleaseCampaignBehavior.DailyHeroTick; 22 dni = ok. 59% szans), (b) zwolnienie przy pokoju i zmianie krolestwa rodu
(ReleasePrisonersInternal - zwalnia kazdego bohatera, ktorego krolestwo NIE walczy z wlascicielem lochu: Tyrion i Dolina sa w pakcie, Jaime i Dorzecze moga nie byc
w wojnie), (c) okup AI-AI (RansomOfferCampaignBehavior.DailyTickHero -> ConsiderRansomPrisoner, u nas prefiks Ransom178 - zloto plynie PRZED ApplyByRansom),
(d) wypuszczenie przez porywacza i wykup (SellPrisonersAction).

**Zmiana.**
- `CanonCompanions`: mapa jencow trzymanych (`HeldRow`, `HeldDay`) - ostatni wykonany wiersz postaci to "prisoner", a pozniejszy "free" jeszcze nie zapadl; liczona
  w dobie po Moves i przy starcie sesji; `Freeing` - na czas naszej akcji "free" (straz przepuszcza); `TemplateHero` - postac z wzoru dla scen.
- `arm_canon_moves.tsv`: #m01 (Winterfell) tylko `classic`; nowy #p10 (ten sam wyzwalacz `war:battania:vlandia+5`) `prisoner:town_V2` (Riverrun) tylko `prolog`;
  dalej #m02 (uwolnienie po Czarnym Nurcie) bez zmian. Kampania klasyczna - bez zmian (decyzja do potwierdzenia: czy Riverrun takze w klasycznej).
- `Prologue.World.cs` (nowy, partial Prologue): prefiks `EndCaptivityAction.ApplyInternal` - jeniec trzymany przez scene (kampania z prologiem, klucz, czas,
  loch OSADY) nie wychodzi przez ucieczke, pokoj, okup, wypuszczenie, wykup; smierc i "po bitwie" (upadek osady, zmiana pana) oraz gracz jako sprawca (rozbicie lochu,
  okup gracza - zaplacil, wiec wychodzi) przechodza; prefiks `RansomOfferCampaignBehavior.DailyTickHero` - okup AI-AI nie zaczyna sie wcale (zloto nie plynie).
  Czas: Tyrion najpozniej do dnia prologu 140 (zapas po #p02), Jaime najwyzej 364 dni od #p10. Bez klucza CanonCompanionsFollowStory straz nie dziala (wiersz "free"
  nie przyjdzie).
- Latki wpinane w `InstallRest` (pierwsza kampania z prologiem w procesie) - klasyczny start: brak latek.
- Klucze MCM (grupa "Prologue 298 (new campaign)"): PrologueRoyalProgress, PrologueHandTourney, PrologueTyrionHeld, PrologueJaimeAtRiverrun, PrologueHillClans,
  PrologueMountainRaiders, PrologueBericHunt (wszystkie TAK; MCM Jeffa ich nie ma - domyslne).
- Linie: `Prolog E5 (m104): start sesji | ...`, `... dzien N | Tyrion: ... | Jaime: ... | straz niewoli: zablokowane dzis ...`, `... straz niewoli: ZABLOKOWANE - <kto>
  (#p01, jeniec town_EN1) - ucieczka ...` (raz na dobe na rodzaj), `... PRZEPUSZCZONE - ...`.

**Ryzyko / co sprawdzic.** Prefiks na prywatnej, duzej metodzie gry (bez wklejania JIT); nikt inny jej nie latka (RC lata publiczne ApplyByEscape/ApplyByRansom tylko
dla gracza - postfiksy). Pokoj: ReleasePrisonersInternal zostawia jenca w lochu (bez rozjazdu stanu). Okup gracza (barter) przechodzi - celowo (inaczej gracz zaplacilby
za nic); decyzja Jeffa, czy zablokowac takze barter. #p10 wymaga wolnego Jaimego w chwili wyzwalacza (jak #m01).

**Status.** DO SPRAWDZENIA (autotest z prologiem: dni 110-132 bez ucieczki Tyriona; Jaime w town_V2 po wojnie Polnocy).
