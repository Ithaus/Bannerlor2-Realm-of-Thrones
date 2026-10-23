## m109-crash - wywrotka przy wejsciu bandy do kryjowki (23.10)

**Problem:** Jeff 22.10 23:59:40 (wersja 103.5) - gra sie wywrocila na mapie (gracz stal w polu/miescie, nic nie robil). CrashScribe
`crash-original-2026-10-22_23-59-40.log`: NullReferenceException w `BanditSpawnCampaignBehavior.OnHomeHideoutChanged` <- `BanditPartyComponent.SetHomeHideout`
<- `Hideout.OnPartyEntered` <- `EnterSettlementAction.ApplyForParty` <- tik mapy (banda AI wchodzi do kryjowki).

**Przyczyna (dekompilacja 1.4.8):** `Hideout.OnPartyEntered` ustawia kazdej bandzie frakcji bandyckiej dom = ta kryjowka; zdarzenie zmiany domu
podaje stara kryjowke, a `BanditSpawnCampaignBehavior.OnHomeHideoutChanged` czyta `oldHomeHideout.Settlement` bez sprawdzenia null. Stara kryjowka
null ma kazda banda z `CreateLooterParty` (dom = osada). W grze to tylko szabrownicy i dezerterzy - ich kultura nie ma osad, wiec AI gry
(`AiVisitSettlementBehavior.CalculateVisitHideoutScoresForBanditParty`: `MapFaction.Culture.CanHaveSettlement`) nie prowadzi ich do kryjowek.
U nas tak powstaja bandy kultur z kryjowkami: najazdy Wolnych Ludzi (FreeFolkRaids, `armoury_ffraid`), bandy prologu i Kroniki
(`StoryActions.SpawnBand`: `armoury_prl_*`, `armoury_kr_*`). AI co godzine wazy je jako gosci kryjowek; pierwsze wejscie = wywrotka.
Jedyny sluchacz tego zdarzenia (gra + wszystkie DLL w Modules) to `BanditSpawnCampaignBehavior`.

**Zmiana:** `Armoury/src/HideoutHomeGuard.cs` (klucz `HideoutHomeGuardEnabled`, domyslnie TAK; NIE = gra jak w 103.5): prefiks na
`BanditSpawnCampaignBehavior.OnHomeHideoutChanged` - gdy stara kryjowka jest null, robi to samo co gra (licznik band starego domu -1, kryjowki +1),
ze starym domem = osada `_relatedSettlement` bandy (ta, ktora gra policzyla przy utworzeniu bandy), i nie wola oryginalu. Inne przypadki - oryginal
bez zmian. Finalizer polyka kazdy inny wyjatek tej metody (licznik, pierwsze 3 i co 100 do Log.Error). Banda zostaje w kryjowce jak zwykli bandyci
(jej domem jest od tej chwili kryjowka). Linie `Dom kryjowki (m109): ` - start sesji (latki 1/1), pierwsze 5 band szczegolowo (id, klan, kultura,
stary dom, kryjowka, zrodlo bandy), doba z liczba wejsc i zrodlami. Progi: `python -I tools/sprawdz_logi.py --grupa m109crash <Armoury-*.log>`.

**Ryzyko / co sprawdzic:** (1) latka musi wejsc (linia startu: latki 1/1) - metoda prywatna zadeklarowana w BanditSpawnCampaignBehavior (DeclaredMethod),
wolana przez delegat zdarzenia (bez inline); (2) bandy prologu/Kroniki po wejsciu do kryjowki maja dom = kryjowka (Prologue.World.Clans liczy bandy
klanow wedlug domu - moze dolozyc nowa bande do wsi; bez wplywu na sceny, ktore prowadza bandy po id); (3) licznik band wsi (klucz slownika gry
dla osad niebedacych kryjowkami) moze zejsc ponizej zera - gra czyta go tylko dla kryjowek; (4) z linii doby widac, czyje bandy wchodza - jesli
bandy scen, rozwazyc trzymanie ich z dala od kryjowek.

**Status:** DO SPRAWDZENIA (autotest wczyt + wgranie 103.6).
