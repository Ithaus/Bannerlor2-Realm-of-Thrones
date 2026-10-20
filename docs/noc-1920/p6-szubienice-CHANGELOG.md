## p6-szubienice (20.10) - szubienice przy rozstajach po rozbitej bandzie i oczyszczonej kryjowce (Armoury)

**Dla Jeffa (prosto).** Gdy Ty, lord, patrol, zaloga albo milicja rozbijecie bande, a takze gdy oczyscisz kryjowke, przy najblizszych rozstajach drog
staje szubienica z wisielcami. Stoi od tygodnia do dwoch (po kryjowce dwa tygodnie). W jednym okregu stoi najwyzej jedna, a na calej mapie najwyzej 40.
Nastepna rozbita banda w tym samym okregu przedluza jej czas i dowiesza wisielca (najwyzej trzech). Karawany i wozy wsi nie wieszaja.
To tylko widok na mapie: nie zmienia zlota, walki, jencow ani relacji. Wylacznik jest w MCM: "Map Gallows" (grupa "Map villages").

**Problem / zrodlo pomyslu.** POMYSLY-35 pkt 17 (R3-07a), Jeff 20.10: "28 - nie, 29 - nie. Pozostale tak", potem "Rob od razu fale 2" i po
wstrzymaniu "zacznij fale 2". Audyt `docs/audyt-2026-10-09/08-drogi-wioski-widok.md` (Dla Jeffa pkt 8, rozdz. 2 "Prawo drog", tabela 4.4 N-2):
szubienica przy drodze pokazuje, ze pan tej ziemi pilnuje goscinca (Leges Henrici Primi; w Dorzeczu wisielcy przy drogach to zwykly widok).
W modach nie bylo egzekucji zwyklych bandytow ani szubienic, wiec wyzwalaczem jest rozbicie bandy albo oczyszczenie kryjowki.

**Zmiana.** Nowe pliki `Armoury/src/Gallows.cs` (regula, miejsce, zapis, linie logu) i `Armoury/src/GallowsView.cs` (obrazek na mapie) oraz minimalne wpiecia:
- WYZWALACZ (jedna regula dla gracza i AI, cala mapa): koniec bitwy (`CampaignEvents.MapEventEnded`, sam odczyt), w ktorej strona przegrana miala bande
  (`MapFaction.IsBanditFaction`, bez Nieumarlych - `Undead.Party`), a zwyciezca ma "prawo": druzyna gracza, lord (tez towarzysz z wlasna druzyna), patrol,
  zaloga, milicja albo osada. Karawana i wozy wsi same nie wieszaja (licznik "bez pana"). Bitwa w kryjowce (`IsHideoutBattle`) = oczyszczona kryjowka,
  miejscem jest kryjowka. Dekompilacja 1.4.8 `MapEvent.FinalizeEventAux`: `OnMapEventEnded` idzie przed `HandleMapEventEnd`, wiec partie przegranych
  sa jeszcze w stronach bitwy (tak samo czytaja je `FreeFolkRaids` i `HorseFate`).
- MIEJSCE: najblizsze rozstaje od miejsca bitwy do 10 jedn. (kryjowka 15): wezly traktow ROT z pliku `arm_map_roads_geo.tsv` (ten sam plik co premia drog;
  koniec lamanej, w ktorym schodza sie co najmniej 3 odnogi, takze "T" na srodku innej lamanej - na pliku 16.10: 311 lamanych, 357 wezlow, 119 rozstajow,
  policzone skryptem tym samym sposobem), a gdy dziala widok drog bocznych - wloty drog do wsi w trakt (rozstaje) i rozwidlenia drog bocznych (waga odleglosci
  x1.4). Plan drog bocznych czytany przez nowa metode `MapRoads.SidePlan()` (tylko odczyt, pod ta sama blokada co reszta MapRoads). Brak rozstajow -
  przy drodze najblizej bitwy (8 jedn., kryjowka 12). Brak drogi - nic (licznik "bez drogi"). Szubienica stoi obok drogi, nie na farbie: 0.6 albo 0.9 jedn.
  od srodka rozstajow, w kierunku najdalszym od drog (co najmniej 0.42 od osi), na ladzie, z dala od osad gry, wiosek na mapie i zajazdow; belka bokiem do drogi.
- OKREG = wies gry najblizsza szubienicy; jedna szubienica na okreg (nowe rozbicie przedluza czas i dowiesza wisielca). Czas: bitwa w polu 7 + ludzie bandy / 6
  dob (najwyzej 14), kryjowka 14 dob. Wisielcy: do 9 ludzi bandy 1, do 24 - 2, wiecej i kryjowka - 3. Najwyzej 40 naraz: przy pelnej liscie nowa zastepuje
  te, ktorej czas konczy sie najwczesniej. Koniec czasu = zdjeta (doba).
- OBRAZEK: gra ma prefab "gallows" (`Native/Prefabs/archhitecture_aserai.xml`), ale to scena z pomostem ok. 11 m, fizyka i postacia edytora - na mape sie
  nie nadaje. Szubienica na mapie jest zlozona z tej samej siatki slupa (`wooden_platform_plank_f_column`): dwa slupy i belka, cienkie liny, 1-3 wisielcow
  z siatki ROT `burnt_corpse` (trup z krzyza Boltonow na mapie ROT; zapas: worek `bd_sack_b` / `mi_sack_a`). Wysokosc 0.40 jedn., szerokosc 0.36
  (zajazd ok. 0.8). Droga zajazdow (`RoadsideInnsView`): kopie siatek w pustych encjach bez fizyki, skryptow i klikania; obrazek powstaje RAZ przy kamerze
  (najwyzej 6 na cwierc sekundy), potem tylko pokaz / schowaj przy zmianie stanu; zdjecie (`Remove`) tylko, gdy szubienica zniknie z listy albo zmieni liczbe
  wisielcow (lista sprawdzana po numerze wersji) - nic co klatke. Widoczna, gdy wies okregu jest widoczna (mgla zwiadu) i kamera nisko
  (`MapVillagesHideAboveCameraHeight`). Komponent widoku `Priority 102` (wolne: wioski 100, zajazdy 101, proba drog 105, drogi boczne 106, pola p6-pola 107).
- ZAPIS: `SaveText.Sync` klucz `arm_gallows` (`g1|x;y;yaw;koniec;okreg;wisielcy;rodzaj;kryjowka~...`), pisany tylko z danymi. Brak klucza (stary zapis,
  save040 Jeffa na WGRANIU 92) = brak szubienic. Reset przy nowej kampanii / przed wczytaniem - osobna linia `_p6SzubieniceReset` w `ArmouryBehavior`.
  Szubienice po czasie w zapisie zdejmowane przy starcie sesji.
- KLUCZ NOWY: `MapGallows` (TAK; grupa MCM "Map villages", przy `MillOnBank`). NIE = nowych nie przybywa, stojace schowane (lista i zapis dzialaja dalej).
- LOG: `Szubienice (p6): start sesji ...` (klucz, plik drog, rozstaje, wczytanie ms), `Szubienice (p6): widok mapy ...`, `Szubienice (p6): postawiona ...` /
  `odnowiona ...` (kazda po bitwie gracza, z AI pierwsze 5 na sesje), `Szubienice (p6): dzien N | ...` (doba linii `LogGate.Lines`: aktywne, okregi,
  rodzaje miejsc, wisielcy, od ostatniej linii: bitwy z bandami, postawione, odnowione, zastapione, zdjete, pominiete bez drogi / bez pana / Nieumarli,
  sredni koszt wyboru miejsca, siec i przebudowy, przeszkody, obrazki, potkniecia).
- Wpiecia: `ArmouryBehavior.cs` (Reset, SyncData, MapEventEnded przed `ArmouryBehavior.OnMapEventEnded`, start sesji po `MapMud.SessionStart`, doba po
  `MapMud.Daily`), `SubModuleMain.cs` (rejestracja widoku jak zajazdy, z licznikiem bledu ladowania typu), `MapRoads.cs` (metoda `SidePlan`),
  `Settings.cs` + `McmSettings.cs` (gen_mcm), `tools/sprawdz_logi.py` (grupa `p6szubienice`, SZ-0..SZ-7).

**Ryzyko / co sprawdzic (kontrola calosci).**
- Zamknieta ekonomia: zadne zloto, sztuka ani czlowiek nie przechodzi z rak do rak (nic do ksiegi Obieg). Jency, relacje, walka - bez zmian.
- Watki: wszystko na watku glownym (koniec bitwy, doba, widok); nic w sciezkach AI (PartiesThink) ani predkosci.
- Czestosc: w biegu 160 dob na WGRANIU 91 (`Kiesy rozbitych (3.2)`) rozbijano srednio 3.3 bandy z kiesa na dobe (wszystkich band - wiecej), wiec
  spodziewane 20-40 szubienic naraz; przy wiecej limit 40 zastepuje te najblizsze konca (licznik "zastapione").
- Obrazek: siatki ze scen (nie ikony "mi_") na mapie - tak juz robia wioski (`empire_village_barn_a`) i doki (`rot_boat*`). Rozmiar i wyglad
  wisielca (`burnt_corpse` w skali 0.15 jedn., nie szerzej niz 0.10) do oceny na oko w grze; brak siatki = worek, brak siatki slupa = szubienica pominieta
  (licznik "pominiete", bez wyjatku).
- `Remove` encji w trakcie gry (koniec czasu, wiecej wisielcow) - tak samo robia proporce wiosek (`MapVillagePennants`) i wioski (`MapVillagesView`).
- Bitwy morskie z piratami: jesli w zasiegu jest droga na wybrzezu, szubienica staje na ladzie przy niej (piratow wieszano w portach); miejsce zawsze na ladzie.
- Wczytanie traktow przy starcie sesji: ok. 3.8 mln sprawdzen odcinkow (357 wezlow x 10 565 odcinkow) - kilkadziesiat ms raz na sesje, w linii startu.
- Kolizje: `git merge-tree` na n22-calosc (4885ae75): jedyny konflikt to dopis linii Reset obok `_p5RavensReset` (p5-kruki) - obie linie zostaja;
  wynik scalenia buduje sie (0 bledow), gen_mcm bez zmian. Drzewa n22f-* nie ruszaja `MapRoads.cs`, `SubModuleMain.cs`, `VillageBook.cs`, `RoadsideInns.cs`.
- Zapis: nowy klucz; starsza DLL go pomija; save040 bez klucza = brak szubienic.

**Status.** DO SPRAWDZENIA (zbudowane, bez autotestu - zakaz uruchamiania gry w tej pracy).

**Decyzje (projektowal Claude; wylacznik: klucz `MapGallows`).**
- Wieszaja tylko ci, ktorzy maja prawo: gracz, lordowie, patrole, zalogi, milicja. Karawany i wozy wsi nie.
- Jedna szubienica na okreg (wies gry najblizsza), odnowienie zamiast drugiej; limit 40 naraz, zastepowana ta najblizsza konca czasu.
- Czas 7 + ludzie/6 dob do 14 (kryjowka 14), wisielcy 1-3 wedlug wielkosci bandy.
- Zamiast prefabu sceny "gallows" - uproszczona szubienica z jego siatki slupa, w skali mapy.
- Zapis w grze (pozycja + doba konca), zeby szubienice nie znikaly po wczytaniu.

**Test w grze.**
- Autotest (bez klikania): w logu Armoury `Szubienice (p6): start sesji - klucz MapGallows tak | plik arm_map_roads_geo.tsv: ... rozstajow traktu 119`,
  linie `Szubienice (p6): postawiona` (z AI pierwsze 5) i `Szubienice (p6): dzien N`; `python -I tools/sprawdz_logi.py --grupa p6szubienice <Armoury-*.log>`
  - SZ-0..SZ-6 TAK (SZ-7 INFO: obrazki tylko przy kamerze). ERROR z "Gallows" - 0.
- Jeff: rozbic bande blisko drogi albo oczyscic kryjowke, zblizyc kamere do najblizszych rozstajow - szubienica z wisielcami; zapisac i wczytac - stoi dalej;
  po 7-14 dniach znika. W MCM "Map Gallows" = off - schowane.
