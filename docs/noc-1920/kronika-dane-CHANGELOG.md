# CHANGELOG galezi w-toku/kronika-dane (Kronika Westeros - warstwa danych, bez C#)

(Plik przeniesiony 21.10 z `docs/kronika-dane-CHANGELOG.md` do `docs/noc-1920/` - jak dzienniki pozostalych galezi.)

## 2026-10-21 - poprawki po recenzji danych K1-K10

**Problem:** recenzja calosci paczek k01-k10 (przeczytane wszystkie 210 okien / 348 wierszy, opcje, wiesci, bandy, zadania - nie tylko probka):
kanon i v2, cytaty, warunki piaskownicy, kolejnosc osi, wersja lekka, chronione postacie, duble z oknami ROT i scenami prologu (PrologueScenes.Data.cs
01-23 koncza sie na upadku Reki - dubli brak), angielski. Walidator byl czysty, ale znalazly sie bledy, ktorych walidator nie widzi (tresc vs stan swiata):
- 047.2 szlo PO 070.2 (`po=kr:070.2+5`, jak w v2), a tekst opowiadal je w kolejnosci ksiazki i drugi raz pokazywal spotkanie Brienne z Jaimem ("Days later
  she finds Jaime") - to samo spotkanie konczy 070.2. Do tego sciezka serialu (083.1 - Brienne ratuje Sanse, ok. 1410) i ksiazki (047.2 - sad Pani
  w Kapturze i ukrycie Brienne na 30 dni, ok. 1430) mogly przyjsc obie, w dowolnej kolejnosci.
- 095.1 (Wielki Sept, serial) zabijal Kevana w opcjach `obie` (a i b), a wiesci N1/N2 mowily o jego smierci; po 052.1 (wyjatek lekki `succeed`)
  Kevan jest glowa Lannisterow, wiec `die` bylby POMINIETY (FORMAT 11) - tekst klamalby w kazdej kampanii kanonicznej. Kevan i tak ginie w 080.1
  (Varys, ksiazka), ktore ma na to warianty `succeed`.
- 115.2 (gruzy Czerwonej Twierdzy, Kleganowie) przychodzilo tez po lagodnej wersji Dzwonow 115.1a (miasto oddane bez ognia) - `kr:115.1` jest
  prawdziwe dla obu wierszy.
- 051.1 (koronacja Tommena) = `done:JoffreyEvent+15`, a smierc Tywina (052.1, m03) = `ev:JoffreyEvent+40`: okno przychodzi za zycia Tywina,
  a tekst mowil o regencji Cersei i Kevanie odmawiajacym sluzby (to jest po smierci Tywina).
- 090.1/090.1a: "On the road to Oldtown" - a 905.1 (wczesniej, od 1200) mowi, ze Sam juz dotarl do Cytadeli (Marwyn).
- Drobne fakty i piaskownica: 075.3 z jamy uwalnia sie "jeden" smok (ksiazka: oba); 079.1 Zlota Kompania "w Myr wsiada na statki" (zrywa kontrakt
  z Myr, nie wsiada tam); 097.1a/b "Sept splonal z polowa ich rodu" - nieprawda po opcji a w 095.1 (Tyrellowie ostrzezeni uciekli), a "{ROSE}, now at
  the head" - nieprawda, gdy glowa Tyrellow sie nie zmienila (Mace zyje w lekkiej); 117.2c "miasto odbudowane z popiolow" - po 115.1a nie splonelo;
  049.2 "her dead sister" - warunki nie sprawdzaja smierci Catelyn; 060.2 "Winter is coming to..." - haslo Starkow jako zdanie opisu (zasada bez cytatow).
- 071.2 przenosi Alys (`clan:` do Thennow) bez sprawdzenia, czy nie jest glowa Karstarkow (wersja lekka nie zmienia glow rodow, FORMAT 16.4).
- 088.2 i 098.1 robia `unhide` Brana, ktorego zadne okno nie ukrywa (po Krwawych Godach jest glowa Starkow) - FORMAT nie mowil, co wtedy robi K0.
**Zmiana:**
- k04 047.2: `przed=kr:083.1` (po oknie serialu przepada, nie ma `_W`); gora/dol napisane jako wyjasnienie wstecz ("Only now does the truth come out ...
  the tale of Sansa was the price of her life") zamiast drugiego spotkania; Jaime bez akcji (zostaje przy rodzie, decyzja v2 47), `hide` Brienne bez zmian.
- k06 070.2a (Raventree bez Brienne): warunek `dead:ROTwanderer9 | kr:083.1`; k07 083.1a (Sansa jedzie bez Brienne): `dead:ROTwanderer9 | kr:047.2`.
  Sciezki Brienne ksiazka/serial sie wykluczaja.
- k08 095.1: bez `die:lord_4_13` w opcjach a i b; N1 "The High Sparrow and hundreds of the faithful die", N2 bez Kevana.
- k10 115.2: warunki `+ & flag:k115_ostra` (wariant d Kleganow dziedziczy).
- k05 051.1a NOWY wariant (`alive:lord_4_1`, obie, lektor 1): ten sam gorny akapit, dolny - Tywin rzadzi jako Reka i chce wydac corke i odeslac
  ja na Casterly Rock (ksiazka), skutek `rel:lord_4_2:lord_4_1:-10`. Id nowe (paczka niewgrana - bez nagrobka).
- k06 071.2: `+ & !head:lord_2_18_1:clan_sturgia_6`.
- Teksty: 090.1/090.1a "Samwell Tarly rides out from Oldtown to bring ..."; 075.3 "both dragons break loose"; 079.1 "breaks its contract with Myr ...
  take ship for the west beneath a banner they have never followed"; 097.1a/b "Since the Great Sept burned, the roses want ..." i "{ROSE}, head of
  House Tyrell"; 117.2c "sits the Iron Throne in King's Landing"; 049.2 "her sister"; 060.2 "Winter is closing in on the Mountains of the Moon".
- `docs/KRONIKA-FORMAT.md`: naglowek (lista poprawek); rozdz. 11 - przyklad glowy rodu zmieniony (Kevan juz nie w 095.1, zasada: nie zabijac w opcjach
  `obie` postaci, ktora inne okno robi glowa rodu) i nowa zasada K0: `unhide` postaci nieukrytej przez Kronike = POMINIETA `nie ukryty`, bez teleportu.
- Generatory paczek (scratchpad 7aee5f12 `k34/`, `k56/`, `k78/gen.py`, `k910/gen.py`) NIE sa zaktualizowane - od tego commitu zrodlem sa pliki TSV
  w repo (ponowne uruchomienie generatora cofneloby te poprawki).
**Liczby po poprawkach (--stat):** k01-k10: okien 210 (wierszy 349 - +1 wariant 051.1a), wyborow 41, band 20, zadan 10, okien z lektor=1 200
(wierszy 311), znakow lektora 147087. K4 12873, K5 18843 (41 wierszy, 39 z lektorem), K7 14914, K8 12636 znakow; reszta bez zmian.
**Testy:** `python -I tools/kronika/kronika_check.py` (k00-k10): **0 bledow, 0 uwag, WYNIK OK** (pojemnosc linii nowych tekstow - walidator, linie.py).
Odwolania nowe: `kr:083.1` (k04 -> k07), `kr:047.2` (k07 -> k04) - znane walidatorowi, bez UWAG.
**Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0):**
- Gra Jeffa bez zmian: same dane, ktore czyta dopiero silnik K0 (go nie ma); `arm_canon_moves.tsv` nietkniety; nic nie wgrywane.
- 047.2 przepada (bez wiesci) na sciezce serialu - linia `POMINIETE ... 083.1 juz ruszylo`. 115.2 i 116.1 po lagodnych Dzwonach czekaja na flage,
  ktora nie przyjdzie (bez `do`) - nic nie pokazuja; to samo robilo juz 116.1.
- Swiadomie BEZ zmian (zapis dla K0/Jeffa): 112.2 Arya zabija Nocnego Krola w bozym gaju bez termu "kto zabil" (luka z FORMAT 12); 109 Twardy Dom
  dopiero, gdy Inni zajma go na mapie (ok. 2167, v2) - dlugo po smierci Jona; kolejnosc 070.1 list -> 070.2 Raventree (v2, w ksiazce odwrotnie);
  078.1 Synowie Harpii w jamie Daznaka (element serialu); 063.1 tylko, gdy Riverrun ma rod Blackfisha (v2: ROT daje mu zamek na Krwawych Godach);
  116.2 w lekkiej Jon (krol Polnocy) zostaje jencem rodu Daenerys bez terminu; 059.2 wymaga Deepwood Motte u Zelaznych (ROT start: Glover) - rzadko.
- Decyzje Jeffa z poprzedniego wpisu (smierci kanoniczne tylko w `pelna`, nowe wiersze `#move` dla Theona/Brienne/Podricka/Jaimego) - aktualne.
**Status:** DANE (bez C#), nie wgrywane - silnik K0 pozniej (plan E).

## 2026-10-21 - 117 wydarzen K1-K10 (paczki tresci, scalenie, poprawki walidatora i formatu)

**Problem:** Kronika Westeros miala format, walidator i 2 okna testowe (k00), ale zadnej tresci. Decyzje Jeffa: wszystkie 117 wydarzen, teksty EN
wlasnymi slowami (bez cytatow), kanon (ksiazki, po nich serial) dopasowany do piaskownicy, wersje lekkie domyslnie, temat obrazka zamiast grafik,
lektor pozniej. Piec autorow (A-E) napisalo paczki K1-K10 rownolegle; trzeba je bylo sprawdzic razem, poprawic i ujednolicic przed silnikiem K0.
**Zmiana:**
- `Armoury/ModuleData/arm_kronika_k01.tsv` ... `k10.tsv` - 117/117 wydarzen, 204 okna + 6 scen "juz w grze" (901-906) = **210 okien**, 348 wierszy
  (z wariantami), 41 list wyboru, 20 band, 10 zadan, 32 pary P1, 70 tekstow wiesci/plotek, 23 wiersze `wersja=pelna`. Generatory (zrodlo plikow,
  dane i teksty w Pythonie): scratchpad 7aee5f12 `k12gen/` (K1-K2), `k34/gen_k34.py`, `k56/`, `k78/gen.py`, `k910/gen.py`.
- `arm_canon_moves.tsv` - 4 wiersze dostaly term `kr:` (stary wyzwalacz zostaje): m02 `|kr:032.1=b|kr:032.1=c`, m04 `|kr:099.2`, m18 `|kr:902.1+1`,
  m22 `|kr:008.1+1`; pary `kr` sa w paczkach.
- Scalenie (ten wpis): `117.1`/`117.1a` - `kod:wielka_rada` przeniesiony ze skutkow okna do skutkow KAZDEJ opcji a-d (skutki okna ida przed lista,
  a uchwyt ma liczyc glos gracza `kr:117.1=x`); K7/K8: 4 wiersze pelne z wlasnym tekstem bez zmiennych (081.2a, 081.2b, 094.1a, 094.2a) `lektor 0 -> 1`
  jak w pozostalych paczkach (FORMAT 16.5). Obie zmiany w generatorach (k910/gen.py, k78/gen.py) i w plikach - generator daje plik bajt w bajt.
- `tools/kronika/kronika_check.py` - 5 poprawek bledow walidatora zgloszonych przez autorow + 2 reguly (uzasadnienia w KRONIKA-FORMAT.md rozdz. 17):
  (1) `_W` okna z `przed`/`do` = uzyty (zapasowa wiesc z rozdz. 3.2) - bylo 31 falszywych UWAG; (2) towary z kodu gry DefaultItems (grain, iron,
  charcoal, hardwood, meat, hides, tools, ironIngot1-6, trash) - nie ma ich w XML, sa w TaleWorlds.CampaignSystem.dll (sprawdzone ciagi UTF-16) i Armoury
  uzywa ich przez GetObject<ItemObject>("iron") - byly 4 UWAGI; (3) zakaz smokow = prefiks `dragon_` (dragon_black...dragon_thumbnail), nie `dragon` -
  odrzucal `dragonglass_axe` i 12 przedmiotow `dragonstone_*`; (4) tokeny w typach zlozonych (`plotka:<id>:@cel`, `own:@cel:<rod|krolestwo>`) - zawsze
  BLAD, choc FORMAT je przewiduje; (5) `kod:` z kolumna `gdzie` w rejestrze KODY (okno/opcja) - nowa regula; (6) UWAGA dla wiersza #move z samymi `kr:`;
  (7) `--stat` z zadaniami, oknami/wierszami z lektor=1 i RAZEM.
- `docs/KRONIKA-FORMAT.md` - 3.3 `kr:` przy ChronicleOfWesteros=NIE = FALSZ (byl blad: "prawdziwe" przy alternatywie `|` odpalaloby m02/m04/m18/m22
  w dniu startu - zglosili wszyscy autorzy); 8 stan par i powod, dla ktorego `remove -> hide` (m11/m20/m21) czeka na K0; 10 jednostki dni;
  11 zasady silnika (`hide` na ukrytym zastepuje koniec, koniec "nigdy" = powrot do siedziby, `die`/`hide` na glowie rodu w chwili akcji tylko w pelnej,
  `dragon_*`, towary, akcja na nieobecnym = POMINIETA); 12 uchwyty z pelnym opisem dla programisty K0 + lista luk bez uchwytu; 13 `@cel` w oldage,
  znaczenie `@cel`/`@region` w wizja, tokeny w typach zlozonych; 17 poprawki walidatora.
**Liczby (python -I tools/kronika/kronika_check.py --stat):**

| Paczka | Okna (wiersze) | Wybory | Bandy | Zadania | Okna z lektor=1 (wiersze) | Znaki lektora |
|---|---|---|---|---|---|---|
| K1 k01 | 20 (34) | 3 | 1 | 0 | 20 (32) | 14966 |
| K2 k02 | 21 (32) | 7 | 3 | 2 | 18 (28) | 13294 |
| K3 k03 | 20 (28) | 3 | 0 | 2 | 20 (26) | 12986 |
| K4 k04 | 23 (34) | 6 | 6 | 2 | 19 (26) | 12850 |
| K5 k05 | 26 (40) | 4 | 1 | 0 | 26 (38) | 18341 |
| K6 k06 | 25 (33) | 1 | 2 | 2 | 23 (31) | 14922 |
| K7 k07 | 22 (38) | 7 | 5 | 1 | 22 (34) | 14920 |
| K8 k08 | 18 (29) | 2 | 1 | 0 | 18 (29) | 12604 |
| K9 k09 | 16 (35) | 3 | 1 | 1 | 16 (27) | 13487 |
| K10 k10 | 19 (45) | 5 | 0 | 0 | 18 (39) | 18166 |
| **razem** | **210 (348)** | **41** | **20** | **10** | **200 (310)** | **146536** |

Znaki lektora = tytul + gora + dol bez prefiksu `{=...}` w wierszach z lektor=1 (310 nagran po kluczu `kr_NNN_w[war]`); unikalnych akapitow mniej
(warianty powtarzaja akapit glownego - np. 115.2a/b/c), w tym 17 wierszy pelnych. 10 okien bez lektora ma `{ZMIENNE}` (stan mapy) albo forme wiesc/log.
Uchwyty `kod:` w calej Kronice: 3 + test (`zaraza` 076.2, `dzwony` 115.1, `wielka_rada` 117.1/117.1a) - limit 10, nowych nie dodano.
**Testy:** walidator na k00-k10: **0 bledow, 0 uwag, WYNIK OK** (przed poprawkami: 0 bledow, 35 uwag - wszystkie falszywe: 31 `_W`, 4 towary).
Nowe reguly: scratchpad 7aee5f12 `kr-final/neg3.py` (sztuczne repo) - 6 BLEDOW i 4 UWAGI, kazda regula trafiona i przepuszczona w druga strone
(dragonglass_axe OK, `@cel`/`@region` w plotce OK, `@kto` BLAD, grain OK / grainx UWAGA, `_W` przy `do` OK / bez `przed`/`do` UWAGA, kod w zlym miejscu
BLAD x3, wiersz tylko z kr: UWAGA). Stare testy neg.py/neg2.py/pos.py: stary i nowy walidator na tych samych danych daja to samo poza UWAGA o `iron`.
Linie okien (linie.py, font Galahad): kazdy tekst miesci sie (tytul <= 45 zn., gora <= 4 linie, dol <= 3 linie) - walidator.
**Ryzyko / co sprawdzic (kontrola calosci, CLAUDE.md 8.0):**
- Regresje w grze: BRAK zmian dzialania. Paczki k01-k10 czyta dopiero silnik K0 (go nie ma). `arm_canon_moves.tsv` czyta dzisiejszy CanonCompanions:
  `kr:` to dla niego nieznany term -> `BaseTrue` = nigdy, a `Fires` laczy termy przez LUB i oglasza "nigdy" tylko, gdy WSZYSTKIE termy sa nigdy -
  m02/m04/m18/m22 ruszaja dokladnie jak przed zmiana; kontrola odwolan przy starcie pomija `kr:` (brak "BRAK:" w linii startu); sprawdz_logi.py
  nie czyta wyzwalaczy. Gra Jeffa (wersja z gospodarka) nie jest dotknieta - nic nie wgrywane.
- `remove -> hide` (m11 Sandor, m20 Osha, m21 Sam) NIE zrobione celowo: dzisiejszy CanonCompanions nie zna `hide` (KnownActions) i odrzucilby
  te wiersze jako zle - zmienilby gre bez silnika. Obowiazek K0: `hide` w CanonCompanions i zmiana trzech wierszy w tym samym commicie. Do tego czasu
  9 okien zaleznych od zywych Sama/Sandora przepada z powodem (lista w FORMAT 8).
- Do zrobienia w K0 (zapisane w FORMAT): `kr:` = falsz przy wylaczonej Kronice; `hide` na ukrytym zastepuje koniec (Arya 008.1 -> 044.2);
  koniec ukrycia "nigdy" = powrot (Rickon 026.2, Melisandre 092.1, Arya); `die` glowy rodu w chwili akcji tylko w pelnej (np. Kevan w 095.1b po 052.1);
  `@cel` dla oldage i wizja; uchwyty wedlug tabeli rozdz. 12; banda Wiary (b062, `faith_militant`) - sprawdzic CreateLooterParty (wroga graczowi?).
- Nowe wiersze `#move` potrzebne, jesli zmiany stron maja byc prawdziwe, a nie tylko w tekscie: Theon (072.3 ucieczka, 087, 108), Brienne i Podrick
  do Starkow (083), Jaime na polnoc (107). Format pozwala (wiersz `kr:` + stary wyzwalacz), ale to decyzja przy silniku.
- Decyzje tresci do akceptacji Jeffa (autorzy wybrali wersje lekka zgodnie z FORMAT 16.4 - walidator nie pozwala w lekkiej zabic glowy rodu):
  w lekkiej ZYJA Stannis (81, zlamany i ranny), Doran (86, rzadza Zmije), Walder Frey (94, gina synowie), Mace (95), Euron i Gora (115), Daenerys (116 -
  Jon w niewoli Nieskalanych, wiec 117.1 Wielka Rada w lekkiej zwykle przepada; epilog 117.2 przychodzi zawsze). Kanoniczne smierci sa w wierszach
  `pelna`. Jesli Jeff chce kanon w lekkiej: dopisac 81, 86, 94, 115, 116 do SUCCEED_LIGHT_OK w walidatorze (jedna linia) i przelaczyc warianty.
  Inne: 052.1 glowa Lannisterow Kevan (ksiazka: dziedzic Jaime, Kevan regent); Kevan ginie albo w 080 (Varys, ksiazka), albo w 095.1b (Sept, serial) -
  ktore pierwsze; 032 alarm tylko opoznia uwolnienie Jaimego do konca Czarnego Nurtu (m02 stary wyzwalacz); 101 (Jorah) i 114 (Varys u Daenerys)
  w lekkiej praktycznie nie przychodza; postacie spoza ROT (Beric, Thoros, Olenna, Ellaria, Hoster, Gilly ...) tylko w tekscie.
- Okna zalezne od paczek innych autorow sprawdzone walidatorem (odwolania `kr:` do 9 paczek bez UWAG): 029 -> 014.1, 047.2 -> 070.2, 082.1/088.1 ->
  066.1 (Bran u trojokiej wrony), 077.1/095.1 -> 062.1 (Wiara Wojujaca), K9/K10 -> 078.1, 082.2, 087.1, 090.1, 094.x, 095.1.
**Status:** DANE (bez C#), nie wgrywane - silnik K0 pozniej (plan E).

## 2026-10-21 - fundament: format, walidator, tematy, k00

**Problem:** 5 autorow tresci Kroniki (117 wydarzen, 204 okna) potrzebuje jednego, sprawdzalnego formatu danych, zanim powstanie silnik K0
(plan "## PLAN kronika" w docs/PLANY-E5-WOJNY-KRONIKA-2026-10-21.md repo glownego, punkty D i E).
**Zmiana:**
- `docs/KRONIKA-FORMAT.md` - specyfikacja `Armoury/ModuleData/arm_kronika_kNN.tsv` (sekcje #okno/#wybor/#tekst/#banda/#zadanie/#para/#usuniete),
  skladnia warunkow (CNF ` & ` / ` | ` / `!`, `+N`) z lista termow planu E, skladnia skutkow z lista akcji (klasy T/R/S, od ktorej paczki),
  uchwyty kod:, okna powracajace, tematy obrazkow, lista chronionych (plan D + uzupelnienie z dekompilacji ROT.Events), zasady tresci,
  podzial 117 wydarzen na K1-K10 (po 11-12, wedlug osi; sceny "juz w grze" 901-906), najczestsze id.
- `tools/kronika/kronika_check.py` (python -I, tylko biblioteka standardowa) + kopia miary linii `tools/kronika/linie.py` (= e4c/linie.py, md5 303202cb).
- `Armoury/ModuleData/arm_kronika_tematy.tsv` - 20 tematow + `stannis_upadek` (tylko 081), 36 sprite'ow ROT w rotacji, obrazki prologu 01-23.
- `Armoury/ModuleData/arm_kronika_k00.tsv` - 2 okna testowe (000.1 z wariantem miejsca gracza i forma wiesc; 000.2 z wyborem 3 opcji, plan:NedEvent-3, przed:ev:NedEvent).
**Odstepstwa od planu (decyzje przy formacie):** akcja `move:` odrzucana (P1 - tylko #para + `kr:`/`mv:`); dodana kolumna `powtorz` (#okno, ostatnia)
dla okien powracajacych z planu A/F i J; dodane sekcje `#tekst` (teksty wiesci/plotek - akcje wiesc:/plotka: potrzebuja zrodla tekstu) i `#zadanie` (K2);
podzial paczek wyrownany do 11-12 wydarzen (plan J mial 10-14). Wiersz glowny = czas + warunki okna + tekst zapasowy; warianty w kolejnosci pliku.
**Testy:** `python -I tools/kronika/kronika_check.py` -> WYNIK OK (k00: 2 okna, 3 wiersze, 1 wybor, 21 tematow). Testy negatywne na sztucznym repo
(scratchpad 7aee5f12/kronfmt: neg.py 30 bledow, neg2.py 19 bledow - prefiksy, pojemnosc, polskie znaki, cudzyslowy, nieznane id/termy/akcje, zloto bez platnika,
chronieni, glowy rodow, #move, pary w obie strony, nagrobki, opcje, md5 lektora, bandy) i pozytywny (pos.py: wszystkie konstrukcje - 0 bledow).
Naprawione po drodze: settlements.xml ma tekst z '>' i samozamykajace `<Settlement .../>` (town_V5 Wysogrod znikal) - parser atrybutow + bez komentarzy XML;
linie.py czyta szerokosc z sys.argv przy imporcie - walidator laduje go z czystym argv.
**Ryzyko / co sprawdzic:** gra nie czyta jeszcze tych plikow (silnik K0 pozniej) - wgranie nic nie zmienia; arm_kronika_*.tsv ida petla arm_*.tsv w wgraj-v5.ps1
(k00 tylko z flaga testu - do zrobienia w K0). Wyzwalacze `remove` m20/m11/m21 (ok. dni 780/990/1200) - zmiana na `hide` najlepiej w K1 (FORMAT rozdz. 8).
**Status:** DANE (bez C#), nie wgrywane.
