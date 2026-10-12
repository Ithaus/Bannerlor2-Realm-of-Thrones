# PROJEKT ETAP 7 - tempo swiata (bez zmiany tempa), mapa, drogi, wioski (PLAN 7.1, 7.4-7.12) - 12.10.2026, wersja 2 (po krytykach)

Status: SAM PROJEKT. Kod, gra, ustawienia i zapisy nietkniete, nic nie budowane, bez commita. Wzor: `docs\etap6\PROJEKT-ETAP6-RZEMIOSLO-SUROWCE-KONIE-2026-10-12.md`.
Krytyka: **v2** po dwoch krytykach (K1 logika/decyzje/logi, K2 kod/latki/UI/koszt) - 23 uwagi, 23 przyjete (4 w zmienionej postaci), 0 odrzuconych; szczegoly `docs\etap7\KRYTYKA-E7.md`
i rozdz. 8. Miejsca zmienione w v2 oznaczone "(v2)". Kolejnosc pracy wedlug Jeffa 12.10 10:30 (SP:1263-1266): kod etapow 5-8 z recenzja i wylacznikami, potem JEDNO scalenie i jeden test.

**Decyzje Jeffa, ktore wiaza ten projekt:**
- 12.10 10:35 UTC: "olej predkosc, zostaw predkosc poruszania sie jak jest" -> 7.2 i 7.3 ODRZUCONE (SP:1252). Warunek "Winterfell - Krolewska Przystan ok. 77 dni"
  (PLAN:218) byl pod 7.2 - nieaktualny; nowe "Skonczone, gdy" w rozdz. 4.
- 12.10: etap 8 (drobne bledy) NIE "przy okazji" (SP:1248); "nie robmy sztucznych praw - jesli ubywa, to ubywa" (SP:1197); "nie robic sztucznie dobrze lordom" (SP:1242-1244);
  gra ma dzialac szybciej - koszt na dobe (8.17); Jeff zacznie NOWA kampanie po skonczeniu (NK dozwolone), stary zapis ma sie wczytac bez bledu.
- 09.10 04:25 (SP:841): L napisy na mapie tylko nad SPALONYMI wioskami; M namioty i ogniska przy napadzie na oboz - WLACZYC; 20 drogi - najpierw pokazac proba, "gdzie wioska, tam droga".
- 09.10 00:10 (SP:773): "proba drog TAK + po drodze szybciej (+15%) TAK" - zapadlo przy planie spowolnienia swiata, PRZED 12.10 -> w tym projekcie jako pytanie 1 (rozdz. 6).
- 09.10 00:00 (SP:760): odbudowa spalonej wioski 1 rok; spalona = >= 50% osad wioski (histereza 3%, CH e5/wsie:1305).
- 08.10 02:20 (SP:377-379): "bierzemy wszystkie 8 do wiosek" (dymek z panem i herbem, proporczyk, rycerz, czym zyje, wiara, historia, pamiec wojny, menu okregu).
- 07.10 (SP:461-463, :425): rabunek osada po osadzie - po kazdej osadzie komunikat i pytanie, osada to DNI, okreg tygodnie, AI tak samo.
- 30.08 (CH e5/wsie:12282): przeprawe przepuszcza pan dla sojusznikow, wrogow nie (CrossingLaw).

Podstawa (dowody nie sa tu powtarzane w calosci):
- rozpoznania `docs\etap7\rozpoznanie-A-logi-dokumenty.md` (**RA7**), `rozpoznanie-B-kod-tempo-drogi-obozy.md` (**RB7**), `rozpoznanie-C-kod-wioski.md` (**RC7**);
- kod: SCR = `C:\Users\GAME\AppData\Local\Temp\claude\C--Users-GAME-Bannerlor2-Realm-of-Thrones\ae222cb7-a7c2-473a-8fc1-1dc1d6f688e3\scratchpad`; **K:** = `SCR\e5\wsie\Armoury\src`
  (galaz `w-toku/e5-wsie` 396c033 = WGRANIE 33 + decyzje 12.10 wsie); GrandTourney i CrashScribe w tym samym drzewie (`SCR\e5\wsie\GrandTourney`, `...\CrashScribe`);
- dekompilacje: **G:** = `SCR\dec32\cs_full` (TaleWorlds.CampaignSystem 1.4.8), **BK:** = `SCR\e4tools\bkfull`; ROT/NavalDLC `SCR\a6dec`, Diplomacy/RBL `SCR\audyt-zloto` (za RB7);
- logi: **L:N** = `Modules\Armoury\Armoury-2026-10-12_02-44-09.log` (120 dob nowej kampanii, WGRANIE 33); **N:N** = `Modules\Armoury\Logs\2026-10-12_02-44-09\noc.log`;
  **W:N** = `...\Logs\2026-10-12_02-44-09\wioski.log`; **CS:N** = `Documents\...\CrashScribe\session-2026-10-12_02-44-08.log`; d91-120 = srednia dob 91-120;
  parsery RA7 `SCR\e7A\`; tempo marszu policzone do tego projektu `SCR\e7P\marsz.py`;
- dokumenty (docs\): **PLAN** = PLAN-DO-KONCA-MODA-2026-10-09.md (etap 7: :199-218), **SP** = STAN-PRAC.md, **a07/a08/a10** = audyt-2026-10-09\07-, 08-, 10-..., **ES** = ESENCJA-AUDYTOW,
  **WL-P** = PROJEKT-WIOSKI-LORE-2026-10-08.md, **WM** = PROJEKT-WIOSKI-NA-MAPIE-2026-10-07.md, **RB** = PROJEKT-RABUNEK-OSADA-PO-OSADZIE-2026-10-07.md,
  **TECH** = dane\wioski-lore\TECHNIKA.md, **AZ** = etap5\AUDYT-ZLOTO-Z-NICZEGO-2026-10-11.md, **P6** = etap6\PROJEKT-ETAP6-...md, **CH** = `SCR\e5\wsie\CHANGELOG.md`.

Oznaczenia: [K] kod plik:linia, [P] pomiar z logu, [S] szacunek, [W] wniosek z kodu (niesprawdzony w grze), [D] decyzja Jeffa (data). Rok gry = 364 doby (kalendarz Armoury,
`Calendar.DaysPerYear` K:Calendar.cs:61), pora = 91 dob = 13 tygodni; dokumenty sprzed 05.10 licza 168 dni (HISTORY.md:20, 45; AUDYT-KALENDARZ.md:169) - ich liczb "na dobe" nie
uzywamy. Napisy w grze i opisy MCM - po angielsku; linie logu - po polsku. Kultury ROT (CLAUDE.md 2): `battania` = Polnoc, `aserai` = Dorne, `vlandia` = Westerlands;
wlasne id ROT `nightswatch`, `skagosi`, `freefolk`, `whitewalker` (Inni), `qohorik`, `summer`, `ibbenese` - zawsze przez obiekt kultury, nigdy przez nazwe.

---

## 0. Dla Jeffa - prostym jezykiem

1. **Tempo marszu zostaje, jak chciales.** Zadna paczka etapu 7 nie zmienia predkosci swiata. Sa tylko dwa wyjatki i oba widac wprost: naprawa bledu kar
   terenu (pkt 2 - niektore kultury jada dzis w lesie SZYBCIEJ niz po rowninie) i pytanie o premie za droge (pkt 11). Dzis duze partie AI robia srednio
   131-138 km na dobe (L:382-35849) - test sprawdzi, ze tak zostaje.
2. **7.4 - las i pustynia.** Polnoc i 7 innych kultur z cecha lasu (Nocna Straz, Skagos, Wolni Ludzie, Qohor, Wyspy Letnie, Inni, Ib) jada dzis w lesie szybciej niz po
   rowninie, a Dorne na pustyni szybciej [S]. Powod: zwracamy cala kare gry, choc gra tym kulturom (i zwiadowcy z perkiem "Forest Kin") polowe kary juz darowala.
   (v2) Skutek widac glownie u jazdy, karawan, wozow wsi i partii wolniejszych niz sufit kolumny (ok. +12% w lesie, Dorne ok. +5% na pustyni). Kolumny piesze lordow
   i tak jada pod sufitem marszu (`MarchPace`, sufit AI ok. 3.0 jedn./h), ktory ten nadmiar dzis obcina - dla nich zmiana wyniesie 0-2%.
   Po naprawie cecha kultury ZMNIEJSZA Twoja kare (Polnoc w lesie polowa, Dorne na pustyni zero), ale nigdy nie robi z niej premii.
3. **7.6 - spiacy lordowie i karawany.** Oboz 0-6 dziala (spi 600-880 partii na godzine), ale co noc ok. 91 razy ktos budzi spiaca partie (N:2835). (v2) Prawie
   4 z 5 obudzonych to KARAWANY: BannerKings co godzine "zwalnia z postoju" kazda karawane stojaca w miejscu i wysyla ja do miasta, nie patrzac, ze spi (N: 435 z 551
   probek). Reszta to lordowie: nasza "godzina snu" czasem wygasa przed nasza kontrola i AI lorda od razu wybiera cel, do tego rozkazy innych modow (i nasz zjazd
   turniejowy GrandTourney o polnocy). Lordowie spiacy w osadzie maja te sama luke.
   Po zmianie sen trwa do switu, a rozkaz wydany spiacemu czeka do rana - o swicie lord (i karawana) jedzie tam, dokad go ostatnio wyslano. "AI placi za nieprzespana noc"
   juz dziala (od 10.10, te same kary co u Ciebie - L:35498). Czlonek armii w drodze na zbiorke spi jak samotny lord - dokladamy tylko pomiar, zeby to bylo widac.
4. **Namioty i ogniska, gdy ktos napadnie Twoj oboz** - 09.10 powiedziales "wlaczyc", a dzis sa dalej wylaczone (w kodzie i w Twoich ustawieniach MCM). Wlaczamy;
   (v2) najpierw poprawka eksperymentu: dzis ognisko stanelby dokladnie w miejscu gracza, a namioty z fizyka miedzy rozstawionym wojskiem (AI by w nich grzezlo) -
   stawiamy je bez fizyki i obok, nie pod nogami. Sprawdzisz w swojej grze (autotest nie gra bitew w obozie gracza).
5. **7.7 - turnieje i sojusze.** Przy dlugim roku turniej moze wystartowac tylko w 3 z 13 tygodni kazdej pory: w tescie przez 61 dni (doby 36-96) nie bylo zadnego
   turnieju (L:10932-28898). Po zmianie kazde miasto znow co trzeci tydzien - tak czesto "na dzien" jak w zwyklej grze. Sojusz gry trwa dzis 84 dni, wezwanie do wojny 42
   (w zwyklej grze to rok i pol roku) - robimy rok 364 i pol roku 182. (v2) Rozejm po pokoju w grze to 20 dni (w zwyklej grze mniej wiecej pora roku) - robimy
   ok. 87 dni (ta sama zasada: czas kalendarza x 364/84). "Pokoj i wojna Myr-Tyrosh po 3 dniach" to nie blad Diplomacy: Pentos nie mial na rate wezwania,
   Tyrosh wyszedl z wojny, po 3 dniach Pentos wezwal go znowu (L:28628, L:31758, L:32798).
6. **Twoje ustawienia Diplomacy** sa pod rok 84 (min. wojna 21 dni, przerwa 21, sojusz 42, pakt 84) - przerwy 21-24 dni miedzy pokojem a nowa wojna w tescie to wlasnie one.
   To Twoj plik - pytanie 3.
7. **7.8-7.9 - wioski.** Dane 2 446 wiosek (zajecie, wiara, historia) pasuja do mapy w grze w 100%; rycerzy trzeba przeliczyc na obecny uklad wiosek i zrobic 13 poprawek
   z 08.10. Potem dymek: herb pana, "Kept for them by Ser ...", wiara wedlug BannerKings na zywo (dymek nie moze przeczyc BK), po Alt - czym zyje, historia i pamiec wojny.
   Menu okregu "Look over the district" z herbem przy kazdej wiosce (jedno miejsce na liste wiosek). Proporczyki nad wioskami (z Twoich "wszystkich 8") - na koniec,
   z wylacznikiem; zostana wylaczone, jesli zjedza ponad 5% klatek.
8. **7.10 - plonaca wioska.** Dzis ani gra, ani zaden mod nie mowi Ci, ze pali sie Twoja wioska. Bedzie: "Smoke over X: Y is burning your village in the Z district",
   po koncu rabunku "Y has plundered X: N of M settlements burned" i - dopiero gdy splonie polowa osad (ten sam stan co dymek i menu, v2) - "X has been burned by Y"
   + pamiec (kto, kiedy, ilu ucieklo i zginelo) w dymku. Wazne: w 120 dobach testu nie splonela do polowy ZADNA wioska (W:160-842, L:389-35852 -
   346 pozarow, 0 spalonych), wiec wyglad spalonej wioski i napisy nad spalonymi zobaczysz dopiero razem z rabunkiem osada po osadzie (pkt 9).
9. **7.11 - rabunek osada po osadzie.** Jedna osada to dni (lord ze 112 ludzmi ok. 4-5 dni), AI tak samo, Ty z okienkiem po kazdej osadzie ("Raid the next settlement
   (about N days)" / "End the raid"). Lup tylko z tego, co wies naprawde ma (kiesa wsi, magazyn, spichlerz) - dzis gra daje z niczego ok. 670 zl dziennie (80 767 zl w 120 dob,
   L:634-36086) i do tego towar z niczego. W pelni tylko w nowej kampanii; stary zapis sie wczyta.
10. **7.12 - Bliznaki.** Dzis liczymy tylko wejscia do miasta (503 w 120 dob, L:556-36012), a nie przejscia mostem. Dokladamy licznik przejsc (karawany, wozy, rody
    wlasne i obce, Ty) z "myto, gdyby bylo". Samo myto - pozniej, z liczbami (pytanie 4).
11. **7.5 - drogi.** Proba drog (dwa kawalki mapy: Fairmarket i Winterfell) jest gotowa od 08.10 na osobnej galezi, ale nikt Ci jej nie pokazal. Pojedzie w tescie
    zbiorczym (osobny krotki bieg na zdjecia, ten sam DLL - v2), dostaniesz zdjecia i wtedy powiesz "ile drog". Premia +15% po drodze: 09.10 powiedziales "tak",
    ale 12.10 "zostaw predkosc" - a premia przyspiesza swiat (v2: kolumny piesze lordow ok. 0% - i tak jada pod sufitem marszu; jazda do ok. +5%; karawany i wozy wsi
    ok. +8%, bo sufit marszu ich nie dotyczy). **Rada: drogi tylko do patrzenia, premia w MCM - domyslnie wylaczona** (pytanie 1).
12. **Kolejnosc i koszt.** Kod calego etapu 7 (bez widoku drog i premii - te po tescie i Twoich odpowiedziach) teraz, razem z etapami 5, 6 i 8; jeden test zbiorczy na koncu.
    Etap 7 doklada ok. 0.03-0.05 s na dobe gry [S] (v2: z probkami pomiarowymi; doba dzis 11.7 s nowej kampanii) - ponizej 0.5%. Kazda paczka ma wlasny wylacznik w MCM.
13. **Cztery pytania** (rozdz. 6): premia za droge; ile drog (po zdjeciach); Twoje ustawienia Diplomacy na rok 364; czy myto w ogole (po liczniku). Reszte decyduje projekt.

---

## 1. Co dzis (liczby)

Z33 (L:N, N:N, W:N, CS:N), 120 dob nowej kampanii. "Cel" - propozycja projektu (rozdz. 4).

| Pozycja | Dzis | Cel etapu 7 | Dowod |
|---|---:|---|---|
| **tempo marszu: partie > 300 ludzi albo wodzowie armii, srednia median dobowych km/dobe** | **138 / 137 / 134 / 131** (d1-30 / 31-60 / 61-90 / 91-120); czysto konne 212 / 210 / 210 / 225 | bez zmian: d91-120 w 124-138 | L:382-8928, L:9220-17849, L:18150-26833, L:27132-35849 (`SCR\e7P\marsz.py`) |
| WorldPace (Jeff) | 75% (kod domyslnie 50) | bez zmian | L:382; Armoury.json:118; K:Settings.cs:370 |
| audyt predkosci gracza: teren | `Plain` 120 / 120 dob (takze 10 innych logow) | - (pomiar 7.4 musi isc po partiach AI) | L:381-35848; RA7 1.1 |
| kary terenu Jeffa | las -0.1, pustynia -0.2, snieg -0.2, brod -0.5, noc -0.5 (6 modeli) | bez zmian (liczby Jeffa) | L:132; Armoury.json:73, :79, :89 |
| **zwrot kary, ktorej gra nie nalozyla (las / pustynia)** | **brak pomiaru**; z kodu: Polnoc w lesie ok. +12%, Dorne na pustyni ok. +5% wobec rowniny [S] - (v2) tylko partie ponizej sufitu `MarchPace` (jazda, karawany, wozy wsi, male partie); kolumny piesze na suficie (AI sr. 3.04 jedn./h, L:36062) - nadmiar obciety | 0; ponizej sufitu las/pustynia wolniej niz rownina | K:TerrainEase.cs:56-60, :79, :84; K:MarchPace.cs:95-148; G DefaultPartySpeedCalculatingModel.cs:264-300; RB7 3.1 |
| oboz swiata 0-6: spi na godzine (lordowie / karawany / wodzowie armii) | 599 / 862 / 877 / 848 (431 / 418 / 5.9 w d91-120) | bez zmian | N:2-2837; L:308 |
| **obudzeni cudza reka na noc** | **56 / 91 / 97 / 93** (razem 10 960 w 120 nocy); 2/3 "rozkaz jak nasz zapamietany", 1/3 "inny"; (v2) w probce logu (co 20. obudzenie) **karawany 435 / 551 (79%)**, lordowie 116 (21%) | razem <= 10, lordowie <= 3 | N:2835; N (linie "obudzony cudza reka"); RA7 1.2; RB7 5.2 |
| zjazd obudzonego (sr. / maks.) | 0.40 / 1.00 jedn. | sr. <= 0.15 | N:2817-2837; RB7 5.2 |
| samotni lordowie w ruchu w oknie obozu BEZ POWODU | ok. 20 na noc (2 435 partio-godzin w 120 nocy) | <= 3 | N (linia "AiNightCamp: ruch"); RB7 5.2 |
| dlug snu AI (nowy 1/2/3 na dobe; dlug 3) | 10 / 1 / 0 (d120); dlug 3 = 0 przez caly bieg; kara -25% / -40% zgodna z tablica 3/3 | bez zmian | L:35797, L:35498, L:35798 |
| czlonkowie armii w drodze na zbiorke | **brak pomiaru** (w sciezce T10 licza sie jak samotny lord - K:NightMarch.cs:783-850) | linia E7/0, spia | RA7 1.2; RB7 5.1 |
| koszt petli obozu | 9.0 ms na tick (d91-120), ok. 54 ms/dobe | bez wzrostu (optymalizacja - 8.17) | L:306-35797 |
| namioty i ogniska przy napadzie na oboz gracza | WYLACZONE: `CampBattlePropsEnabled = false` w kodzie i w Armoury.json Jeffa | wlaczone (Jeff M, 09.10) | K:Settings.cs:1140; Armoury.json:302; SP:841 |
| **wyplaty nagrod turniejow gry** | **72 w 120 dob; doby 36-96: 0 przez 61 dob**; tygodnie sezonu 5-12: 0 zdarzen | zadne okno >= 14 dob bez turnieju | L:620-36077, L:10932-28898; RB7 6.1 |
| sojusz / udzial w wojnie sojusznika (gra) | 84 / 42 dni | 364 / 182 | G DefaultAllianceModel.cs:112, :114 |
| (v2) przerwa pokoj -> propozycja wojny gry (rozejm gry) | > 20 dni | > ok. 87 dni (20 x 364/84) | G KingdomDecisionProposalBehavior.cs:242 (`GetRandomWarDecision`); BK postfiks tej metody (BK DiplomacyPatches.cs:69-115) |
| wezwania do wojny (168) | 9 w 120 dob; Myr-Tyrosh: zerwane z braku raty -> pokoj -> po 3 dniach nowe (cena 5 035 -> 200 217 zl) | bez zmian regul (rata dzienna ta sama) | L:28628, L:31758, L:32798 |
| pokoj -> nowa wojna tej samej pary | 24 dni (Qarth/Volantis), 21 (Stormlands/Vale), 3 (Myr/Tyrosh - wezwanie 168) | po decyzji Jeffa (pytanie 3) | CS:3334-3790, CS:3467-3873, CS:4101-4158 |
| ustawienia Diplomacy Jeffa (min. wojna / przerwa / sojusz / pakt / wojna domowa / frakcja) | 21 / 21 / 42 / 84 / 240 / 120 (domyslne, rok 84) | pytanie 3 | DiplomacySettings_v1.2.json:27, :28, :31, :33, :52, :53 |
| wojen naraz | 2 (d1) -> 14-17 od d30 | nie mniej niz polowa po 7.7 | CS:1593-4359 |
| **dane lore wiosek w grze** | brak pliku `arm_map_village_lore.tsv`; B pasuje 2 446 / 2 446, rycerze A tylko 687 / 2 446 | 2 446 / 2 446, bramka 5A8C2735 | RC7 2.1; crc ukladu w L (linia startowa ksiegi) |
| dymek wioski | linia pana na zywo (T7), stan z ksiegi, "X is putting it to the torch"; bez herbu | herb, rycerz, wiara BK, Alt | K:MapVillagesView.cs:4452, :4604-4635 |
| komunikat "plonie Twoja wioska" | brak w grze, ROT, BK, BKROT, Diplomacy, BEE; teksty `VilLog*` sa, nieuzywane | komunikat z napastnikiem | K:VillageTexts.cs:25-31; RC7 4.1 |
| pozary wiosek / wioski "spalone" | 346 pozarow (sprawca w logu przy kazdym) / **0 spalonych**, plonie naraz maks. 10, pustych osad 97.6 | spalone > 0 (z 7.11) | W:160-842; L:389-35852, L:36104 |
| rabunki: sesje / trafieni / zabici / uchodzcy | 656 / 24 676 / 824 / 23 010 (93%) | trafieni = zabici + las + jency + uchodzcy | L:389-35852, L:1517-36096 |
| **lup gry z niczego (zloto za hearth)** | **80 767 zl / 120 dob** (ok. 670 zl/d, rosnie z wojna; gracz 0) | 0 | L:634-36086 (linia "Hearth (E4/0)"); G RaidEventComponent.cs:179-188 |
| towar z rabunku z niczego (dochod produkcji, "common loot") i niszczony towar wsi | niezmierzone; gra: lup z magazynu wsi z 50% zniszczen, plus towar z niczego | 0 z niczego; zniszczone - ujscie z przyczyna | G RaidEventComponent.cs:204-264, :273-299 |
| rabunki zakonczone (udane / przerwane) | 308 (75 / 233) | udzial przerwanych - odczyt | RC7 5.1 (linie "Ludzie (spustoszenie)", L:36096) |
| **Bliznaki: przejscia mostem** | **brak licznika**; wejscia do miasta 503 (4.3/d; karawany 68%) | linia dobowa przejsc | L:556-36012 |
| myto przeprawy | 0 (okno ksiegi "myto 17020/0") | - (po liczniku i decyzji) | L:36056 |
| `CrossingLaw` zawracanie wrogich lordow | 0 linii w 120 dob - petla AI biegnie tylko, gdy GRACZ stoi we wrogiej strefie | etap 8 (osobna paczka) | K:CrossingLaw.cs:73-76 (`return` przed :100 `AiTick`) |
| proba drog 1.13 | gotowa na galezi `w-toku/p2-drogi-proba` 95f05be (641 stempli, 2 okna 50x50), niepokazana | zdjecia w tescie zbiorczym | git 95f05be; RAPORT-NOCNY-2026-10-09.md:60, :90 |
| doba gry (nasze mody ok. 7%) | 11.7 s nowa kampania, 18.2 s zapis z konca biegu | etap 7 <= +0.1 s | SP (WGRANIE 33); PLAN 8.17 |

**Najwazniejsze przyczyny** (RB7/RC7, sprawdzone w kodzie do tego projektu):
- **Kary terenu:** `TerrainEase.Swap` zawsze oddaje pelny procent gry (`AddFactor(+0.3)` las, `+0.1` pustynia, K:TerrainEase.cs:56-60, :79, :84), a gra w lesie naklada
  `-0.3 x (0 - ForestKin.PrimaryBonus)` = -0.15 przy zwiadowcy z ForestKin i >= 75% pieszych oraz doklada `+EffectBonus x 0.3` cechy `BattanianForestSpeedFeat`
  (G DefaultPartySpeedCalculatingModel.cs:264-283), a na pustyni przy cesze `AseraiDesertFeat` nie naklada nic (:291-295). Komentarz w kodzie (K:TerrainEase.cs:26-28) mowi
  "oddajemy pelny procent, wiec wychodza odrobine na plus; swiadomie" - to bylo 02.09; PLAN 7.4 (zatwierdzony 09.10) liczy to jako blad (+12% to nie "odrobina").
- **Budzenie spiacych (v2, po krytyce K1-01):** (a) **karawany - 79% probek** (N: 435 z 551 linii "obudzony cudza reka"): BK wylacza godzinny tick karawan gry i w swoim
  `BKCaravansBehavior.HourlyTickPartyImpl` (BK :828-839) przy `DefaultBehavior == Hold` wola `ReleaseCaravanFromHold` (:298) -> `SetMoveGoToSettlement` do celu (:367),
  bez patrzenia na `Ai.IsDisabled` - to jest "rozkaz jak nasz zapamietany" u karawan; faza snu (odnawianie terminu AI) tego nie zatrzyma, zatrzyma tylko bramka `SetMove*`.
  (b) **lordowie - 21%:** gra mysli za partie tylko przy `!Ai.IsDisabled` (G AiPartyThinkBehavior.cs:49); `DisableForHours(1)` ustawia `_enableAgainAtHour = teraz + 1 h`
  (G MobilePartyAi.cs:1561-1565), AI wraca w `MobilePartyAi.Tick` po terminie (:311-320, :437-442; co `AiCheckInterval` ok. 0.25 h); gdy termin minie przed naszym przegladem,
  partia na Hold dostaje od razu nowy cel (G AiPartyThinkBehavior.cs:94) przez `SetMove*`. Lordowie spiacy w osadzie (`_townHold`, K:NightMarch.cs:735-779) maja te sama luke.
  Straznik `HoldSleepers` kladzie z powrotem dopiero po fakcie (K:NightRest.cs:414-468, co 0.1 h gry - :1334).
- **Turnieje:** `GetTournamentStartChance` = 0, gdy `|hash(miasto) % 3| != GetWeekOfSeason` (G DefaultTournamentModel.cs:27); przy 13 tygodniach w porze start mozliwy w tygodniach 0-2.
  Czynny model NavalDLC przekazuje te metode do `BaseModel` = Default (RB7 6.1, CS:1514).
- **Sojusze:** `MaxDurationOfAlliance => Days(84)`, `MaxDurationOfWarParticipation => Days(42)` (G DefaultAllianceModel.cs:112, :114); cena wezwania x dni (:569, :583);
  `CrownCallToWar.Days()` czyta to samo (K:CrownCallToWar.cs:60).
- **Wioska nie zostaje spalona:** prog ksiegi 50% osad, a przy tempie E4/5 jeden rabunek trafia ok. 57 ludzi (RC7 5.4) - pusta osada to 250 ludzi (`DistrictSettlementPeople`).
- **Lup z niczego:** zloto za hearth (G RaidEventComponent.cs:179-188), towar z dochodu wsi i "common loot" bez magazynu (:273-299); towar z magazynu wsi idzie do napastnika
  z prawdopodobienstwem 0.5 x mnoznik, reszta znika (:204-264). E4/5 liczy tylko ludzi (K:Devastation.cs:330-391).
- **Bliznaki:** licznik `Measure169c.OnSettlementEntered` liczy wejscia do osady (K:Measure169c.cs:891-929), nie strone rzeki.

---

## 2. Reguly (jedna na zjawisko)

**Zasady wspolne** (z etapow 1-6 i decyzji Jeffa): kazda paczka z wlasnym wylacznikiem MCM (wylaczony = stan sprzed paczki); wyjatek per obiekt - licznik potkniec, bez globalnych
wylacznikow (CLAUDE.md 7, `_tentBroken`); logika gry tylko na zegarze gry; widok mapy przebudowywany tylko przy zmianie stanu, nigdy co klatke, bez odczytu nazw prefabow
(AccessViolation 08.10, `MapVillagesView.cs:1268-1275`); zadnego zlota ani towaru z niczego i w nicosc (kazde ujscie ma przyczyne); jedna regula gracz i AI; NK (7.11) wlaczone
domyslnie, w starym zapisie wczytuje sie bez bledu (brak sekcji = stan pusty).

| # | Zjawisko | Regula | Dzis | Paczka |
|---|---|---|---|---|
| R1 | **tempo marszu** | bez zmian: WorldPace Jeffa (75%), sufit kolumny `MarchPace`, kary terenu Jeffa w jednostkach mapy, podloga gry 1.0, `Estimated*Speed` gry; zadna paczka etapu 7 nie zmienia predkosci poza R2 (naprawa) i R3 (tylko po TAK Jeffa) | jak jest (12.10) | - |
| R2 | **kary terenu** | gra naklada kare procentem; my oddajemy DOKLADNIE to, co gra nalozyla netto (`vf - feat`, te same warunki co gra: perk ForestKin zwiadowcy przy >= 75% pieszych, cecha lasu, cecha pustyni) i kladziemy plaska kare Jeffa w tym samym ulamku, ktory gra zostawila (`kara x (vf - feat) / 0.3`; pustynia z cecha 0); cecha zmniejsza kare, nigdy nie daje premii; perki DesertBorn, Pathfinder, NightRunner, DayTraveler - osobne premie gry, zostaja | pelny zwrot zawsze | E7/T |
| R3 | **droga** | droga to WIDOK (gdzie wioska, tam droga - po probie 1.13 i decyzji "ile"); premia za droge tylko po TAK Jeffa: jedna maska drog, +15% biezacego wyniku (`Add(0.15 x wynik / (1 + suma))`), poza lasem i brodem, dla WSZYSTKICH partii, domyslnie wylaczona | brak drog i premii | E7/D0, D1, D2 |
| R4 | **czas logiki** | kazda decyzja gry na zegarze gry (`CampaignTime`), nigdy sekundy realne ani klatki; wyjatek: odswiezanie widoku i dlawienie komunikatow | `HideoutPurge` 3 s realne (K:HideoutPurge.cs:135-139); `CrossingLaw` klatki (etap 8) | E7/C |
| R5 | **sen partii** (v2) | jedna funkcja `NightRest.SleepKind(mp)`: oboz (partia w `_bedPos` w godzinie obozu; `_bedPos` czyszczone zawsze razem z `_camping`), dlug (`NightMarch._debtSleep`), osada (`_townHold`), brak. Spiacy w obozie albo w snie dlugu nie przyjmuje rozkazu ruchu: rozkaz zapisuje sie TAM, GDZIE TRZYMA GO DANY SEN - oboz: `_orders[mp]` (oddaje swit, `ApplyOrder`), dlug: `_debtSleep[mp].Order` (oddaje `ReleaseDebt` przy dlugu 0, nie swit). Osada: bez bramki (gra nie wypusci partii z wylaczonym AI; rozkaz czeka w partii do switu). Wlasne AI spiacego (oboz, dlug, osada) wylaczone do konca snu (odnawiane w przegladzie co 0.1 h, gdy zostalo < 0.6 h); o swicie `EnableAi` dla obozu i osady. Wyjatki: bitwa (`MapEvent`), alarm T10 (zdejmuje ze snu, bez bramki), gracz, nasze oddanie rozkazu; `SetMoveModeHold`, `SetMoveToNearestLand`, `SetMoveGoToInteractablePoint` zawsze przechodza | straznik po fakcie (91/noc) | E7/O1 |
| R6 | **zbiorka armii** | niedoczepiony czlonek armii = samotny lord (sciezka T10 `LordNight`) - spi; bez nowej reguly, tylko pomiar | bez pomiaru | E7/0 |
| R7 | **nieprzespana noc AI** | T10 (WGRANIE 19): dlug 1-3, te same tablice kar co gracz, marsz nocny tylko z powodu, najwyzej 2 noce forsowne - bez zmian | dziala | - |
| R8 | **czas w modelach gry** (v2) | JEDNA zasada: stala gry, ktora w kalendarzu 84 oznacza czas KALENDARZA (rok, pol roku, pora - niezaleznie od tego, czy zapisana jako `Days(...)` czy `Years(...)`), liczona przez kalendarz Armoury (x `Calendar.DaysPerYear`/84): sojusz `Days(84)` -> 364, udzial w wojnie sojusznika `Days(42)` -> 182, przerwa po pokoju przed propozycja wojny gry 20 -> ok. 87; cykl turniejow miasta = co 3 tygodnie (tydzien = `(int)ToWeeks % 3`, przy 3 tygodniach w porze dokladnie gra); czasy TRWANIA zjawisk (150 dni niecheci, trybut 100 dni) zostaja dniami; rozejmy BK (`YearsFromNow`) licza juz rok 364; ustawienia Diplomacy Jeffa - R9 (pytanie 3) | 84 / 42 / 20; tygodnie 0-2 z 13 | E7/K |
| R9 | **ustawienia cudzych modow w dniach** | przeliczane tym samym rokiem (x 364/84), tylko za zgoda Jeffa (jego plik), gra zamknieta, kopia `.bak` + D: | Diplomacy pod rok 84 | pytanie 3 |
| R10 | **stan wioski** | JEDEN: ksiega wiosek `VillageBook.StateOf` (spalona = >= 50% osad, histereza 3%, odbudowa rok); zdarzenie gry (`VillageStateChanged`) tylko dla komunikatow chwili (plonie / spladrowana); zadnych drugich progow; (v2) slowo "burned" w komunikacie i pamieci tylko przy stanie ksiegi `SBurned` (przejscie w `CheckUp`), przy samym Looted gry - "plundered" z liczba spalonych osad | ksiega jest (E4/11) | wszystkie W |
| R11 | **dane lore wiosek** | jeden plik `arm_map_village_lore.tsv`, jedno wczytanie na sesje (`MapVillageLore`), bramka = crc ukladu ksiegi (uid:osady, K:VillageBook.cs:214-217; dzis 5A8C2735), zly uklad = lore wylaczone, dymek jak dzis | brak | E7/L, W2 |
| R12 | **pan, rycerz, wiara wsi** | pan i herb na zywo z gry (`Clan.Banner`); rycerz: posiadacz tytulu wsi z BK, gdy jest i jest kim innym niz glowa rodu pana, inaczej rycerz z lore; wiara: z BK na zywo (udzial), zdanie lore o swietym miejscu tylko gdy klucz wiary lore = dominujaca wiara BK | pan na zywo | W2 |
| R13 | **lista wiosek okregu** | jedno okno: "Look over the district" (z herbami); "People of this land" - naglowek i podsumowanie okregu; wiersze z jednej funkcji stanu (`VillageBook.MenuSection`) | sekcja w "People of this land" | W3 |
| R14 | **pamiec wojny** | w ksiedze wiosek: ostatni napastnik, doba, ilu ucieklo, zginelo, licznik spalen - zapis tylko przez `VillageBook.Export/Import` (jeden pisarz klucza `arm_district`, K:ArmouryBehavior.cs:461); (v2) w zapisie `StringId` bohatera-wodza i rodu (sprawdzone `BadId`), nigdy wyswietlana nazwa - nazwa na zywo przy budowie dymka, brak bohatera = tekst zastepczy EN; "Burned by" tylko przy przejsciu na `SBurned`, przy splondrowaniu "Plundered by" | tylko doba w ksiedze | W1 |
| R15 | **rabunek** | osada po osadzie: krok = m osad, czas w dobach z ludzi napastnika (`DevastationPerRaiderDay`); ludzie przez JEDNA funkcje Devastation (`Strike`/`StrikePeople`) i (v2) JEDEN stan rabunku E4/5 (`RaidState` w `_raids`: zabici, jency E4/9, klucz sesji ksiegi `Comp`); lup tylko z zasobow wsi (kiesa wsi tym samym platnikiem co E5/A7, magazyn wsi, spichlerz okregu na jedzenie napastnikow); zniszczone sztuki - ujscie "spalone w rabunku"; AI jeden krok na sesje, gracz z okienkiem; kara relacji `RaidDamage` | gra (punkty, zloto i towar z niczego) | R1, R2 |
| R16 | **przeprawa** | licznik przejsc przez most (zmiana strony rzeki), bez myta; myto osobna paczka po liczniku i TAK Jeffa | wejscia do miasta | E7/B |
| R17 | **widok mapy (wioski, drogi, proporczyki)** | przebudowa tylko przy zmianie stanu, budzet na klatke, LOD wedlug kamery; licznik potkniec per obiekt; brak odczytu nazw prefabu; wlasne prefaby Armoury w `Modules\Armoury\GUI\Prefabs` - bez UIExtenderEx (ForgeView dalej jedynym uzytkownikiem) | ogien/dym przy zmianie stanu | W2, W4, W5, D1 |
| R18 | **napad na oboz gracza** | namioty, ognisko, pochodnie wokol linii gracza (`CampScene`) - wlaczone; (v2) scenografia bez fizyki (agenci nie grzezna), nic w promieniu 3 m od agenta gracza | wylaczone | E7/O2 |

**Pogodzone bez pytania Jeffa / poprawki do PLAN i rozpoznan:**

| Miejsce | Bylo | Jest (dowod) |
|---|---|---|
| PLAN:201 cel "drogi przyspieszaja; drogi przyspieszaja" | powtorzone, sprzeczne z 12.10 | cel: tempo jak jest; drogi widoczne (ile - po probie); premia - pytanie 1 |
| PLAN:218 "Winterfell - KP ok. 77 dni ... test roczny po zmianie tempa" | warunek pod 7.2 | nowe "Skonczone, gdy" (rozdz. 4); test = jeden test zbiorczy (SP:1263) |
| PLAN 7.1 "najpierw blokady tempa" (podloga 1.0, kary x tempo, `Estimated*Speed`, T-1) | do zrobienia | skreslone: warunek odrzuconego 7.2; podloga i kary x tempo zmienilyby tylko predkosc; T-1 (WorldPace jako mnoznik) daje jazdzie -7% przy 75% (a07:232) - sprzeczne z 12.10 "zostaw predkosc"; `Estimated*Speed` - rozdz. 7 (RB7 2.2). 7.1 = tylko "jedna skala czasu" (E7/C) |
| RA7 pytanie 2 (T-1) | do Jeffa | rozstrzygniete decyzja 12.10 (zmiana predkosci = nie) |
| PLAN:259 L, :260 M | otwarte | rozstrzygniete 09.10 04:25 (SP:841): L tylko nad spalonymi (E7/W4), M wlaczyc (E7/O2) |
| RC7 J2 proporczyk (poza lista PLAN 7.9) | pytanie | Jeff 08.10 "wszystkie 8" (SP:377-379) - paczka E7/W5 z wylacznikiem i progiem klatek 5% |
| RC7 J3 dwa okna z lista wiosek | pytanie | regula R13 (jedno miejsce na zjawisko) |
| RC7 J4 wyglad spalonej | pytanie | prog 50% to decyzja Jeffa (SP:760) - zostaje; wyglad i napisy w paczce po 7.11 (E7/W4), bo bez 7.11 nie ma spalonych |
| RC7 J6 nieoplacalny rabunek gracza | pytanie | zgodne z decyzja 07.10 (rabunek osada po osadzie, "AI tak samo" - SP:461-463) i zamknieta ekonomia (lup z zasobow wsi); bez pytania |
| RC7 J1 20 przykladowych dymkow | pytanie | nie blokuje: 20 dymkow w pliku `tools/wioski-lore/probka-dymkow.txt` do wgladu Jeffa (styl poprawiany w Pythonie) |
| RA7 "Myr/Tyrosh 3 dni lamie Diplomacy" | do wyjasnienia | to wezwanie 168: "porozumienie zerwane ... Tyrosh wychodzi z wojny (pokoj)" (L:31758), "Pentos wzywa Tyrosh przeciw Myr" (L:32798) - nie Diplomacy; linia E7/0 bedzie podawac przyczyne |
| RA7 "lup z niczego: 7.11 czy etap 5" | do rozstrzygniecia | zloto za hearth na drodze gry - E5/A7 "drobne zrodla gry" (AZ:116, :291: lup z kiesy wsi do jej wysokosci); 7.11 przejmuje lupienie dla swoich rabunkow i placi TA SAMA funkcja platnika; towar z niczego w rabunku (G :273-299) i zniszczenia (:204-264) - tylko 7.11 (R15) |
| RB7 7.1 "CrossingLaw na zegar gry + pomijac spiacych" | w 7.1 | petla AI martwa dla AI (K:CrossingLaw.cs:73-76 przed :100) - zmiana zegara martwej petli nic nie daje, a jej ozywienie zmieni gre (CLAUDE.md 8 pkt 4) -> cala `CrossingLaw` do etapu 8; spiacych chroni R5 (blokada `SetMove*` obejmie tez `CrossingLaw.AiTick` K:CrossingLaw.cs:146, gdy ozyje) |
| RB7 "proba 1.13 nie zrobiona" | - | jest: galaz `w-toku/p2-drogi-proba` 95f05be (`MapRoadsProbe.cs` 652 linie, `arm_map_roads_probe.tsv` 641 stempli, `Prefabs/arm_map_roads.xml`), zablokowana 09.10 i niepokazana (RAPORT-NOCNY:60, :90) |
| RB7 tempo "117-124 km/dobe" | prog +-5% | z Z33: srednie median dobowych 138 / 137 / 134 / 131 (L:382-35849) - prog wedlug okna d91-120 |
| a07:191-193 "AiNightsAwakeInChase martwy" | blad | zyje (K:NightMarch.cs:504) - nieaktualne |
| WL-P P6 kolumna `lands` | dane | kod liczy na zywo (`LandsText`, T7) - kolumna zbedna, generator jej nie zapisuje (jedno zrodlo) |
| WL-P bramka `layout_crc32` (crc calego pliku) | bramka | crc ukladu ksiegi (R11) - crc pliku zmienia linia "# scene_xml_crc" i odrzucilaby dobre dane (RC7 2.1) |
| WL-P "sekcja m: z osobnej klasy WarMemory" | drugi pisarz | przez `VillageBook.Export/Import` (R14) |
| RB:174, :12 zabici 5%, "50 ludzi ok. 10 dni na osade" | liczby paczki 113 | E4/5: zabici 2% (przy niewoli 5%) (L:122) - kroki przeliczone w R1 |
| AUDYT-KALENDARZ.md:169 "3 z 6 tygodni" | rok 168 | "3 z 13" (rok 364), potwierdzone L:10932-28898 |
| K:TerrainEase.cs:26-28 "swiadomie na plus" | komentarz | PLAN 7.4 "blad" - komentarz poprawiony w E7/T |
| K:Settings.cs:370 opis `WorldPacePercent` "lore-true month of the 168-day calendar" | tekst MCM w grze | poprawiony w E7/C (rok 364, bez obietnicy "miesiac lore") - domyslnej 50 i wartosci Jeffa 75 nie ruszamy |
| (v2) PLAN 7.7 "rozejmy licza czas jak reszta swiata" | w v1 cicho pominiete (R8 zostawial 20 dni) | R8 v2: przerwa po pokoju przed propozycja wojny gry 20 -> ok. 87 dni (E7/K pkt 3); rozejmy BK juz w roku 364 (`YearsFromNow`); Diplomacy - pytanie 3 (plik Jeffa); prog w rozdz. 4 |
| (v2) rozdz. 0 pkt 3 v1 "2/3 obudzen to wlasne AI lorda" | blad diagnozy | 79% probek to karawany budzone przez BK (`ReleaseCaravanFromHold`), lordowie 21% (krytyka K1-01) - E7/O1 przebudowana |

---

## 3. PACZKI etapu 7 w kolejnosci

Numeracja robocza 224-241 (etap 5: 195-205, etap 6: 206-223; przed kodem grep CHANGELOG/STAN-PRAC/docs - jesli "drobne zrodla gry" E5/A7 dostana numer wczesniej, przesunac).
Kazda paczka: osobny commit, wpis CHANGELOG (z "Ryzyko / co sprawdzic"), wlasny wylacznik, autor + recenzent; `McmSettings.cs` generowac `python tools/gen_mcm.py` po kazdym
scaleniu (nie scalac recznie). Wielkosci: S <= 1 noc pracy, M, D. Galaz: `w-toku/e7` od glowy `w-toku/e6-p3` po jej zamknieciu (wspolne `Settings.cs`, `ArmouryBehavior.cs`,
`tools/sprawdz_logi.py`), a gdy porcja 3 etapu 6 jeszcze trwa - od `w-toku/e5-wsie` z jednym scaleniem na koncu. Pozostale pliki etapu 7 (TerrainEase, NightRest, NightMarch,
VillageBook, MapVillagesView, Devastation, Measure169c, HideoutPurge, CampScene) nie sa celem zadnej paczki E6 (projekt E6 rozdz. 5.1 - E6/K0 tylko czyta predkosc w `MarchPace`).

| Paczka | Nr | PLAN | Co | Wiel. | NK | Zalezy od | Kod | Test |
|---|---|---|---|---|---|---|---|---|
| **E7/0** | 224 | 7.6, 7.7 (pomiar) | POMIAR: wojna i pokoj z przyczyna, zbiorka w nocy, turnieje wedlug tygodnia pory; grupa `etap7` w narzedziu | S | nie | - | teraz | test zbiorczy |
| E7/T | 225 | 7.4 | kary terenu dokladnie jak gra (R2) + probka godzinna AI | S | nie | - | teraz | jw. |
| E7/K | 226 | 7.7 | turnieje co 3. tydzien, sojusz 364 / wezwanie 182, (v2) przerwa po pokoju ok. 87 dni (R8) | S | nie | E7/0 (linia turniejow) | teraz | jw. |
| E7/C | 227 | 7.1 | jedna skala czasu: odwet kryjowek na zegarze gry; tekst MCM `WorldPacePercent` i komentarze 168 -> 364 (R4) | S | nie | - | teraz | jw. |
| E7/O1 | 228 | 7.6 | sen do switu: AI odnawiane w przegladzie (oboz, dlug, osada), rozkaz dla spiacego czeka tam, gdzie trzyma go jego sen (R5; v2) | S | nie | E7/0 (licznik w tym samym `NightMarch`) | teraz | jw. |
| E7/O2 | 229 | 7.6 (Jeff M) | namioty i ogniska przy napadzie na oboz gracza - (v2) bez fizyki, nie pod nogami gracza (R18) | S | nie | - | teraz | test Jeffa (misja) |
| E7/L | 230 | 7.8 | dane lore 2 446 wiosek: rycerze od nowa na v4, 13 poprawek, jeden plik z bramka ukladu (R11) | M (Python) | nie | - | teraz, rownolegle (poza gra) | kontrole L0 |
| E7/B | 231 | 7.12 | licznik przejsc mostem Bliznakow, "myto gdyby bylo" (R16) | S | nie | - | teraz | test zbiorczy |
| E7/W1 | 232 | 7.10 (a) | komunikat "plonie Twoja wioska" / "spalona przez", pamiec wojny w ksiedze (R10, R14) | S | nie | - | teraz | jw. + bieg zapisu |
| E7/W2 | 233 | 7.9 (a) | dymek: herb pana, rycerz, wiara BK, Alt (zajecie, historia, pamiec wojny) (R11, R12) | S-M | nie | E7/L, W1 | po E7/L | jw. (zdjecia) |
| E7/W3 | 234 | 7.9 (b) | menu okregu "Look over the district" z herbami; "People of this land" - podsumowanie (R13) | S | nie | W2 (`MapVillageLore`) | po W2 | jw. |
| E7/R1 | 235 | 7.11 (a) | rabunek osada po osadzie dla AI: przejecie lupienia (Priority.First, wspolny `RaidState` E4/5), czujnosc, lup z zasobow wsi z niezaleznym bilansem, kara relacji (R15; v2) | D | **tak** | E5/A7 (platnik lupu), W1 (sekcje `arm_district`) | po E5/A7 i W1 | jw. |
| E7/R2 | 236 | 7.11 (b) | okienko gracza po kazdej osadzie, "End the raid" (R15) | M | tak | R1 | po R1 | test Jeffa |
| E7/W4 | 237 | 7.10 (b) | wyglad czesciowo pustej i spalonej wioski, napisy nad spalonymi (R10, R17; Jeff L) | S | nie | R1 (zeby byly spalone), W1 | po R1 | jw. (zdjecia) |
| E7/W5 | 238 | 7.9 (c; Jeff 08.10 "wszystkie 8") | proporczyk pana nad wioskami z bliska (R17) | S | nie | W2 | ostatnia | jw. (klatki) |
| E7/D0 | 239 | 1.13 -> 7.5 | proba drog P2 przeniesiona na drzewo etapu 7, (v2) w tym samym DLL, wlaczana kluczem tylko w krotkim biegu zdjec - zdjecia dla Jeffa (R3) | S | nie | - | teraz (przeniesienie) | zdjecia w tescie zbiorczym |
| E7/D1 | 240 | 7.5 | widok drog (siec na ukladzie v4, decale z budzetem, Valyria) (R3, R17) | D | nie | D0 + decyzja "ile drog" (pytanie 2) | **po tescie zbiorczym** | osobny |
| E7/D2 | 241 | 7.5 | premia za droge (maska, postfiks, latka cache) - **tylko po TAK Jeffa** (pytanie 1), domyslnie 0 | M | nie | D1 | po D1 i odpowiedzi | osobny |

**Wspolne pliki etapu 7:** `NightMarch.cs` (E7/0 licznik zbiorki i E7/O1 faza - E7/0 pierwszy); `NightRest.cs` (tylko E7/O1); `VillageBook.cs` (W1 sekcja pamieci `m:`, R1 sekcje
`r:`/`c:` zarezerwowane od E4/11 - E411:201; kolejnosc W1 -> R1); `MapVillagesView.cs` (W2 `OnHover`, W4 obrazek i napisy, W5 proporczyki - kolejnosc W2 -> W4 -> W5); `Devastation.cs`
(W1 tylko odczyt `RaidState`; R1 (v2): straznik `DistrictRaid.Owns` na samej gorze `RaidPrefix` :330, w `EndPrefix` :405 pomija tylko `Settle` i podmiane hearth
(nie `StateOf`), dostep `internal` do `StateOf`, wspolny rdzen `Strike` od "trafionych" w dol dla `Strike` i `StrikePeople`); `VillageBook.cs` (W1 - punkt przejscia na spalona w `CheckUp`); `ArmouryBehavior.cs` (rejestracje i `Reset`
w konstruktorze jak E5/0 / E6/0). **Pulapka MCM** (CLAUDE.md 7) - w `Armoury.json` Jeffa SA (12.10): `TerrainEaseEnabled` true, `ForestSpeedPenalty` 0.1, `DesertSpeedPenalty` 0.2,
`NightSpeedPenalty` 0.5, `WorldPacePercent` 75, `HideoutPurgeEnabled`, `CrossingLawEnabled`, `CrossingLawAi`, `AiCampsAtNight`, `NightRestEnabled`, `SpeedAuditEnabled`,
**`CampBattlePropsEnabled` false (:302)** - domyslnych z tej listy NIE zmieniamy, poza `CampBattlePropsEnabled` (decyzja M - wpis w pliku przy wgraniu, gra zamknieta, kopia `.bak`);
kazda nowa regula ma nowy klucz.

---

### E7/0 - Pomiar etapu 7 (224, tylko log) - S - KOD TERAZ

**Cel.** Trzy rzeczy, ktorych dzis nie widac, a ktore sa progami etapu (rozdz. 4): przyczyna kazdej wojny i pokoju (RA7 nie umialo powiedziec, kto skrocil przerwe Myr-Tyrosh),
zbiorka armii w nocy (RA7 1.2 "brak jakiegokolwiek pomiaru"), turnieje wedlug tygodnia pory (dzis tylko posrednio przez wyplaty nagrod). Gra bez zmian: zadna latka nie zwraca
false, nie zmienia wyniku, rozkazu ani zlota.

**Pliki**
| Plik | Zmiana |
|---|---|
| NOWY `WorldClock7.cs` | sluchacze `CampaignEvents.WarDeclared` (G CampaignEvents.cs:687, z `DeclareWarDetail`) i `MakePeace` (:753, z `MakePeaceDetail`); slownik para -> doba ostatniego zdarzenia (pamiec sesji); `TournamentStarted` (:685) - licznik dnia, doba ostatniego startu; `Reset()` |
| `CrownCallToWar.cs` | `[ThreadStatic]` przyczyna "wezwanie 168 zerwane" ustawiana wokol `MakePeaceAction.Apply` (K:CrownCallToWar.cs:222) - linia E7/0 podaje przyczyne zamiast golego `Default` |
| `NightMarch.cs` | w `LordNight` (K:NightMarch.cs:783-850) osobny licznik czlonka armii niedoczepionego (`mp.Army != null && mp.AttachedTo == null && mp.Army.LeaderParty != mp`): spi / idzie z powodem / idzie bez powodu, partio-godziny |
| `ArmouryBehavior.cs` | rejestracja sluchaczy, `WorldClock7.Reset()` w konstruktorze, linia dobowa po `MoneyLedger.Daily` |
| `tools/sprawdz_logi.py` | `--grupa etap7` (szkielet z blokami `etap7_<paczka>` jak etap4/etap5; progi rozdz. 4) |

**Okna (dowod):** zdarzenia gry - tylko odczyt (wzor `ArmsPricing.OnWarDeclared`, K:ArmouryBehavior.cs:672, K:ArmsPricing.cs:387); `LordNight` liczy juz `LoneField`/`LeadField`
(K:NightMarch.cs:795) - nowy licznik w tej samej galezi.

**Linie logu:**
- `Wojna i pokoj (E7/0): dzien N | <A> - <B>: WOJNA (przyczyna <DeclareWarDetail>) | dni od poprzedniego zdarzenia tej pary D` / `... POKOJ (przyczyna <MakePeaceDetail> | wezwanie 168 zerwane)`;
  dobowo: `| par z nowa wojna < 21 dni po pokoju: n (przyczyny)`.
- `Zbiorka (E7/0): noc N | czlonkowie armii w drodze do wodza w oknie obozu: partio-godzin a, spia b (x%), ida z powodem c, ida bez powodu d | wodzowie armii obozuja e/f`.
- `Turnieje (E7/0): dzien N, tydzien pory w (0-12) | rozpoczete dzis k [miasta], w toku m, dni od ostatniego startu s`.

**Klucz:** `Stage7MeasureLog` = true (`// counts wars and peaces with their cause, army members riding to the muster at night and tournaments by week of the season - written to the log only, changes nothing in the game`).

**Test (blok `etap7_0`):** 0 bledow; koszt <= 1 ms/dobe; T1 kazde zdarzenie KRONIKI (CS) ma linie E7/0 z przyczyna (+-1 doba); T2 linia zbiorki w kazdej nocy z wodzem armii w polu;
T3 linia turniejow 120/120 dob. Bez progow wynikowych - te maja E7/K i E7/O1.

**Ryzyka:** male (sluchacze). `MakePeaceDetail` z Diplomacy/BK moze byc `Default` - przyczyna "inna" (bez zgadywania). **E4-E6:** tylko odczyt.
**Koszt:** zdarzenia rzadkie, licznik nocny w istniejacej petli - < 1 ms/dobe.

---

### E7/T - Kary terenu dokladnie jak gra (225, PLAN 7.4) - S - KOD TERAZ

**Zakres (R2).** W `TerrainEase.SpeedPostfix` (K:TerrainEase.cs:62-107) te same warunki co gra, bez czytania rozpiski (gra liczy ruch bez opisow - K:TerrainEase.cs:23-25):
1. **las:** `vf` = 0.3, albo `0.3 x (0 - ForestKin.PrimaryBonus)` = 0.15, gdy `mp.EffectiveScout` ma perk `DefaultPerks.Scouting.ForestKin` i piesi >= 75% `MemberRoster`
   (petla jak G DefaultPartySpeedCalculatingModel.cs:268-277, tylko przy perku); `feat` = `PartyBaseHelper.HasFeat(mp.Party, DefaultCulturalFeats.BattanianForestSpeedFeat)`
   ? `EffectBonus x 0.3` : 0 (G :279-282); zwrot `AddFactor(+(vf - feat))` - dokladnie netto gry (przy obu: 0); nasza kara `Add(-ForestSpeedPenalty x (vf - feat) / 0.3)`
   (perk albo cecha - polowa kary Jeffa, oba - zero). Liczby gry czytane w czasie gry (`PrimaryBonus`, `EffectBonus`), nie wpisane na stale.
2. **pustynia/wydmy:** `HasFeat(AseraiDesertFeat)` ? zwrot 0 i kara 0 (gra nic nie naklada - G :291-295) : jak dzis. DesertBorn - premia gry, zostaje.
3. brod/most/rzeka, snieg, noc, bagno - bez zmian (RB7 3.1: gra naklada zawsze, zwrot rowny).
4. Wpis w rozpisce pod ta sama nazwa ("Forest", "Desert") - dymek pokaze prawde. Komentarz K:TerrainEase.cs:26-28 ("swiadomie na plus") poprawiony.
5. **Probka godzinna** (dziala tez przy wylaczonym kluczu = "na sucho", tylko z `Stage7MeasureLog`): raz na godzine gry partie rodow AI i karawany w ruchu: teren pod partia
   (`GetFaceTerrainType`, jak K:TerrainEase.cs:76), cecha/perk, `mp.Speed`; liczniki: partio-godziny las/pustynia wedlug cechy, "zwrot ponad netto gry" starej reguly (0.3 - (vf - feat), 0.1 na pustyni z cecha)
   i biezacej, srednia predkosc w lesie / na rowninie dla Polnocy i na pustyni / rowninie dla Dorne - (v2) osobno dla partii, ktorych `MarchPace` nigdy nie tnie
   (karawany, wozy wsi - K:MarchPace.cs:96) i dla rodow.
6. **(v2) Probka z rozpiski gry** (tylko z `Stage7MeasureLog`): co godzine najwyzej 8 partii AI w lesie/na pustyni (losowo z probki pkt 5) - `SpeedExplained` na glebokosci 0
   (jak `DailyAudit`, K:TerrainEase.cs): suma wpisow GRY dla lasu (kara lasu, ForestKin, cecha lasu - nazwy z G DefaultPartySpeedCalculatingModel.cs:264-283) albo pustyni
   + nasz wpis zwrotu = netto po naszym postfiksie (niezalezne od naszego wzoru - prawdziwy T1); obecnosc wpisu "Marching column" = partia na suficie `MarchPace`
   (flaga "na suficie" do T3; bez nowej funkcji w `MarchPace`, ktora zmienia tez E6/K0).

**Kogo dotyczy** (ROT `ROT-Content\ModuleData\spcultures.xml`, RB7 3.1): cecha lasu - `battania` (Polnoc), `nightswatch`, `skagosi`, `freefolk`, `qohorik`, `summer`, `whitewalker`, `ibbenese`;
cecha pustyni - `aserai` (Dorne). Przyklad [S]: partia Polnocy PONIZEJ sufitu (jazda, karawana, woz wsi, mala druzyna), baza 4.0, suma czynnikow ok. 1.1 - dzis w lesie
+0.6 - 0.11 = ok. +12% wobec rowniny; po zmianie -0.05 x 1.1 = ok. -1.5% (polowa kary Jeffa). Dorne na pustyni: dzis +0.4 - 0.22 = ok. +5%, po zmianie 0
("No speed penalty on desert", G DefaultCulturalFeats.cs:117). **(v2, krytyka K1-05)** Kolumna piesza AI jest zwykle NA suficie `MarchPace` (Priority.Last, po nas:
`MarchFootPace` 4.0 x `WorldPacePercent` 75% = 3.0 jedn./h; L:36062 srednio 3.04) - nadmiar z lasu sufit obcina juz dzis, wiec dla niej zmiana wyniesie ok. 0-2%
(tylko gdy kara spycha ja ponizej sufitu), dla Dorne na pustyni 0. Skutek naprawy widac glownie u jazdy, karawan i wozow wsi.

**Pliki:** `TerrainEase.cs` (postfiks, `HourlySample`, linia dobowa obok `DailyAudit`), `ArmouryBehavior.cs` (godzinny tick), `Settings.cs`.
**Okna (dowod):** istniejacy postfiks na kazdym `CalculateFinalSpeed` (`TerrainEase.ApplyAll`, Priority.Normal, K:TerrainEase.cs:161-162), tylko na najbardziej zewnetrznym poziomie
(`SpeedDepth.OutermostFinal`, K:SpeedDepth.cs:26); ROT/NavalDLC/RBL wolaja vanille (RB7 1.1), wiec warunki gry sa te same. Bez nowej latki.

**Klucz:** `TerrainEaseExact` = true (`// a culture or a scout that halves the game's forest or desert penalty halves ours too - the penalty shrinks, it never turns into a bonus`).
Wylaczony = dzisiejszy pelny zwrot.

**Linia logu:** `Kary terenu (E7/T): dzien N | probki AI (partio-godziny): las a (cecha lasu b, ForestKin c), pustynia d (cecha pustyni e) | zwrot ponad netto gry: stara regula x jedn., teraz y | Polnoc las/rownina p1/p2, Dorne pustynia/rownina q1/q2 | koszt z ms`.

**Linia logu (v2, dopisek):** `| z rozpiski gry: probek r, netto teren (gra + zwrot) sr. u (maks. |u| v), na suficie MarchPace w% | ponizej sufitu: Polnoc las/rownina p3/p4`.

**Test (blok `etap7_t`, v2):** T1 z rozpiski gry (pkt 6): netto lasu i pustyni po naszym postfiksie <= 0 w kazdej probce (zwrot nigdy wiekszy niz kara gry), a przy cesze
dokladnie kara Jeffa x (vf - feat)/0.3 (+-0.001) - liczone z wpisow GRY, nie z naszego wzoru; dawny licznik `y` zostaje tylko jako odczyt; T2 probek las z cecha lasu >= 50
partio-godzin w 120 dob (inaczej prog nieczytelny - zglosic, nie zaliczac); T3 tylko partie ponizej sufitu (karawany i wozy wsi z pkt 5 + probki pkt 6 bez "Marching column"):
Polnoc las < rownina, Dorne pustynia <= rownina (srednie d31-120); udzial probek rodow na suficie - odczyt; T4 koszt probek <= 20 ms/dobe; T5 tempo marszu (rozdz. 4) w progu;
T6 regresja E5: miasta kupuja >= 98% plonu (wozy wsi Polnocy w lesie wolniej).

**Ryzyka:** male; (v2) jazda, karawany, wozy wsi i male druzyny Polnocy i 7 kultur ROT zwolnia w lesie o ok. 12-13%, Dorne na pustyni o ok. 5%; kolumny piesze na suficie
o 0-2% - to naprawa (Jeff 02.09: las i pustynia maja spowalniac). Wozy wsi Polnocy w lesie wolniej = mniej kursow na rynek (E5 `MarketCarts`) - prog T6.
Kolizje: `MarchPace` (sufit po nas - bez zmian), `NightRest.SpeedPostfix` (procent dlugu - bez zmian), E7/D2 (premia poza lasem - niezalezna), E6/K0 (tylko odczyt predkosci).
**NK:** nie. **Stary zapis:** bez stanu. **Koszt:** postfiks - `List.Contains` cech i perk zwiadowcy tylko w lesie/na pustyni (< 0.01 s/dobe, RB7 3.2); probka 24 x ok. 1 000 partii (<= 10 ms); (v2) rozpiska gry 24 x 8 wywolan (ok. 10 ms) - obie tylko z `Stage7MeasureLog`.

---

### E7/K - Turnieje, sojusze i rozejm w roku 364 (226, PLAN 7.7) - S - KOD TERAZ

**Zakres (R8).**
1. **Turnieje:** prefiks na `DefaultTournamentModel.GetTournamentStartChance` (G DefaultTournamentModel.cs:21-32) - ta sama formula co gra, tydzien = `(int)CampaignTime.Now.ToWeeks % 3`
   zamiast `GetWeekOfSeason` (prywatne `SuitableForTournament` :45 przez AccessTools). Przy 3 tygodniach w porze to dokladnie gra; kazde miasto co 3. tydzien caly rok -> na dobe tyle,
   co w zwyklej grze, na rok ok. 4.3x wiecej niz dzis. Czynny model NavalDLC przekazuje te metode do Default (RB7 6.1) - latka trafia; GrandTourney i TournamentsXPanded jej nie lataja.
2. **Sojusze:** postfiksy na getterach `DefaultAllianceModel.MaxDurationOfAlliance` / `MaxDurationOfWarParticipation` (G :112, :114) -> `CampaignTime.Days(Calendar.DaysPerYear(s))`
   i `/2` (364 / 182, liczone przy odczycie). Gra zapisuje koniec przy zawarciu (G AllianceCampaignBehavior.cs:463, :489) - trwajace sojusze i porozumienia koncza sie wedlug starych dat.
3. **(v2, krytyka K1-06) Rozejm gry:** propozycja wojny gry wybiera krolestwo w pokoju > 20 dni (predykat w `KingdomDecisionProposalBehavior.GetRandomWarDecision`,
   G :234-249, :242). Postfiks na tej metodzie (prywatna, AccessTools; wzor BK - BK DiplomacyPatches.cs:69-115 lata ja tak samo i zamienia wynik na `BKDeclareWarDecision`):
   gdy wynik to `DeclareWarDecision` (takze BK) przeciw krolestwu z `PeaceDeclarationDate.ElapsedDaysUntilNow <= 20 x DaysPerYear/84` (ok. 87) -> `null` (brak propozycji
   w tej probie; gra losuje znow przy nastepnej). Priority.Low - po postfiksie BK (cel ten sam, sprawdzamy `FactionToDeclareWarOn`). Roznica wobec "wybierz inne krolestwo"
   (predykat gry) - odczyt liczby propozycji w linii E7/0; transpiler na literale w domknieciu odrzucony (kruchy). Rozejmy BK (`YearsFromNow`) licza juz rok 364 - bez zmian.
4. Czasy trwania zjawisk w dniach (150 dni niecheci `DefaultDiplomacyModel.cs:1027`, trybut 100 dni) - bez zmian (R8: to nie kalendarz).

**Pliki:** NOWY `WorldTime7.cs` (latki wpinane w kampanii wzorem `CrownCallToWar.EnsureHooks` K:CrownCallToWar.cs:68 - pulapka CLAUDE.md 7 z konstruktorem statycznym modelu;
w tych dwoch modelach statycznych `TextObject` nie ma, ale wzor ten sam), `ArmouryBehavior.cs`, `Settings.cs`.

**Klucze:** `TournamentsAllSeason` = true (`// towns hold tournaments in every week of the season, each town every third week - as often per day as in the short calendar`);
`DiplomacyYearLong` = true (v2, dawniej `AllianceYearLong`; `// alliances last a year, calls to war half a year and the pause between peace and a new war proposal a quarter of the long calendar - 364, 182 and about 87 days instead of 84, 42 and 20`).

**Linie logu:** startowa `Czas swiata (E7/K): turnieje co 3. tydzien (prefiks wpiety; czynny model: <nazwa>), sojusz 364 / wezwanie 182 (postfiksy wpiete), rozejm gry 87 dni (postfiks wpiety, po BK tak/nie)`;
dobowa (v2): `| propozycje wojny gry wstrzymane rozejmem: n`;
turnieje - linia E7/0; wezwania - istniejaca "Wezwania do wojny (168): ... przez 182 dob" (dzis "42 dob", L:28628).

**Test (blok `etap7_k`):** T1 zadne okno >= 14 dob bez rozpoczetego turnieju (dzis 61 dob, L:10932-28898); T2 starty w kazdym tygodniu pory 0-12; T3 nowe porozumienia "przez 182 dob";
T4 KRONIKA: wojen naraz w d61-120 >= 7 (polowa dzisiejszych 14-17, CS:1593-4359); T5 "Szczelnosc (164a)": udzial "uciete" nagrod turniejow - odczyt (platnik z nadwyzka, L:36077);
T6 GrandTourney: zjazdy na sezon - odczyt (log GT skopiowac przed biegiem zapisu - dzis nadpisany, RA7 1.3); T7 (v2) zadna nowa wojna z decyzji krolestwa (przyczyna z linii E7/0)
miedzy para, ktora zawarla pokoj < 87 dni wczesniej (poza wezwaniami 168 i Diplomacy - pytanie 3); T8 linia startowa: postfiks rozejmu wpiety.

**Ryzyka:** (1) ok. 4x wiecej zjazdow GrandTourney (`SummonLords` GrandTourney TourneyBehavior.cs:262-320) - bezpiecznik `LocalWhenAtWar` zostaje; (2) dluzsze sojusze = mniej zmian
na mapie wojen (miara T4), a dluzszy rozejm = mniej nowych wojen (ta sama miara T4); (3) `CrownCallToWar.Days()` (K:CrownCallToWar.cs:60) czyta 182: cena gry rosnie x4.33 (G DefaultAllianceModel.cs:569 - cena x dni), rata dzienna = cena / dni bez zmian;
korona placi dluzej - zgodnie z regula 168 (brak raty = koniec porozumienia); prog gry `CallToWarWallet < -100000` (G :378) staly. **NK:** nie. **Stary zapis:** daty sojuszy zapisane - bez bledu.
**Koszt:** ~100 wywolan na dobe - 0 (postfiks rozejmu - przy propozycjach, kilka na dobe).

---

### E7/C - Jedna skala czasu (227, PLAN 7.1) - S - KOD TERAZ

**Zakres (R4).** Po odrzuceniu 7.2 z 7.1 zostaje tylko "jedna skala czasu" (rozdz. 2, "Pogodzone"):
1. `HideoutPurge.OnTick` (K:HideoutPurge.cs:135-139): odnowienie rozkazu pogoni odwetu co 3 s realne -> co `HideoutPursuitHours` godzin gry (`CampaignTime`, wzor `NightRest.GuardDue` K:NightRest.cs:344).
   Przy x8 dzis co ok. 7 h gry, przy x1 co ok. 0.9 h (RB7 2.3) - teraz zawsze tak samo.
2. Tekst MCM `WorldPacePercent` (K:Settings.cs:370 - "lore-true month of the 168-day calendar") po angielsku, bez roku 168 i bez obietnicy "miesiac lore"; komentarze `WorldPace.cs:14-28` - rok 364.
   Domyslnej (50) i wartosci Jeffa (75) nie ruszamy.
3. NIE w E7/C: `CrossingLaw` (etap 8, rozdz. 7); dlawienie komunikatow i odswiezanie widoku na sekundach realnych (`NightRest.cs:1033-1034`, `:1324-1326`, `Wayfinder.cs:51`, `QuartermasterLaw.cs:1033, 1065`) -
   widok, zostaje; rozne godziny "nocy" (sen 21-5, wzrok 21-4, warsztaty 23-5, ciemnosc gry 22-02 - RB7 2.3) - rozne zjawiska, nie scalac.

**Pliki:** `HideoutPurge.cs`, `Settings.cs` (+ `python tools/gen_mcm.py`), `WorldPace.cs` (komentarz).
**Klucz:** `HideoutPursuitHours` = 0.5 (`// a reprisal band renews its chase every this many game hours, whatever the game speed (0 = the old every three real seconds)`).
**Linia logu:** startowa `Jedna skala czasu (E7/C): odwet kryjowek co 0.5 h gry`; w istniejacej linii HideoutPurge - liczba odnowien na dobe.
**Test (blok `etap7_c`):** T1 grep kodu (narzedzie): zadna logika gry poza widokiem/komunikatami nie czyta `DateTime.Now` ani akumulatora klatek (lista wyjatkow w bloku, `CrossingLaw` jako etap 8);
T2 tekst MCM bez "168"; T3 odnowien pogoni na dobe gry - odczyt.
**Ryzyka:** male (bandy odwetu gonia rowno niezaleznie od predkosci gry). **NK:** nie. **Koszt:** 0.

---

### E7/O1 - Sen do switu: spiacy nie dostaje rozkazow (228, PLAN 7.6) - S - KOD TERAZ (v2 - przebudowana po krytykach)

**Przyczyna (v2, krytyka K1-01).** Dwa rozne zjawiska: (a) **karawany - 79% probek obudzen** (N: 435 z 551): BK `BKCaravansBehavior.HourlyTickPartyImpl` (BK :828-839)
przy Hold wola `ReleaseCaravanFromHold` (:298) -> `SetMoveGoToSettlement` (:367) bez patrzenia na `Ai.IsDisabled` - zatrzyma je tylko bramka (pkt 3), faza (pkt 1) nic tu
nie da; (b) **lordowie - 21%**: luka terminu AI (pkt 1) i cudze rozkazy (pkt 3). Lordowie spiacy w osadzie (`_townHold`) - ta sama luka terminu (pkt 1, K1-07).

**Zakres (R5).**
0. **Jedna funkcja snu (v2, O1-magazyn):** `NightRest.SleepKind(mp)` -> `Camp` (w `_bedPos` i `InCamp(godzina)`, K:NightRest.cs:225), `Debt` (`NightMarch._debtSleep`,
   dostep `internal`), `Town` (`NightMarch._townHold`), `None`. Uzywaja jej bramka (pkt 3), odnawianie (pkt 1) i swit (pkt 2). `_bedPos.Remove(mp)` wszedzie tam, gdzie partia
   wypada z `_camping` (dzis `HoldSleepers` zostawia `_bedPos` przy nieaktywnej partii i przy potknieciu - K:NightRest.cs:424, :459; `DropFromCamp` juz czysci oba) -
   inaczej bramka blokowalaby cala noc partie, ktorej nikt nie pilnuje. `IsCamping` (MaterialOrders, K:NightRest.cs:599-609) bez zmian: bramka pisze do `_orders` tylko
   u partii `Camp`, ktore maja tam wpis od zasniecia (`RememberOrder`), wiec obie odpowiedzi sie zgadzaja (zmiana `IsCamping` poszerzylaby pole regresji o kontrakty karawan).
1. **Faza snu (lordowie).** W przegladzie straznika co 0.1 h gry (`HoldSleepers`, K:NightRest.cs:414-468, wolany :1334), w przegladzie snu dlugu (:1348) i (v2) w przegladzie
   lordow w osadzie (`_townHold`, ten sam przeglad snu dlugu w `NightMarch`) dla partii `Camp`/`Debt`/`Town`: gdy AI wylaczyl NASZ termin (`_enableAgainAtHour` <= 1.0 h od teraz)
   i zostalo **< 0.6 h** (v2, O1-faza: AI wraca w `MobilePartyAi.Tick` po terminie - G MobilePartyAi.cs:311-320, :437-442; przy szybkiej grze i niskim fps jedna klatka moze
   trwac ok. 0.3 h gry) - `DisableForHours(1)` od nowa. Cudza blokada (BK uczta `DisableAi`, gentry 72 h; termin > 1.5 h - `NightMarch.ForeignHold` K:NightMarch.cs:238-252) -
   NIE ruszana. Odczyt terminu: NOWY skompilowany getter (delegat AccessTools, raz na sesje) tylko dla odnawiania; **`ForeignHold` bez zmian** (v2, K1-08: ujednolicenie
   odczytu to optymalizacja istniejacego kodu - 8.17). Rada RB7 5.3 (a) - bez `DisableForHours(2)` (`ForeignHold` uznalby nasz sen za cudza blokade).
2. **Swit.** W galezi switu `AiNightCamp` (K:NightRest.cs:703-736) - `mp.Ai.EnableAi()` dla partii `Camp` i (v2) `Town` (dzis `_townHold.Clear()` bez wlaczenia - AI wstaje
   do godziny po swicie), ktorym AI wylaczylismy MY (termin <= 1.0 h). Sen dlugu bez zmian (`ReleaseDebt` juz wola `EnableAi`, K:NightMarch.cs:1497-1504). Alarm T10 juz wola
   `EnableAi` (K:NightMarch.cs:222, :813, :833).
3. **Rozkaz dla spiacego.** NOWY `SleepGate.cs`: prefiksy Priority.First na **10** `MobileParty.SetMove*` (G MobileParty.cs:3919 Engage, :3928 GoAround, :3937 GoToSettlement,
   :3947 GoToPoint, :3978 Escort, :3991 PatrolAroundPoint, :4000 PatrolAroundSettlement, :4006 Raid, :4016 Besiege, :4028 Defend). **Bez bramki** (v2, O1-nawigacja):
   `SetMoveModeHold` (:3907), `SetMoveToNearestLand` (:3957 - ucieczki na lad nie wolno wstrzymac) i `SetMoveGoToInteractablePoint` (:3968) - rzadkie, a `ApplyOrder` ich
   nie odtworzy (`default: break`); gdy rusza spiacego, siatka `HoldSleepers` kladzie go z powrotem, a zapamietana misja zostaje nietknieta.
   Warunek blokady (kolejnosc = koszt): klucz -> `mp != MainParty` -> `SleepKind(mp)` = `Camp` albo `Debt` -> `mp.MapEvent == null` -> nie nasze oddanie rozkazu
   (`[ThreadStatic]` flaga w `ApplyOrder` K:NightRest.cs:655, `GiveOrderBack` :641 i alarmie T10). Skutek: oryginal nie biegnie, a rozkaz (v2, K1-02 / O1-magazyn)
   **zapisuje sie tam, gdzie trzyma go dany sen:**
   - `Camp` -> `_orders[mp]` (nadpisuje zapamietany; oddaje swit przez `GiveOrderBack` -> `ApplyOrder`, ze sprawdzeniem waznosci - wojna, strona - K:NightRest.cs:662-690);
   - `Debt` -> `_debtSleep[mp].Order` (oddaje `ReleaseDebt` przy dlugu 0 - K:NightMarch.cs:1497-1504 - nie swit; dluznik z dlugiem 2-3 spi przez swit i o 6:00 nie dostaje
     `SetMove`, wiec `HoldDebtSleepers` nie liczy go jako obudzonego; rozkaz zatrzymany w dzien nie czeka do nastepnego switu);
   - `Town` i alarm - bramka nie blokuje (osada: gra nie wypusci partii z wylaczonym AI, rozkaz czeka w partii do switu; alarm: partia czuwa - wyjatek R5).
   `NightOrder` (K:NightRest.cs:582-588) dostaje pola `Nav`, `Port`, `Point` (GoToPoint / PatrolAroundPoint); `ApplyOrder` ich uzywa (dzis `nav = Default`, port false -
   :664-690; rozkaz GrandTourney z `NavigationType.All` (TourneyBehavior.cs:313), Rescue i MaterialOrders z Naval/portem wrocilby inna droga, a straznik BK odrzuca cel bez drogi
   ladowej przy Default - K:CartTownExit.cs:15-24, wiec na wyspach rozkaz by przepadl). Wpisy z `RememberOrder` dostaja wartosci domyslne - dzisiejsze zachowanie bez zmian
   (zapamietywanie drogi i portu przy zasnieciu - etap 8, nie przy okazji). Pamiec bez zapisu (jak dzis).
4. `HoldSleepers` / `HoldDebtSleepers` zostaja jako siatka (zjazd > 0.3 bez rozkazu). `CrossingLaw.AiTick` (K:CrossingLaw.cs:146) i GrandTourney `SummonLords` - bez zmian:
   blokada przesunie ich rozkaz na swit (straz mostu i tak stoi; wyjazd na turniej o 6:00 zamiast o 0:00 - dla lorda w polu i w osadzie tak samo).
5. **(v2) Liczniki osobno lordowie / karawany** (K1-01): obudzeni cudza reka i ich rodzaj; rozkazy zatrzymane; u karawan osobno "zwolnienie z Hold przez BK" (karawana
   na Hold, `SetMoveGoToSettlement` w nocy - MaterialOrders spiacych karawan nie rusza, `IsCamping`); "AI naszych spiacych wstalo przed switem" (w przegladzie: partia
   `Camp`/`Debt`/`Town` z `!Ai.IsDisabled`, bez alarmu i bitwy) - bezposredni dowod fazy.

**Okna (dowod):** gra mysli za partie tylko z wlaczonym AI (G AiPartyThinkBehavior.cs:49) i przy Hold bierze nowy cel od razu (:94) przez `SetMove*`; `DisableForHours` (G MobilePartyAi.cs:1561-1565),
powrot AI w `Tick` (:311-320, :437-442). Cudze latki na `SetMove*` (sprawdzone, v2 - O1-BK): nasz `Wayfinder` - postfiksy GoToPoint/GoToSettlement (K:Wayfinder.cs:143-146, tylko `MainParty` :28-37);
**BK `AiDecisionTracePatches` - 13 celow** (12 `SetMove*` + `SetSailAtPosition`, BK :22-285): prefiksy bool (`GuardSettlementMove`, `FreezeWatchdog.Enter`, z `ref navigationType`)
i postfiksy. Harmony u Jeffa 2.4.2: po naszym `false` prefiksy bool / z ref sa pomijane (`SCR\harm\mct.cs:202-230`), postfiksy biegna - `FreezeWatchdog.Exit` tylko zeruje
`_handler`/`_diagnostic` (BK FreezeWatchdog.cs:248-255), nieszkodliwe; diagnostyka BK zaloguje ruch, ktorego nie bylo. Nasz prefiks Priority.First lapie `navigationType` przed zmiana przez BK.
Wolajacy (wazni): G `Army.cs:930` (wodz nowej armii na zbiorke), `SetPartyAiAction.cs:33, 48`; BK `ReleaseCaravanFromHold` (:367); nasze `IslandRoads.cs:46`, `MaterialOrders.cs:702`
(sprawdza wynik `car.TargetSettlement == dest` :709 - spiace karawany pomija wczesniej przez `IsCamping` :385, :853, wiec zatrzymany rozkaz nie bedzie wziety za nieudany).

**Klucz:** `SleepersHoldOrders` = true (`// a party asleep in camp takes no marching orders until dawn - the last order it was given waits and is carried out at first light; its own thinking sleeps till dawn too`).
Wylaczony = dzisiejszy straznik po fakcie (faza i blokada razem - jedna zmiana zachowania, jeden wylacznik).

**Linie logu (v2):** w linii godzinowej `noc.log` ("AiNightCamp") rodzaje obudzen osobno `lordowie / karawany` i dopisek `| rozkazy zatrzymane u spiacych: lordowie n1 (GoToSettlement a,
GoToPoint b, Engage c, inne d; z naszych e), karawany n2 (zwolnienie z Hold przez BK f, inne g); do snu dlugu h | AI odnowione r (oboz/dlug/osada) | AI wstalo przed switem: lordowie x, karawany y |
SleepGate sr. z us`; w linii switu (L "NightRest swit 6:00", L:650-35799) `| AI wlaczone o swicie s (oboz/osada), rozkazy zatrzymane i oddane o swicie t | dluznicy obudzeni o 6:00 u`.

**Test (blok `etap7_o1`, v2):** T1 obudzonych cudza reka razem <= 10 na noc, w tym lordowie <= 3 (d31-120; dzis 91, w probce 79% karawan - N:2835); T2 zjazd sr. <= 0.15 jedn. (dzis 0.40);
T3 lordowie w ruchu bez powodu w oknie obozu <= 3 na noc (dzis ok. 20), w tym "wyjazd z osady / po bitwie" <= 1 (`_townHold`); T4 dowod fazy: "AI naszych spiacych wstalo przed switem"
u lordow = 0 na noc (d31-120); u karawan i "rozkaz jak nasz zapamietany" (osobno lordowie / karawany) - odczyt (bramka zatrzymuje te same rozkazy, wiec to nie dowod fazy);
T5 dlug 3 AI = 0, kara zgodna z tablica (jak L:35798); T6 0 potkniec `SleepGate`/`HoldSleepers`; dluznicy obudzeni o 6:00 = 0; rozkazy zatrzymane dluznikom (h) oddane przy
`ReleaseDebt` (licznik oddanych = h, +-1 na dobe); T7 linia zbiorki E7/0: czlonkowie armii w drodze spia >= 95% partio-godzin; T8 KRONIKA i "Miara: marsz" - bez zmiany wojen i tempa (rozdz. 4);
T9 koszt `SleepGate` + odnawiania <= 5 ms/dobe; zwolnien BK zatrzymanych na noc - odczyt; MaterialOrders - "nieudanych" rozkazow karawan nie wiecej niz przy kluczu off (bisekcja).

**Ryzyka:** (1) srednie - zatrzymany rozkaz, ktory byl potrzebny w nocy (ucieczka nadana spiacemu przez cudzy mod): alarm T10 najpierw zdejmuje partie z obozu (`_camping.Remove`, `_bedPos.Remove`),
wiec po alarmie blokada nie dziala; bitwa - bez blokady; ucieczka na lad (`ToNearestLand`) - bez bramki; (2) armia gracza zwolana w nocy - spiacy lordowie rusza o swicie (widoczne dla Jeffa);
uczta BK u spiacego - wyjazd o swicie; (3) (v2 - zamkniete) Harmony 2.4.2 i para `FreezeWatchdog.Enter/Exit` - sprawdzone, nieszkodliwe (wyzej); (4) martwy kod ozywiony: zaden (straznik zostaje);
(5) (v2, K1-01) BK co godzine ponawia zwolnienie zablokowanej karawany: do ok. 418 karawan x 6 godzin = ok. 2 500 prob na noc, kazda z wyborem celu po stronie BK (koszt BK, nie nasz) -
zmierzyc liczbe prob (f) i stoper; przy dlawiku BKROT prob bedzie mniej. O swicie karawana jedzie do celu z OSTATNIEJ proby BK (w `_orders`) - swieza decyzja BK, jak dzis z `WakeHandler`.
**NK:** nie. **Stary zapis:** bez nowego stanu.
**Koszt:** prefiks w dzien - porownanie godziny; w nocy `SleepKind` (dwa slowniki); getter terminu ok. 900 partii (oboz + osada) x 60 przegladow na noc - < 0.01 s/dobe; mniej obudzen = mniej przeliczen trasy.

---

### E7/O2 - Namioty i ogniska przy napadzie na oboz (229, Jeff M 09.10) - S - KOD TERAZ (v2)

**Zakres (R18).** `CampScene` (K:CampScene.cs, wpiety K:SubModuleMain.cs:293) jest od sierpnia jako EKSPERYMENT, nigdy nieuruchomiony, domyslnie wylaczony
(`CampBattlePropsEnabled = false`, K:Settings.cs:1140; Armoury.json Jeffa :302 false). Jeff 09.10 04:25: "WLACZYC" (SP:841).
**(v2, krytyka O2-fizyka) Najpierw poprawka eksperymentu, potem wlaczenie:** dzis `CampScene` czeka na `Agent.Main` (wojsko juz rozstawione, K:CampScene.cs:43-46), stawia
namioty 11 m, plot 14 m, pochodnie 6 m od gracza, a **ognisko w promieniu 0 - na pozycji gracza** (:55-70), przez `GameEntity.Instantiate(scene, name, frame)` (:103), ktore
tworzy fizyke (`createPhysics: true`, TaleWorlds.Engine GameEntity - SCR\e7krit2\GE.cs:398-401). Navmesh sie nie zmienia, wiec AI wchodziloby w namioty i plot i grzezlo.
1. Scenografia **bez fizyki**: `GameEntity.Instantiate(scene, name, callScriptCallbacks: true, createPhysics: false)` albo `InstantiateWithRestOffset(scene, name, createPhysics: false, frame, 0f)`
   (oba przeciazenia sa w SCR\e7krit2\GE.cs:379 i :403) i potem `SetFrame` - agenci przechodza przez obiekty (najwyzej przenikanie w obrazie), nikt nie grzeznie.
2. Nic w promieniu 3 m od agenta gracza: ognisko 8 m ZA graczem (kat 180 stopni), namioty 11-13 m za graczem jak dzis, pochodnie 6 m, plot 14 m.
3. Lista kandydatow prefabow (`TentPrefabs`/`FirePrefabs`/`TorchPrefabs`/`FencePrefabs`) sprawdzana RAZ przy starcie przez `GameEntity.PrefabExists` (jest w TaleWorlds.Engine 1.4.8,
   GE.cs:423-425 - punkt "do sprawdzenia (2)" zamkniety); brakujace - w logu i pomijane jak dzis. Zadnego odczytu nazw prefabow z istniejacych encji (AccessViolation 08.10).
4. Domyslna `true` + opis MCM bez "EXPERIMENTAL"; przy wgraniu wpis `true` w `Armoury.json` Jeffa (gra zamknieta, `.bak`).

**Klucz:** istniejacy `CampBattlePropsEnabled` (domyslna false -> true; wpis Jeffa poprawiony przy wgraniu - pulapka CLAUDE.md 7).
**Linia logu:** startowa `CampScene (E7/O2): prefaby dostepne - namioty x/5, ogniska y/3, pochodnie z/2, plot w/2 (bez fizyki)`; w misji istniejace wpisy "CampScene".
**Test:** T1 linia startowa (autotest); T2 test Jeffa: napad na jego oboz w nocy - namioty i ognisko za linia gracza, **wojsko gracza i wroga nie grzeznie w scenografii**, 0 bledow
(autotest nie gra bitew w obozie gracza).
**Ryzyka:** encje w scenie misji (nie mapa), stawiane raz (`_done`); zly prefab = pominiety; bez fizyki - strzaly i ludzie przenikaja namioty (tylko wyglad). **NK:** nie. **Koszt:** 0 na dobe (tylko misja).

---

### E7/L - Dane lore 2 446 wiosek (230, PLAN 7.8) - M (Python) - KOD TERAZ, ROWNOLEGLE

**Zakres (R11).** Offline (w grze tylko nowy plik danych):
1. Skrypty i dane z kopii `D:\Backup-Bannerlord\wioski-lore-2026-10-08` do repo `tools/wioski-lore/` (dzis `b_dane.py` wskazuje Temp innej sesji - RC7 2.1); generator A czyta uklad z argumentu (P12).
2. Rycerze (A) od nowa na ukladzie w grze (`Modules\Armoury\ModuleData\arm_map_villages.tsv`, v4, 2 446 wiosek - wspolnych z A dzis tylko 687); dane B (zajecie, wiara, historia) bez zmian (2 446 / 2 446).
3. Poprawki WL-P rozdz. 10: P1 imiona rycerzy z list krainy (nie postacie AWOIAF), P2 Hollard wymarly, P3 herby unikalne w krainie, P4 herb Essos + kody `Banner.IsValidBannerCode`, P5 "Kept for them by",
   P7 historia bez powtorek tematu, P8-P10 zdania, P11 `war_lore` (Sherrer, Mummer's Ford, Wendish Town), P13 jeden plik. **P6 (`lands`) - pominiete** (kod liczy na zywo - R11).
4. Jeden plik `Armoury\ModuleData\arm_map_village_lore.tsv` (ok. 1-1.3 MB) z naglowkiem `layout_crc` = crc ukladu jak ksiega wiosek (uid:osady, K:VillageBook.cs:214-217; dzis 5A8C2735) -
   nie crc pliku (RC7 2.1). Tabela klucz wiary lore (`old_gods`, `seven`, `drowned`...) -> id wiary BK (dla R12).
5. `tools/wioski-lore/probka-dymkow.txt` - 20 dymkow z 5 krain do wgladu Jeffa (nie blokuje).

**Kontrole L0 (test paczki, narzedzie):** 2 446 / 2 446 uid; 0 imion z listy postaci AWOIAF martwych/glownych; 0 powtorzen herbu w krainie; ASCII; dlugosci linii <= 125 / 110;
bramka = 5A8C2735; kody herbow - kontrola w grze (linia startowa W2).
**Ryzyka:** zero dla gry (dane czyta dopiero W2). Uklad v4 zamrozony od 08.10 (WGRANIA 28-33 bez zmiany pliku); zmiana ukladu = bramka wylacza lore (dymek jak dzis).
**Wgranie:** nowy plik w `ModuleData` - lista plikow wgrania (dzis DLL + ModuleData) sprawdzona przy skladaniu.

---

### E7/B - Licznik przejsc przez Bliznaki (231, PLAN 7.12) - S - KOD TERAZ

**Zakres (R16).** Sam log, bez latki i bez stanu w zapisie:
1. Raz na godzine gry (`CampaignEvents.HourlyTickEvent`): `MobileParty.StartFindingLocatablesAroundPosition(brama, 8)` / `FindNextLocatable` (precedens K:NightMarch.cs:564) - tylko partie
   w promieniu 8 od mostu Bliznakow (`ROT_town3`, pozycja 374.07 / 630.77, brama 373.94 / 632.28 - ROT-Map settlements.xml:8077), bez petli po `MobileParty.All`.
2. Strona rzeki = znak iloczynu wektorowego (pozycja - most) x kierunek rzeki; kierunek liczony RAZ przy wczytaniu: 64 punkty na okregu r = 4 wokol mostu, `GetFaceTerrainType` (woda/lad,
   jak K:TerrainEase.cs:76), os = prosta przez srodki dwoch lukow wody; brak dwoch lukow = licznik wylaczony z linia "BRAK OSI" (bez wywrotki).
3. Pamiec: slownik partia -> ostatnia strona (tylko partie w strefie; wpis znika po wyjsciu); przejscie = zmiana strony. Czyszczenie w `Reset`.
4. Wejscia do miasta (K:Measure169c.cs:891-929, linia "(e) Bliznaki") - zostaja obok (porownanie). `CrossingLaw` - nie (petla martwa, etap 8).

**Pliki:** NOWY `TwinsCrossing.cs`, `ArmouryBehavior.cs`. **Klucz:** `TwinsCrossingCount` = true (`// counts the parties crossing the bridge at the Twins each day and what a toll would have brought - log only, nobody pays anything`).
**Linia logu:** `Przeprawa Bliznaki (E7/B): dzien N | przejsc n (na polnoc a, na poludnie b) - karawany c (kiesy X zl, ludzi M, zwierzat K), wozy wsi d, partie rodow e (krolestwo pana f / obce g), bandy h, gracz T/N | myto gdyby bylo Z zl (2 zl od czlowieka, 1 zl od zwierzecia; rody krolestwa pana bez myta - stawki z ES:215, tylko do porownania)`;
w dobach 1-3 pozycje przejsc (x/y).
**Test (blok `etap7_b`):** T1 linia 120/120 dob; T2 przejsc > 0; T3 pozycje przejsc z dob 1-3 <= 3 jedn. od bramy (inaczej brod/drugi most w promieniu - zmniejszyc promien); T4 koszt < 0.1 ms/dobe.
**Ryzyka:** zero dla gry. Partia szybsza niz 8 jedn./h przeskoczy strefe miedzy probkami (przy ok. 5 jedn./h - co najmniej 2 probki) - licznik zaniza, nie zawyza. **NK:** nie.

---

### E7/W1 - Komunikat "plonie Twoja wioska", pamiec wojny (232, PLAN 7.10 a) - S - KOD TERAZ (v2)

**Zakres (R10, R14).**
1. Sluchacz `CampaignEvents.VillageStateChanged(village, old, new, raiderParty)` (G CampaignEvents.cs:659; wolany przy wejsciu w rabunek z napastnikiem i przy Looted - RC7 4.1).
   Jeden "plonie" na wies na sesje rabunku (przy milicji gra daje 2 zdarzenia - RB K12; klucz: komponent rabunku albo doba + napastnik).
2. Kto slyszy: wies rodu gracza (`Village.Bound.OwnerClan == Clan.PlayerClan` - to samo co "your fief" w `HeldLine` K:MapVillagesView.cs:4452) i rabunek gracza;
   opcja "cale krolestwo". {VILLAGE} = wioska z ogniem (`VillageBook.FxUid` - ten sam wybor co widok), {RAIDER} = przywodca na zywo. Postac: `MBInformationManager.AddQuickInformation` + `Log.Player`.
   Gra daje tylko wpis dziennika (`CommentOnChangeVillageStateBehavior`) - komunikat niczego nie dubluje (RC7 4.1).
   **(v2, krytyki K1-04 / W1-spalona) Tekst wedlug JEDNEGO stanu - ksiegi (R10):**
   - wejscie w rabunek: `VilLogBurningOwn` (jest, K:VillageTexts.cs:26);
   - **przejscie wioski na spalona w ksiedze** (`CheckUp`, K:VillageBook.cs:632-636 - wolane synchronicznie z `Burn`, takze przy kroku R1 i przy zerowaniu przez oboz armii):
     ksiega dopisuje (uid, doba, StringId sprawcy) do kolejki pod swoim zamkiem, `VillageNews` oproznia ja w ticku (nie z wnetrza `Strike` ani prefiksu Harmony) -
     `VilLogBurned` ("{VILLAGE} in the {DISTRICT} district has been burned by {RAIDER}.") albo `VilLogBurnedPlayer` przy rabunku gracza;
   - **Looted gry przy wiosce, ktora w ksiedze nie jest spalona:** NOWE teksty EN `VilLogPlundered` = "{RAIDER} has plundered {VILLAGE} in the {DISTRICT} district: {BURNED} of {N} settlements burned."
     i `VilLogPlunderedPlayer` = "Your men have plundered {VILLAGE}: {BURNED} of {N} settlements burned." ({BURNED}/{N} - osady wioski z ksiegi). Gdy ta sama sesja spalila wioske
     (komunikat "has been burned" juz byl) - przy Looted nic drugi raz.
   Po R1 kazda sesja AI konczy sie Looted po jednym kroku (ok. 6% osad wioski - jeden krok lorda na ok. 16 osad), wiec "has been burned" przy Looted klamalby wobec dymka i menu okregu.
3. **Pamiec wojny** na wioske w ksiedze: ostatni napastnik, doba, ilu ucieklo, zginelo (z `RaidState` E4/5 w chwili `RaidCompleted`, przed `EndFinalizer` - po R1 ten sam stan, K1-03),
   rodzaj ("Burned" tylko przy przejsciu na spalona w ksiedze; inaczej "Plundered"), licznik spalen (tylko przy przejsciu na `SBurned`) - nowa sekcja `m:` w `VillageBook.Export/Import`
   (K:VillageBook.cs:441, :187; jeden pisarz `arm_district` K:ArmouryBehavior.cs:461). **(v2, W1-zapis)** W zapisie `StringId` bohatera-wodza i jego rodu, sprawdzone `BadId`
   (K:VillageBook.cs:145-148) - nigdy wyswietlana nazwa partii (`RaiderName` zwraca `lp.Name.ToString()`, K:MapVillagesView.cs:1935; znak `|` w nazwie zepsulby cala ksiege przy
   `Split('|')` - :323, a nazwa zostalaby zamrozona w jezyku i z dopiskiem "'s Party"). Nazwa na zywo przy budowie dymka; bohater nie istnieje = tekst zastepczy EN "a raider long gone".
   Stary zapis bez sekcji = brak pamieci.
4. Teksty pamieci (dla W2): "Burned by {RAIDER} in the {SEASON} of {YEAR} AC; about {FLED} souls fled and {DEAD} were killed." oraz (v2) "Plundered by {RAIDER} in the {SEASON} of {YEAR} AC;
   about {FLED} souls fled and {DEAD} were killed." - pora i rok z kalendarza Armoury (rok AC jak K:Calendar.cs:17-19).

**Pliki:** NOWY `VillageNews.cs`, `VillageBook.cs` (sekcja `m:`, pola, kolejka przejsc na spalona w `CheckUp`), `Devastation.cs` (tylko odczyt liczb z `RaidState`), `VillageTexts.cs`
(teksty pamieci, `VilLogPlundered`, `VilLogPlunderedPlayer`), `ArmouryBehavior.cs`.
**Klucze:** `VillageRaidMessages` = 1 (`// who hears that a village is burning: 0 nobody, 1 your own fiefs, 2 your whole kingdom`); `VillageWarMemory` = true (`// a village remembers who burned or plundered it, when, and how many fled and died`).
**Linia logu:** `Wioski - wiesci (E7/W1): dzien N | plonie: zdarzen a (wsie gracza b, krolestwa gracza c), komunikatow d (by bylo przy "krolestwo" e); spladrowane f | spalone w ksiedze i (komunikatow "burned" k), splondrowane bez spalenia j (komunikatow "plundered" l) | pamiec wojny: wpisow g (nowe h)`.
**Test (blok `etap7_w1`, v2):** T1 zdarzen "plonie" = sesjom rabunku z linii "Ludzie (spustoszenie)" + sesjom R1 z linii "Rabunek osada po osadzie (E7/R1)" (po R1; +-1 na dobe - dedup milicji);
T2 autotest bez lenna: komunikatow 0, "by bylo" > 0 (dowod sciezki); T3 bieg zapisu 9 dob: wpisy pamieci po wczytaniu = przed zapisem; stary zapis Jeffa (doba 362) wczytuje sie
(sekcji brak - 0 wpisow, 0 bledow); T4 0 potkniec; T5 zgodnosc ze stanem ksiegi: kazdy "burned" (komunikat, "by bylo", wpis pamieci) ma `SBurned` w tej samej dobie (i = liczba
spalonych z linii "Ksiega wiosek (E4/11)"), zaden "plundered" - nie; T6 sekcja `m:` bez nazw (tylko identyfikatory przechodzace `BadId`; kontrola w biegu zapisu).
**Ryzyka:** spam przy "krolestwo" (ok. 3 rabunki na dobe swiata - RC7 4.1) - domyslnie tylko wlasne lenna; przejscie na spalona z zerowania armii (bez rabunku) tez da "has been burned by
{wodz armii}" - zgodne z R10 (jeden stan). **NK:** nie. **Koszt:** zdarzenia rzadkie - 0.

---

### E7/W2 - Dymek wioski: herb, rycerz, wiara, historia (233, PLAN 7.9 a) - S-M - PO E7/L

**Zakres (R11, R12).**
1. `MapVillageLore` (szkic WL `MapVillageLore.cs`, 111 linii): jedno wczytanie pliku E7/L na sesje, slownik uid -> wiersz, bramka `layout_crc` == crc ukladu `VillageBook`; zly uklad = lore wylaczone,
   dymek jak dzis + jedna linia w logu.
2. Dymek: wlasny typ `MapVillageTip` + `MapVillageTooltipVM : PropertyBasedTooltipVM` z `BannerImageIdentifierVM` (TECH 1.2, szkic `MapVillageTooltip.cs`, 200 linii); rejestracja
   `InformationManager.RegisterTooltip<MapVillageTip, MapVillageTooltipVM>(cb, "ArmMapVillageTooltip")` w `OnBeforeInitialModuleScreenSetAsRoot` (K:SubModuleMain.cs:42), wyrejestrowanie
   przy wyladowaniu. **Pulapka:** delegat `Action<PropertyBasedTooltipVM, object[]>` (inaczej pusty dymek bez bledu - TECH 1.2). Prefab `Modules\Armoury\GUI\Prefabs\ArmMapVillageTooltip.xml`
   = kopia ROT `PropertyBasedTooltip.xml` + herb 52x52 (sprawdzony parserem 08.10) - **nowy katalog w instalacji** (wgranie musi go kopiowac).
3. Linie: herb pana (`Clan.Banner`) przy nazwie; pan na zywo (`HeldLine` bez zmian); rycerz "Kept for them by Ser X of Y" (Polnoc bez "Ser"; Dothrakowie "takes its tribute") - albo posiadacz tytulu
   wsi z BK (`TitleManager.GetTitle(settlement).deJure` odbiciem, TECH 1.2), gdy jest kim innym niz glowa rodu pana (R12); wiara "Faith: {wiara BK} ({udzial}%)" z `PopulationData.ReligionData`
   (BK ReligionData.cs:21-40) przez istniejacy odczyt BK odbiciem (K:BkSeal34.cs:165, :353-383); po Alt (tekst gry `str_map_tooltip_info`): zajecie, zdanie o swietym miejscu (tylko gdy
   klucz wiary lore = dominujaca wiara BK), historia, pamiec wojny (W1).
4. Wpiecie: tylko `Visual.OnHover` (K:MapVillagesView.cs:4604-4635) - buduje `MapVillageTip` z tych samych danych (`StateLine`, `HeldLine`, `RaiderName` bez zmian - jedno zrodlo).
   Zapas (klucz off albo rejestracja nieudana): dzisiejszy dymek + linia pana w kolorze rodu.

**Klucz:** `VillageTipLore` = true (`// the village tooltip shows the lord's banner, the knight who keeps the village and its faith from Banner Kings; hold Alt for its trade, history and war memory`).
**Linia logu:** startowa `Dymek wioski (E7/W2): lore 2446/2446 (bramka 5A8C2735 zgodna), dymek ArmMapVillageTooltip zarejestrowany | BK: wiara tak/nie, tytuly tak/nie | herby z lore niewazne n`.
**Test (blok `etap7_w2`):** T1 linia startowa (bramka zgodna, 0 odrzuconych); T2 0 potkniec `OnHover`/lore; T3 zdjecia z autotestu (najechanie na wioske Polnocy, Reach, Dorne) - herb, rycerz, Alt;
T4 wiara w dymku = dominujaca wiara BK (probka 20 wiosek w logu raz na sesje).
**Ryzyka:** prefab nigdy niewidziany w grze - blad prefabu = brak dymka, nie wywrotka (TECH 1.1); tekstury herbow czyszczone przy wejsciu w misje - dymek budowany przy kazdym najechaniu, nie dotyczy.
**NK:** nie. **Koszt:** 0 na dobe (wczytanie raz 20-50 ms, pamiec 2-3 MB napisow).

---

### E7/W3 - Menu okregu "Look over the district" (234, PLAN 7.9 b) - S - PO W2

**Zakres (R13).** Opcja "Look over the district" (teksty sa: `DistOption`, `DistTitle` K:VillageTexts.cs:32-33) w menu `village` i `village_looted` -> `MBInformationManager.ShowMultiSelectionInquiry`
z herbem rycerza w kazdym wierszu (`InquiryElement` z `BannerImageIdentifier`), dymek wiersza (rycerz, stan, zajecie, pamiec wojny), "Hear more" -> pelny opis, "Back to the villages".
Wiersze z TEJ SAMEJ funkcji stanu co `VillageBook.MenuSection` (K:VillageBook.cs:1392); w "People of this land" (K:PeopleMenu.cs:29-32) zostaje naglowek i podsumowanie okregu, bez wierszy wiosek.
Bez przelaczania menu (zwykle menu, nie menu oczekiwania - pulapka `SwitchToMenu` nie dotyczy, TECH 3.1).
**Klucz:** `DistrictMenuEnabled` = true (`// "Look over the district" in a village menu lists the villages of its district with the banners of their keepers`). Wylaczony = dzisiejsza sekcja z wierszami.
**Linia logu:** startowa (index opcji w `village` po BK/ROT).
**Test (blok `etap7_w3`):** T1 opcja wpieta w obu menu; T2 zdjecie okna (tryb zdjec autotestu; czy herb nie jest rozciagniety w ramce 86 x 63 prefabu ROT); T3 0 bledow.
**Ryzyka:** kolejnosc opcji (BK i ROT dokladaja do `village`). **NK:** nie. **Koszt:** 0 (tylko po kliknieciu).

---

### E7/R1 - Rabunek osada po osadzie dla AI (235, PLAN 7.11 a) - D - NK - PO E5/A7 I W1 (v2)

**Zakres (R15; RB 0-11 po poprawkach K1-K12, przeliczony na E4/5; v2 po krytykach K1-03, R1-kolejnosc, R1-koniec, R1-RaidDead, R1-test, R1-milicja).**
1. **Przejecie fazy lupienia:** NOWY `DistrictRaid.cs` - prefiks `RaidEventComponent.Update` (G RaidEventComponent.cs:160) z **Priority.First** (v2) zwraca false w fazie lupienia
   (`DefenderSide.TroopCount == 0`, G :162) dla rabunkow, ktore prowadzi R1. **Decyzja "rabunek prowadzi R1"** (v2) zapada RAZ, na poczatku rabunku, zanim pobiegnie jakikolwiek
   inny prefiks `Update`: w pierwszym wywolaniu prefiksu R1 (Priority.First - przed `Devastation.RaidPrefix` i oknem W6 `Measure40`) albo w sluchaczu `VillageStateChanged` -> BeingRaided;
   zapisana w statycznym slowniku komponent -> sesja (`DistrictRaid.Owns(raid)`), warunek: klucz i wies ma przelicznik ludzi (`Devastation.Covers`). Krok = m osad
   (m = min(Z, max(1, N / D)), D = `RaidDetachmentMen`), postep w ludziach przeszukanych (R x N x f x u x dt, R = istniejace `DevastationPerRaiderDay`), punkty osady = 1 - 0.99 x postep
   (nigdy 0 w kroku - K1). Co 5% kroku `Devastation.StrikePeople(v, ludzie, rs, napastnik)` - NOWA metoda obok `Strike` (K:Devastation.cs:233) z **wspolnym rdzeniem** (v2): `Strike`
   liczy trafionych z osobodni i wola ten sam kod od "trafionych" w dol (`PeopleBook.Move`, zabici / las / uchodzcy, `Famine.Burn`, `VillageBook.OnStrike` z `rs.Comp`, liczniki
   `rs.Hit`/`rs.Dead`/`rs.CapWould`, K:Devastation.cs:245-284) - jedna funkcja rachunku, bez drugiej kopii; ludzie -> `VillageBook.OnStrike` -> lancuch `Burn` (wpiecie E411:365).
2. **Jeden stan rabunku (v2, K1-03 / R1-RaidDead):** R1 zaklada i wypelnia TEN SAM `RaidState` E4/5 (`Devastation.StateOf(raid, v, true)`, dostep `internal`; `Slaver` z kultury
   najezdzcy jak dzis - K:Devastation.cs:325, `Comp` = komponent rabunku), przez `StrikePeople`. Stan zyje do `EndFinalizer` (K:Devastation.cs:451), wiec czytaja go jak dzis:
   jency E4/9 (`ThrallLaw.CapturePrefix` -> `Devastation.TakeCaptives`, K:ThrallLaw.cs:178-215, K:Devastation.cs:467-485 - bez stanu dalby 0 jencow i dalej blokowal BK),
   nekromancja Innych (`Devastation.RaidDead` w prefiksie ROT `OnRaidCompleted`, K:OthersRise.cs:328 - bez stanu 0 trupow), klucz sesji ksiegi wiosek (`rs.Comp` - ogien i `FxUid`
   w widoku i komunikatach) i pamiec wojny W1. **Straznik przeciw podwojnemu liczeniu:** `if (DistrictRaid.Owns(__instance)) return;` na SAMEJ GORZE `Devastation.RaidPrefix`
   (:330 - przed `StateOf`, osobodniami i podmiana hearth :356; `__state` zostaje pusty, wiec `RaidFinalizer` nic nie robi - :365-366); w `EndPrefix` (:405) przy `Owns` pomijane
   tylko `Settle` i podmiana hearth - `StateOf` zostaje. `EndFinalizer` nie liczy sesji R1 do "Ludzie (spustoszenie)" (`TotalDays` = 0) - licza sie w linii R1.
3. **AI:** jeden krok (m osad) na sesje, potem koniec wedlug pkt 7 (punkty 0 -> Looted w `OnBeforeFinalize` G :128-135 -> `RaidCompleted`); czujnosc (Looted) ceil(1 + 100 x d) dob,
   maks. `RaidAlertMaxDays` (d = trafieni sesji / ludzie okregu); prefiks `VillageHealCampaignBehavior.DailyTickSettlement` dla wsi w czujnosci - tylko odklada leczenie wsi
   (wies zostaje Looted przez N dob). (v2, R1-milicja) Milicji R1 nie rusza: +20 milicji siedzi w `IncreaseSettlementHealthAction.ApplyInternal` (G :9-15, przy powrocie wsi do Normal)
   i to zrodlo juz zamyka E4/8 (`MilHealPre/Post`, K:ArmsFromPeople.cs:969) - wlasna korekta R1 liczylaby je drugi raz.
4. **Lup z zasobow wsi:** udzial s = ludzie kroku / ludzie w domu; zloto z kiesy wsi TA SAMA funkcja platnika co E5/A7 (do wysokosci kiesy, inaczej 0 - AZ:291); towar z magazynu wsi
   (`Settlement.ItemRoster`) w udziale s - do napastnika (bez losowania 50% gry) albo "spalone w rabunku" (ujscie z przyczyna: ksiega towarow 146, `HorseBook` dla koni, `ArmsBook` dla uzbrojenia - E6/0);
   zadnego towaru "z dochodu wsi" ani "common loot" (G :273-299 - z niczego); napastnicy jedza ze spichlerza okregu tyle, ile zjadaja (K4).
5. **Kara relacji:** `RaidDamage = q` przed `RaidCompleted` (K3; inaczej rabunek bez kary; setter prywatny - G :30 - przez odbicie, delegat raz na sesje).
6. **Zapis:** sekcje `r:` (sesje) i `c:` (czujnosc) w `arm_district` przez `VillageBook.Export/Import` (zarezerwowane - E411:201). Brak = sesja od nowa z punktow gry (stary zapis bez bledu).
7. **(v2, R1-koniec) Co prefiks R1 robi sam zamiast oryginalu `Update`.** Oryginal w fazie lupienia rabunku R1 NIGDY nie biegnie - takze na koniec (przepuszczenie go przy punktach ~0
   wyplaciloby jeszcze raz zloto za hearth z niczego G :179-188 i towar z niczego :273-299, bo `CalculateHitDamage` nie zalezy od punktow osady - G DefaultRaidModel.cs:46-58):
   - w kazdym ticku lupienia: `MapEvent.WasEverInLootingPhase = true` (G :164; czytaja ja przelaczenie odsieczy w bitwe polowa - G EncounterGameMenuBehavior.cs:732-754 - i rozpoznanie
     rabunku z morza - G MapEventHelper.cs:72), `SettlementHitPoints` wedlug postepu, `RaidDamage` (setter z odbicia);
   - na koncu kroku AI: `SettlementHitPoints = 0`, `MapEvent.BattleState = AttackerVictory` (setter internal - odbicie; G MapEvent.cs:284-291), `finish = true`,
     `MapEventSettlement.Party.SetLevelMaskIsDirty()`, `return false` (dokladnie zakonczenie gry z G :301-306);
   - lup: towar napastnika (pkt 4) i `CampaignEventDispatcher.Instance.OnItemsLooted(partia, lup)` jak oryginal (G :308-311); sluchacze w grze: `ViewDataTrackerCampaignBehavior`
     (znaczniki nowych przedmiotow) i `VillageHostileActionCampaignBehavior` (lup gracza - R2); BK i Armoury nie sluchaja; **przed kodem** ilspycmd: ROT, BKROT, BEE, DTE, NavalDLC
     (czy ktos nie liczy lupu drugi raz);
   - hearth: tylko przez `StrikePeople` (`PeopleBook.Move`) - zadnego `Hearth -=` gry.
8. **(v2, R1-test) Niezalezny bilans lupu.** Okno W6 `Measure40` pomija wsie objete E4/5 (K:Measure40.cs:446), a pomiar lupu w `Devastation.RaidPrefix/RaidFinalizer`
   (K:Devastation.cs:355-357, :375-379) jest dla rabunkow R1 wylaczony straznikiem - wiec R1 mierzy sam, wokol kazdego kroku: kiesa wodza (wzor Devastation), kiesa wsi (platnik E5/A7),
   magazyn wsi (sztuki i wartosc), towar napastnika, spichlerz okregu. Linia podaje roznice: zloto wodza = ubytek kiesy wsi; towar napastnika + spalone w rabunku = ubytek magazynu;
   zjedzone = ubytek spichlerza. Do tego "Szczelnosc (164a)" i `MoneyLedger` bez nowego zrodla bez platnika.

**Okna (dowod):** G `RaidEventComponent.Update` :160-312 (zloto :179-188, magazyn :204-264, z niczego :273-299, `OnItemsLooted` :308-311, koniec :301-306), `OnBeforeFinalize` :128;
E4/5 prefiks/finalizer (K:Devastation.cs:330-391, :405-461; void z `out __state` - po naszym `false` biegnie dalej, Harmony `SCR\harm\mct.cs:202-230`);
cel AI rabunku (G AiMilitaryBehavior.cs:426, :431), powrot po walce z milicja (`OnFinalize` G RaidEventComponent.cs:146-158), wynik celu x (1 + (1 - punkty)) (G DefaultTargetScoreCalculatingModel.cs:301);
ROT `AIThinkPatch` (RB K12). E5/A7 (jeszcze niekodowane - SP "DALEJ") lata te sama metode (okno "rabunek wsi", AZ:302) - jedna funkcja platnika, R1 wola ja dla swoich rabunkow,
E5/A7 omija rabunki `DistrictRaid.Owns`.
**Klucze:** `RaidBySettlementEnabled` = true, NK (`// villages are raided settlement by settlement: each takes days, and the loot comes only from what the village really has`); `RaidDetachmentMen` = 200;
`RaidAlertMaxDays` = 17 (opisy EN przy kodzie).
**Linia logu:** `Rabunek osada po osadzie (E7/R1): dzien N | sesji a (AI b, gracz c), krokow d (osad e), dni na osade mediana f (lordowie), przerwanych g (%) | trafieni h = zabici + las + jency + uchodzcy (kontrola 0) | lup: zloto z kiesy wsi X zl, towar z magazynu Y szt. (Z zl), spalone w rabunku S szt., jedzenie ze spichlerza J | bilans (v2): zloto wodza - kiesa wsi d1, towar + spalone - magazyn d2, zjedzone - spichlerz d3 | jency z rabunkow R1 c, zabici w stanie rabunku (RaidDead) z | czujnosc: wsi k`.
**Test (blok `etap7_r1`, v2):** T1 bilans zlota d1 = 0 w kazdej dobie (niezalezny pomiar pkt 8) i "lup z niczego" w linii "Hearth (E4/0)" = 0 zl dla wsi objetych (dzis ok. 670 zl/d - L:634-36086);
T2 bilans towaru d2 = 0 i spichlerza d3 = 0 (towar z niczego 0); T3 kontrola trafionych = 0; T4 mediana dob na osade lorda <= 5; T5 wioski "spalone" > 0 w 120 dob (dzis 0); T6 udzial przerwanych
sesji - odczyt (dzis 233 z 308); T7 0 potkniec; T8 KRONIKA i oblezenia - bez zapasci (odczyt); T9 bieg zapisu: sesje `r:` po wczytaniu = przed zapisem; T10 (v2) jency z rabunkow R1 przy najezdzcy
z niewola > 0 (dzis E4/9 dziala dla rabunkow E4/5); T11 (v2) zabici w `RaidDead` rabunkow R1 = zabici z linii R1 (+-1); trupy OthersRise z rabunkow R1 > 0, jesli Inni rabowali; T12 (v2)
odsiecz w trakcie rabunku R1 przechodzi w bitwe polowa (odczyt, jesli wystapi).
**Ryzyka:** (1) tempo wojny - lord pali 1 osade w ok. 4.6 doby (gra dzis ok. 2.4), czesc AI nie skonczy osady (przerywa AI/odsiecz) - czesc kroku w ksiedze, bez czujnosci; (2) spustoszenie swiata ok. 4x
dzisiejszego na rabunek (osada 250 ludzi wobec ok. 57) - zamierzone (warunek widocznosci W4), zmierzyc spalone i uchodzcow wobec E4/5; (3) nasluchy konca rabunku (E4/5, E4/9 jency BK `ExecuteCapture`,
`WatchLaw.OnRaidCompleted` K:ArmouryBehavior.cs:755, `OutlawLaw.OnVillageLooted` :715, ROT nekromancja, BKROT, BEE -8% chlopow na kazde `VillageBeingRaided`, Diplomacy) widza jeden koniec na sesje
jak dzis - dluzsze sesje = rzadsze zdarzenia; (4) linie gry w RB z dekompilacji sprzed 1.4.8 - przy pisaniu tylko G (dec32); (5) (v2) kolejnosc Harmony na `Update`: R1 Priority.First,
`Devastation.RaidPrefix` Normal (straznik na gorze), `Measure40.RaidPre` void z finalizerem (pomija wsie E4/5 - K:Measure40.cs:94, :432-450), E5/A7 omija `Owns` - wpisane do 5.1;
(6) (v2) sluchacze `OnItemsLooted` w cudzych modach - przed kodem. **NK:** tak (osady n i tempo od startu); stary zapis wczytuje sie.
**Koszt:** prefiks na ok. 7 rownoczesnych rabunkow (zastepuje petle lupu gry), 20 `StrikePeople` na krok, bilans 5 odczytow na krok - pojedyncze ms/dobe (stoper w linii).

---

### E7/R2 - Okienko gracza po kazdej osadzie (236, PLAN 7.11 b) - M - NK - PO R1

**Zakres.** Gracz rabuje ta sama regula co AI (R1), ale po kazdej osadzie gra staje: okienko `ShowInquiry` (teksty sa: `RaidStepTitle`, `RaidStepBody`, `RaidNext`, `RaidEnd` - K:VillageTexts.cs:68-72)
otwierane z ticku menu (wzorzec NightRest - flaga, okienko w ticku); "Raid the next settlement (about N days)" - nastepny krok; "End the raid" = punkty 0 + `SetOverrideWinner(Attacker)` + `StartWait` (K2)
- (v2) koniec ta sama droga co R1 pkt 7 (prefiks R1 konczy, oryginal `Update` nie biegnie - inaczej zloto i towar z niczego),
dalej droga gry (`village_player_raid_ended`, G VillageHostileActionCampaignBehavior.cs:99-122); postfiks init raidu po wczytaniu (`StartWait`). **ZAKAZ `GameMenu.SwitchToMenu()` z menu oczekiwania
`raiding_village`** (CLAUDE.md 7 - CTD) - tylko `ExitToLast` / droga gry.
**Klucz:** `RaidPlayerStepPopup` = true (`// your raid stops after each settlement and asks: raid the next one (about N days) or end the raid`). Wylaczony = gracz jak AI (jeden krok i koniec).
**Test:** autotest gra AI - test Jeffa (lista RB ryzyko 7): pasek 99% przy okienku, "End the raid", "End Raiding" po 1 osadzie i przy 0, wczytanie w stanie decyzji, armia gracza / gracz w armii AI,
odsiecz w trakcie okienka, Escape; 0 bledow.
**Ryzyka:** zakleszczenie menu / CTD (RB K1/K2) - najwieksze w etapie; dlatego osobna paczka z wylacznikiem. **NK:** tak. **Koszt:** 0.

---

### E7/W4 - Wyglad spalonej wioski i napisy nad spalonymi (237, PLAN 7.10 b, Jeff L) - S - PO R1

**Zakres (R10, R17).**
1. Wioska czesciowo pusta - nizszy poziom obrazka (3 -> 2 -> 1) wedlug udzialu pustych osad z `VillageBook.StateOf` (WM 1.4); spalona (stan ksiegi, >= 50% osad) - przyciemnienie
   `GameEntity.SetFactorColor` na kopiach W2 + zdjete detale + dym `map_icon_village_plunder_fx` przez czujnosc R1.
2. Przebudowa obrazka TYLKO przy zmianie stanu ksiegi: slot oznaczony jako brudny, przebudowa przy najblizszym pokazaniu (te same `Create` / `RemoveAll` co dzis, K:MapVillagesView.cs:2102). Nic co klatke.
3. Napisy "{VILLAGE} has been burned" (`VilBurned` K:VillageTexts.cs:10) tylko nad SPALONYMI (Jeff L; stan ksiegi `SBurned` - R10), prefab w `GUI\Prefabs`,
   pozycja `MBWindowManager.WorldToScreenInsideUsableArea`, tylko z bliska, najwyzej ok. 30 w kadrze. **(v2, krytyka W4-warstwa)** NIE wzorem `CraftPopup` (K:CraftPopup.cs:216-221 to okno
   modalne: `SetInputRestrictions`, `IsFocusLayer`, `TrySetFocus` - zjadaloby klikniecia albo zostawalo na zlym ekranie): napisy jako `MapView` (`MapScreen.AddMapView`) albo warstwa
   dokladana przy aktywacji `MapScreen` i zdejmowana przy dezaktywacji - bez ograniczen wejscia i bez fokusu, ukryta przy menu, rozmowie, encyklopedii i w misji; pozycje przeliczane
   co klatke tylko dla napisow w kadrze (najwyzej 30).
4. NIE: trwale "looted" wsi gry przy polowie osad (postfiks `SettlementVisual.RefreshLevelMask`, WM 5.5) - najwieksze ryzyko widoku, rozdz. 7.
**Klucze:** `BurnedVillageLook` = true (`// a village shrinks as its settlements empty, and a burned one stands dark`); `BurnedVillageLabels` = true (`// "X has been burned" over burned villages, close up only`).
**Linia logu:** w linii "Wioski" (L:36104) `| przebudowy obrazka n (zmiany stanu m), napisy w kadrze maks. k`; potkniecia per obiekt.
**Test (blok `etap7_w4`):** T1 przebudowy = zmiany stanu (0 przebudow bez zmiany); T2 zdjecie spalonej wioski (z R1 spalone > 0; jesli 0 - w kompilacji testowej jedna wioska ustawiona jako spalona);
T3 spadek klatek z napisami < 2% (tryb zdjec); T4 0 potkniec.
**Ryzyka:** wizerunki mapy (CLAUDE.md 7: CTD przy przebudowie co tick) - tylko przy zmianie stanu, licznik potkniec per slot; `SetFactorColor` na kopiach bez skryptu "Town Entity Manager" (WM 5.4) - zdjecie.
**NK:** nie. **Koszt:** kilka przebudow na dobe.

---

### E7/W5 - Proporczyk pana nad wioskami (238, Jeff 08.10 "wszystkie 8") - S - OSTATNIA

**Zakres (R17).** WL-P rozdz. 2: `MobilePartyVisual.GetBannerOfCharacter(banner, "campaign_flag")` jako osobna encja przy wiosce, tylko z bliska (kamera <= 90, najblizsze 80, budowa 12 na cwierc
sekundy), porownanie kodu herbu raz na sekunde (zmiana pana), przebudowa po misji (`ClearVisualMemory` - gra czysci tekstury herbow).
**Klucz:** `VillagePennants` = true (`// the lord's pennant flies over villages close to the camera`). **Prog:** spadek klatek > 5% w trybie zdjec - domyslna false i informacja dla Jeffa.
**Linia logu:** `Proporczyki (E7/W5): w kadrze n, budowa sr. x ms/klatke, przebudowy po misji m, potkniecia k`.
**Ryzyka:** nowe encje na mapie (historia AccessViolation W2) - budzet na klatke, licznik potkniec per encja, brak odczytu nazw prefabu. **NK:** nie. **Koszt:** klatki, nie doba.

---

### E7/D0 - Proba drog do pokazania Jeffowi (239, PLAN 1.13 -> 7.5) - S - PRZENIESIENIE TERAZ, ZDJECIA W TESCIE ZBIORCZYM

**Zakres (R3).** Galaz `w-toku/p2-drogi-proba` 95f05be (08.10, na drzewie z paczki 161): `MapRoadsProbe.cs` (652 linie - stemple sciezek ROT na 2 oknach 50 x 50: Fairmarket, Winterfell;
budzet 8 ms na tick, LOD przy zmianie kamery), `Prefabs/arm_map_roads.xml` (6 prefabow z samym `decal_component`), `ModuleData/arm_map_roads_probe.tsv` (641 stempli), MCM `MapRoadsProbeEnabled` = false.
Przeniesc na drzewo etapu 7 (konflikty tylko `ArmouryBehavior.cs` - `Reset`, i `SubModuleMain.cs` - rejestracja). **(v2, krytyka D0-dwa-DLL) Jeden DLL:** stala `ForceOnInProbeBuild`
usunieta; probe wlacza klucz `MapRoadsProbeEnabled` = true w konfiguracji autotestu (kopia `Armoury.json` testu, nie plik Jeffa) tylko na **osobny krotki bieg zdjec**
(kilka dob, zdjecia obu okien z bliska i z daleka + licznik klatki z logu); bieg A (120 dob) i reszta testu zbiorczego - na tym samym DLL z kluczem false (proba doklada do 8 ms na tick
i zafalszowalaby pomiary kosztu i klatek). Wgrywany DLL i pliki (prefab XML, tsv) = dokladnie te z testu (md5) - zgodnie ze stala zgoda Jeffa. Jeff oglada -> pytanie 2.
**Przed kodem sprawdzic:** czy siec drog z sesji "drogi" (08.10, "95.7% wiosek polaczonych") liczona jest na ukladzie v4 2 446 wiosek (RB7 4.1: siec z `B\wioski-v4\drogi` trafia w 843 z 2 446 -
wtedy przeliczyc `s6_siec.py` na `ModuleData\arm_map_villages.tsv`, ok. 7.6 s); proba stawia tylko stemple farby ROT, wiec do zdjec wystarcza.
**Test:** zdjecia + "MapRoadsProbe: stempli N, czas stawiania x ms, klatka y ms" w logu (bieg zdjec); w biegu A linia "MapRoadsProbe: wylaczona"; 0 bledow. DLL do wgrania = DLL z testu (klucz false).
**Ryzyka:** prefab XML z bledem = blad przy starcie - (v2) jedzie do Jeffa ten sam plik, ktory przeszedl start w tescie; przy bledzie startu w tescie prefab i tsv nie ida do wgrania. **Koszt:** klatki (tylko w biegu zdjec).

### E7/D1 - Widok drog (240, PLAN 7.5) - D - PO TESCIE ZBIORCZYM I DECYZJI "ILE DROG"

**Zakres (R3, R17).** Siec na ukladzie v4 (D-0 z a08), decale jak tropy gry (pula 2 048), menedzer komorek 50 x 50, LOD, budzet stempli na klatke (DROGI.md 4: 150-550 decali w kadrze); drogi "gdzie
wioska, tam droga" (Jeff 09.10) w liczbie wedlug pytania 2; Valyria (D-3): ciemniejszy stempel na traktach z lore (Pentos - Ghoyan Drohe - Norvos - Qohor, demon road), w Essos poza nimi tylko szlaki.
**Klucz:** `MapRoadsVisible` = true (`// unpaved roads drawn on the campaign map - looks only, nobody moves faster on them`). **Test:** osobny (klatka +< 1 ms, zdjecia). **NK:** nie. **Koszt:** klatki.

### E7/D2 - Premia za droge (241, PLAN 7.5) - M - TYLKO PO "TAK" JEFFA (pytanie 1), DOMYSLNIE WYLACZONA

**Zakres (R3).** RB7 4.2: maska `arm_map_roadmask.bin` (raster 1 jedn., ok. 320 KB, naglowek crc sceny + crc ukladu wiosek; zly crc = premia wylaczona + linia w logu); postfiks na kazdym `CalculateFinalSpeed`,
`SpeedDepth.OutermostFinal`, (v2, krytyka D2-priorytet) **Priority.LowerThanNormal** (pewnie po `TerrainEase` - Normal, K:TerrainEase.cs:161-162 - a przed sufitem `MarchPace` i dlugiem snu - oba Priority.Last;
przy rownym Normal kolejnosc zalezalaby od wpiecia, a wzor liczy procent biezacego wyniku); premia przed sufitem - kolumny piesze na suficie nic nie zyskaja (K1-05): na ladzie, poza lasem i brodem, bit maski = 1 ->
`__result.Add(pct/100 x wynik / (1 + SumOfFactors), "Road")` (dokladnie +pct% wyniku, wzor K:MarchPace.cs:131-135); latka na prywatnym `MobileParty.IsLastSpeedCacheInvalid` (G MobileParty.cs:3056-3071):
zmiana bitu maski pod partia -> `true` (bez tego premia "przykleja sie" do sciany siatki 8-10 jedn.); slownik partia -> bit bez zapisu. Dla WSZYSTKICH partii.
**Klucz:** `RoadSpeedBonusPercent` = 0 (`// every party moves this much faster while on a road (0 = off)`). **Test:** linia "Road" w rozpisce gracza na drodze, znika po zjechaniu; licznik wywolan latki cache
w pierwszej godzinie > 0 (JIT nie wkleil); "Miara: marsz" rosnie zgodnie z udzialem drog; miasta kupuja >= 98% plonu (wozy szybsze, `MarketCarts.PerDay` K:MarketCarts.cs:338-345 premii nie widzi).
**Koszt:** 0.04-0.1 s/dobe (RB7 4.2) - przy x8 mozna zdlawic do co 0.05 h gry na partie.

---

## 4. Test i progi etapu (nowe "Skonczone, gdy" - zamiast PLAN:218)

**Standard** (P6 rozdz. 4, zasada Jeffa 12.10 10:30 - SP:1263-1266): jeden test zbiorczy po scaleniu kodu etapow 5-8 - bieg A 120 dob NOWEJ kampanii (NK: E7/R1, R2), bieg zapisu 9 dob
z konca biegu A (pamiec wojny, sesje rabunku, stary stan), proba zimy i bieg dlugi wedlug planu testu zbiorczego; do tego wczytanie kopii zapisu Jeffa (doba 362) - 0 bledow. (v2, krytyka D0-dwa-DLL)
JEDEN DLL: osobny krotki bieg zdjec z kluczem proby drog (E7/D0) i trybem zdjec (W2, W3, W4, W5) w konfiguracji testu; bieg A bez proby; wgrywany DLL i pliki = testowane (md5). Niezdany prog - bisekcja wylacznikami (kazda paczka ma swoj), nie nowy kod.
**Zadnego progu "wobec innego biegu" poza tempem** (P6 K1-08): test idzie po etapach 5-6 (inne wojny, wiecej jazdy E6/K3), wiec progi paczek sa bezwzgledne albo liczone w TYM SAMYM biegu
(probka "na sucho" E7/T, licznik "by bylo" W1). Narzedzie: `python tools/sprawdz_logi.py --grupa etap7` (bloki `etap7_0`, `_t`, `_k`, `_c`, `_o1`, `_b`, `_w1`..`_w5`, `_r1`) + `--crashscribe`.

**Skonczone, gdy:**

| Prog | Jak mierzony | Paczka | Dzis |
|---|---|---|---|
| **tempo marszu bez zmian** - partie > 300 ludzi albo wodzowie armii: srednia median dobowych d91-120 w 124-138 km/dobe (+-5% od 131) | "Miara: marsz" (`SCR\e7P\marsz.py` w bloku `etap7_0`); poza progiem - najpierw bisekcja wylacznikami E6/K0, E6/K3, E7/T, E7/O1 (E6 tez zmienia jazde i konie) | wszystkie | 131 (L:27132-35849) |
| (7.4, v2) z rozpiski gry: netto lasu/pustyni po naszym postfiksie <= 0, przy cesze/perku = kara Jeffa x (vf - feat)/0.3; probek las z cecha >= 50 partio-godzin; ponizej sufitu `MarchPace` (karawany, wozy wsi, probki bez "Marching column"): Polnoc las < rownina, Dorne pustynia <= rownina; miasta kupuja >= 98% plonu | "Kary terenu (E7/T)" | E7/T | brak pomiaru; z kodu +12% / +5% (ponizej sufitu) |
| (7.6, v2) obudzonych cudza reka razem <= 10 na noc, w tym lordowie <= 3; zjazd sr. <= 0.15 jedn.; w ruchu bez powodu <= 3 lordow na noc (wyjazd z osady <= 1); AI spiacych lordow wstalo przed switem = 0; dluznicy obudzeni o 6:00 = 0 | `noc.log` "AiNightCamp", L "NightRest swit" | E7/O1 | 91 (79% karawany) / 0.40 / ok. 20 / brak pomiaru (N:2835) |
| (7.6) dlug snu 3 AI = 0; kara jak u gracza (zgodnosc z tablica 3/3) | "NocnyMarsz: swit", "NocnyMarsz: kara" | (T10) | 0; 3/3 (L:35797-35798) |
| (7.6) zbiorka: czlonkowie armii w drodze do wodza spia >= 95% partio-godzin okna obozu | "Zbiorka (E7/0)" | E7/0, E7/O1 | brak pomiaru |
| (7.6, Jeff M; v2) linia startowa CampScene (bez fizyki); namioty w napadzie na oboz, wojsko nie grzeznie - test Jeffa | "CampScene (E7/O2)" | E7/O2 | wylaczone |
| (7.7) zadne okno >= 14 dob bez rozpoczetego turnieju gry; starty w kazdym tygodniu pory 0-12 | "Turnieje (E7/0)" | E7/K | 61 dob zera; tygodnie 5-12: 0 (L:10932-28898) |
| (7.7) nowe sojusze 364 dni, porozumienia wezwan "przez 182 dob"; wojen naraz d61-120 >= 7 | "Wezwania do wojny (168)", KRONIKA (CS) | E7/K | 42 dob (L:28628); 14-17 |
| (7.7) kazda wojna i pokoj z przyczyna; przerwy pokoj -> wojna w skali roku 364 - wedlug odpowiedzi na pytanie 3 (gdy TAK: >= 90 dni poza wezwaniami 168) | "Wojna i pokoj (E7/0)" | E7/0, (Diplomacy) | 3 / 21 / 24 dni (CS:3334-4158) |
| (7.7, v2) rozejm gry: zadna nowa wojna z decyzji krolestwa miedzy para, ktora zawarla pokoj < 87 dni wczesniej (poza wezwaniami 168 i sciezka Diplomacy - pytanie 3); postfiks rozejmu wpiety | "Wojna i pokoj (E7/0)", startowa "Czas swiata (E7/K)" | E7/K | rozejm gry 20 dni |
| (7.1) zadna logika gry nie liczy sekund realnych ani klatek (wyjatki: widok, komunikaty, `CrossingLaw` - etap 8); tekst MCM bez "168" | grep w bloku `etap7_c` | E7/C | `HideoutPurge` 3 s realne |
| (7.8) plik lore 2 446 / 2 446, bramka 5A8C2735, kontrole L0 | narzedzie L0 + linia startowa W2 | E7/L, W2 | brak pliku |
| (7.9) dymek z herbem, rycerzem i Alt (zdjecia 3 krain); wiara = dominujaca BK; menu okregu w `village` i `village_looted`; 0 potkniec widoku i dymka | "Dymek wioski (E7/W2)", "Wioski" (potkniecia - L:36104 dzis 0), zdjecia | W2, W3 | linia pana bez herbu |
| (7.9) proporczyki: spadek klatek < 5% (inaczej domyslnie wylaczone) | "Proporczyki (E7/W5)", tryb zdjec | W5 | - |
| (7.10, v2) "plonie" = sesje rabunku E4/5 + R1 (+-1/dobe); komunikatow "by bylo" > 0 (autotest bez lenna); "burned" (komunikat i pamiec) tylko przy stanie ksiegi `SBurned`, "plundered" - nigdy przy `SBurned`; pamiec wojny przechodzi zapis -> wczytanie (w `m:` same identyfikatory); stary zapis Jeffa - 0 bledow | "Wioski - wiesci (E7/W1)", "Ksiega wiosek (E4/11)", bieg zapisu | W1 | brak komunikatu |
| (7.10) wioski spalone > 0 w 120 dob; przebudowy obrazka = zmiany stanu; zdjecie spalonej; napisy tylko nad spalonymi | "Ksiega wiosek (E4/11)", "Wioski" | R1, W4 | 0 spalonych (L:389-35852) |
| (7.11, v2) niezalezny bilans R1: zloto wodza = ubytek kiesy wsi, towar napastnika + spalone = ubytek magazynu, zjedzone = ubytek spichlerza (roznice 0); "Hearth (E4/0)" lup z niczego 0 zl; trafieni = zabici + las + jency + uchodzcy (kontrola 0); jency z rabunkow R1 > 0 (najezdzcy z niewola); zabici `RaidDead` = zabici R1; mediana dob na osade lorda <= 5; udzial przerwanych - odczyt | "Rabunek osada po osadzie (E7/R1)", "Hearth (E4/0)", "Ludzie (spustoszenie)" | R1 (+ E5/A7) | 80 767 zl / 120 dob (L:634-36086) |
| (7.11) okienko gracza, "End the raid", wczytanie w stanie decyzji - 0 bledow | test Jeffa | R2 | - |
| (7.12) linia "Przeprawa Bliznaki" 120/120 dob, przejsc > 0, pozycje przejsc z dob 1-3 <= 3 jedn. od bramy | "Przeprawa Bliznaki (E7/B)" | E7/B | tylko wejscia: 503 (L:556-36012) |
| (7.5) zdjecia proby obejrzane przez Jeffa, decyzja "ile drog" i odpowiedz na pytanie 1 | zdjecia z kompilacji testowej | E7/D0 | proba niepokazana |
| **0 bledow naszych modow** (Armoury, CrashScribe, GrandTourney) | logi + `--crashscribe` | wszystkie | 0 (WGRANIE 33) |
| **koszt etapu 7** <= 0.1 s/dobe gry bez widoku drog (< 1% doby 11.7 s), widok wiosek i proporczyki - klatki | stoper w liniach paczek, "Koszt 171-174" | wszystkie | - |
| **regresja E5/E6:** miasta kupuja >= 98% plonu; progi etapow 5 i 6 z testu zbiorczego bez spadku przez etap 7 (bisekcja wylacznikami E7) | linie etapow 5 i 6 | R1, O1, K | z testu zbiorczego |

7.5 (D1, D2) - osobny test po decyzjach Jeffa: klatka +< 1 ms przy widoku; przy premii "Road" w rozpisce i znikanie po zjechaniu, "Miara: marsz" rosnie zgodnie z udzialem drog, miasta >= 98% plonu.

---

## 5. Ryzyka / kontrola calosci (zasada 0)

### 5.1 Wspolne cele latek i stan (plik:linia)

| Cel / stan | Kto dzis | Kto w etapie 7 | Co pilnowac |
|---|---|---|---|
| `CalculateFinalSpeed` (6 modeli: gra, ROT, RBL, NavalDLC, BK, BKROT) | `SpeedDepth` (Priority.First + finalizer, K:SpeedDepth.cs:35-61), `TerrainEase.SpeedPostfix` Normal (K:TerrainEase.cs:62), `NightRest.SpeedPostfix` Last (K:NightRest.cs:1779), `MarchPace.SpeedPostfix` Last (K:MarchPace.cs:95-150); E6/K0 tylko czyta | E7/T (w istniejacym postfiksie), E7/D2 (nowy postfiks Priority.LowerThanNormal - v2 - po TerrainEase) | wszystko tylko na `OutermostFinal`; sufit `MarchPace` zawsze ostatni (v2: obcina nadmiar terenu i premie drogi u kolumn pieszych - szacunki i progi E7/T, D2, pytanie 1 to uwzgledniaja); zadnej pracy ciezkiej (wolane przy kazdej zmianie sciany siatki) |
| `MobileParty.IsLastSpeedCacheInvalid` (private, G MobileParty.cs:3056) | nikt | E7/D2 (tylko po TAK) | licznik wywolan przy starcie (JIT) |
| `MobileParty.SetMove*` (12, G :3919-4028) | `Wayfinder` postfiksy GoToPoint/GoToSettlement (tylko `MainParty`, K:Wayfinder.cs:28-37, :143-146); (v2) BK `AiDecisionTracePatches` - **13 celow**: 12 `SetMove*` + `SetSailAtPosition` (BK :22-285; prefiksy bool `GuardSettlementMove`/`FreezeWatchdog.Enter` z `ref navigationType`, postfiksy `FreezeWatchdog.Exit`) | E7/O1 (`SleepGate` prefiksy Priority.First na 10 metodach - bez Hold, ToNearestLand, GoToInteractablePoint) | nasze oddanie rozkazu przepuszczone flaga; po naszym false prefiksy BK pominiete, postfiksy biegna - nieszkodliwe (Harmony 2.4.2, sprawdzone - v2) |
| `MobilePartyAi` termin `_enableAgainAtHour` | `HoldSleepers`/`AiNightCamp`/T10 `DisableForHours(1)` (K:NightRest.cs:444, :838, :900; K:NightMarch.cs:776, :842, :1445, :1486, :1547, :1623); `ForeignHold` (K:NightMarch.cs:238-252) | E7/O1 (v2: odnawianie < 0.6 h dla obozu, dlugu i osady `_townHold`, `EnableAi` o swicie dla obozu i osady) | cudzej blokady (> 1.5 h) nie skracac; (v2) nowy getter tylko dla odnawiania, `ForeignHold` bez zmian (8.17) |
| `_orders` / `NightOrder` / `ApplyOrder` (K:NightRest.cs:582-690); `_debtSleep[mp].Order` (K:NightMarch.cs:95, :1482, :1503) | T1/T10 (rozkaz zapamietany przed snem, oddanie o swicie, alarm; dluznik - przy `ReleaseDebt`) | E7/O1 (v2: blokada zapisuje rozkaz tam, gdzie trzyma go sen - oboz `_orders`, dlug `ds.Order`; pola `Nav`/`Port`/`Point`) | jedno miejsce oddania rozkazu na rodzaj snu; `IsCamping` (MaterialOrders) bez zmian; waznosc sprawdzana przy oddaniu jak dzis |
| `DefaultTournamentModel.GetTournamentStartChance` (G :21-32) | nikt (GrandTourney lata koniec turnieju w Default i NavalDLC) | E7/K (prefiks) | NavalDLC przekazuje do Default (RB7 6.1) - sprawdzic jeszcze raz linia startowa "czynny model" |
| `DefaultAllianceModel.MaxDurationOfAlliance` / `MaxDurationOfWarParticipation` (G :112, :114) | czyta `CrownCallToWar.Days()` (K:CrownCallToWar.cs:60) | E7/K (postfiksy getterow) | rata dzienna 168 bez zmian; wpinac w kampanii (wzor `EnsureHooks`) |
| (v2) `KingdomDecisionProposalBehavior.GetRandomWarDecision` (private, G :234-249) | BK postfiks (`GetRandomWarDecisionPatch`, BK DiplomacyPatches.cs:69-115: rozejm BK, zamiana na `BKDeclareWarDecision`) | E7/K (postfiks Priority.Low - rozejm gry ok. 87 dni) | tylko `null` przy rozejmie, nigdy nowa decyzja; cel z `FactionToDeclareWarOn` (takze BK) |
| `MakePeaceAction.Apply` w `CrownCallToWar` (K:CrownCallToWar.cs:222) | 168 | E7/0 (flaga przyczyny wokol wywolania) | tylko flaga, bez zmiany logiki |
| `RaidEventComponent.Update` (G :160) | E4/5 prefiks void z `out __state` (podmiana hearth) i finalizer (K:Devastation.cs:330, :363; Priority.Normal); okno W6 `Measure40` void + finalizer (pomija wsie E4/5, K:Measure40.cs:94, :446); E5/A7 (plan: okno platnika lupu) | E7/R1 (v2: prefiks **Priority.First**, false w lupieniu dla rabunkow `DistrictRaid`, decyzja `Owns` na poczatku rabunku) | (v2) prefiksy void biegna po naszym false - straznik `Owns` na samej gorze `Devastation.RaidPrefix`; E5/A7 omija `Owns`; jeden platnik lupu (funkcja E5/A7); oryginal nigdy nie biegnie w lupieniu R1 (koniec - R1 pkt 7) |
| `RaidEventComponent.OnBeforeFinalize` (G :128) | E4/5 `EndPrefix`/`EndFinalizer` (K:Devastation.cs:405-440) | E7/R1 (v2: przy `Owns` pomija tylko `Settle` i podmiane hearth, `StateOf` zostaje), R1 `RaidDamage` przed `RaidCompleted` | jeden `RaidCompleted` na sesje (nasluchy E4/9, `WatchLaw`, `OutlawLaw`, ROT, BKROT, BEE, Diplomacy) |
| (v2) stan rabunku `Devastation._raids` / `RaidState` (K:Devastation.cs:71-82) | E4/5 (osobodni, zabici), E4/9 `TakeCaptives` (jency), OthersRise `RaidDead`, ksiega wiosek `rs.Comp` | E7/R1 (zaklada i wypelnia przez `StrikePeople`), W1 (odczyt przy `RaidCompleted`) | jeden stan na rabunek; usuwany tylko w `EndFinalizer` (po nasluchach konca rabunku) |
| `VillageHealCampaignBehavior.DailyTickSettlement` | nikt (milicja +20 przy odbudowie - `IncreaseSettlementHealthAction.ApplyInternal`, juz E4/8 K:ArmsFromPeople.cs:969) | E7/R1 (czujnosc - tylko odklada leczenie) | tylko wsie w czujnosci; milicji R1 nie rusza (v2) |
| `CampaignEvents.VillageStateChanged` (G CampaignEvents.cs:659) | gra (`CommentOnChangeVillageStateBehavior` - wpis dziennika), BEE (na `VillageBeingRaided`) | E7/W1 (sluchacz) | dedup milicji (2 zdarzenia na rabunek) |
| klucz zapisu `arm_district` | `VillageBook.Export/Import` (K:ArmouryBehavior.cs:461, K:VillageBook.cs:187, :441) | W1 (sekcja `m:`), R1 (`r:`, `c:`) | jeden pisarz; brak sekcji = stan pusty (stary zapis) |
| `MapVillagesView.Visual.OnHover` (K:MapVillagesView.cs:4604) | dymek W2/T7 | E7/W2 | `StateLine`/`HeldLine`/`RaiderName` - jedno zrodlo |
| obrazki wiosek (`Create`/`RemoveAll`, K:MapVillagesView.cs:2102) | W2 (ogien i dym przy zmianie stanu, `PollRaids` :1874-1920) | E7/W4 (poziom, przyciemnienie), E7/W5 (proporczyki) | tylko przy zmianie stanu; licznik potkniec per slot; bez nazw prefabow |
| menu `village` / `village_looted` | "People of this land" (K:PeopleMenu.cs:29-32); BK i ROT opcje | E7/W3 | jedna lista wiosek; index opcji |
| `HourlyTickEvent` | wiele (NightRest, Measure, ...) | E7/T (probka), E7/B (Bliznaki) | probki tylko odczyt; koszt w linii |
| `MobileParty.StartFindingLocatablesAroundPosition` | `NightMarch` (K:NightMarch.cs:564) | E7/B | zapytanie siatki, bez petli po `MobileParty.All` |
| `CampScene` (misja, K:SubModuleMain.cs:293) | wylaczony | E7/O2 (v2: bez fizyki, nic w promieniu 3 m od gracza) | tylko misja; prefaby sprawdzane raz (`PrefabExists`) |
| `HideoutPurge.OnTick` (K:HideoutPurge.cs:135) | odwet co 3 s realne | E7/C | zegar gry |

### 5.2 Spojnosc logiki

1. **Zamknieta ekonomia:** lup z rabunku tylko z kiesy wsi, magazynu wsi i spichlerza (R15) - jeden platnik z E5/A7; zniszczone sztuki = ujscie "spalone w rabunku" (przyczyna jak "polegl" u koni),
   koniec towaru i zlota z niczego w rabunkach (G RaidEventComponent.cs:179-188, :273-299). Turnieje: wiecej turniejow = wiecej nagrod, ale nagrody maja platnika od 164a ("Szczelnosc", L:36077 -
   "uciete" gdy platnik bez nadwyzki) - zadnego nowego zrodla. Wezwania 168: cena x4.33, rata ta sama, platnik ten sam (skarbiec wzywajacego). Myto - zadnego w etapie 7 (licznik "gdyby bylo").
   Droga - bez premii (dopoki Jeff nie powie TAK). (v2) Czujnosc R1 milicji nie rusza (+20 przy odbudowie zamyka juz E4/8). Bilans lupu R1 mierzony niezaleznie (R1 pkt 8).
2. **Jedna zasada na zjawisko:** sen - jedna regula (R5) i jedna funkcja `SleepKind` dla obozu, snu dlugu i lordow w osadzie, jeden straznik, jedno oddanie rozkazu (`ApplyOrder`) z magazynu danego snu (v2); kary terenu - netto gry (R2) dla wszystkich partii i gracza;
   czas kalendarza w modelach gry (rok, pol roku, rozejm) = `Calendar.DaysPerYear` (R8, v2); stan wioski - ksiega (R10), zdarzenie gry tylko dla komunikatu chwili; lore - jeden plik i jedno wczytanie (R11); lista wiosek - jedno okno (R13);
   pamiec wojny - ksiega (R14); ludzie z rabunku - jedna funkcja Devastation (`Strike`/`StrikePeople`), lup - jeden platnik (E5/A7); wybor wioski z ogniem - `VillageBook.FxUid` dla widoku, komunikatu
   i pamieci; przeprawa - licznik (R16), `CrossingLaw` osobno (etap 8).
3. **Kolejnosc w godzinie i dobie:** godzina obozu: tick godzinny partii (gra) <-> nasz przeglad co 0.1 h (E7/O1 odnawia termin, zanim minie) -> blokada `SetMove*` w kazdej chwili nocy -> swit (6:00):
   oddanie rozkazow obozu (`ApplyOrder`, ze sprawdzeniem waznosci) i `EnableAi` (oboz, osada); dluznik - rozkaz przy `ReleaseDebt` (v2); doba: rabunki (R1 krok co tick mapy) -> `RaidCompleted` (nasluchy) -> `VillageStateChanged` (W1) -> ksiega wiosek
   `CheckUp` (stan, W4 brudne sloty) -> linie dobowe po `MoneyLedger.Daily` (E7/0, E7/T, E7/B, W1, R1).
4. **Stan miedzy kampaniami:** `WorldClock7` (pary wojen, turnieje - sesja), probka E7/T (sesja), `SleepGate` (bez stanu; flaga watku), `TwinsCrossing` (slownik w strefie - sesja), dedup W1 (sesja),
   `MapVillageLore` (statyczny, wczytany raz - przy zmianie kampanii bez zmian, bo dane mapy), pamiec wojny i sesje R1 (zapis `arm_district` przez ksiege), sloty W4 (od nowa przy wczytaniu);
   `Reset()` w konstruktorze `ArmouryBehavior` (wzor E5/0, E6/0 - :395). Proba drog D0 - `MapRoadsProbe.Reset()` (jak w galezi P2).
5. **Nie liczyc drugi raz:** ludzie rabunku - E4/5 pomija osobodni rabunkow R1 (straznik `Owns`), ale stan rabunku jest jeden (v2); milicja po odbudowie - tylko E4/8; lup - E5/A7 pomija rabunki R1; zwrot kary terenu - jeden postfiks (E7/T zmienia istniejacy, nie doklada drugiego);
   rozkaz u spiacego - blokada albo straznik (straznik tylko dla zjazdu bez rozkazu); premia drogi D2 - poza lasem (las juz spowalnia), `Add` od biezacego wyniku.
6. **Martwy kod ozywiony:** E7/O1 - zaden (straznik zostaje); E7/C - `HideoutPurge` dziala jak dzis, tylko rowno; `CrossingLaw.AiTick` - NIE ozywiamy (etap 8; ozywienie = zawracanie wrogow przy 3
   przeprawach - CLAUDE.md 8 pkt 4); E7/R1 - prefiks `VillageHealCampaignBehavior` dla czujnosci (nowa praca tylko dla wsi Looted, kilkanascie naraz). Teksty `VilLog*`, `Dist*`, `Raid*` (dzis
   nieuzywane) - zaczynaja byc uzywane przez W1/W3/R2 (napisane po angielsku 07-08.10, sprawdzic tresc).
7. **Kalendarz:** pora = 91 dob = 13 tygodni (364/4); turnieje w cyklu 3 tygodni jak w roku 84; dokumenty sprzed 05.10 (rok 168) - ich liczb "na dobe" nie uzywamy (AUDYT-KALENDARZ.md:169, HISTORY.md:20, 45).

### 5.3 Cudzy kod (sprawdzone w dekompilacji do tego projektu)

- G `DefaultPartySpeedCalculatingModel.CalculateFinalSpeed` :256-330 - las :264-283 (ForestKin :268-277, cecha lasu :279-282), brod :285-288, pustynia :291-295, snieg, noc (E7/T).
- G `MobilePartyAi` :437-442 (wlaczenie po terminie), :1561-1565 `DisableForHours`, :1568-1571 `DisableAi` (Never); G `AiPartyThinkBehavior` :49 (bez myslenia przy wylaczonym AI), :94 (Hold -> nowy cel od razu)
  - podstawa fazy E7/O1; `SetAiBehavior` (G MobilePartyAi.cs:1405-1410) ustawia tylko zachowanie krotkie, cel "rozkazu" (DefaultBehavior) daje `SetMove*`.
- G `MobileParty` :3907 `SetMoveModeHold`, :3919-4028 dwanascie `SetMove*` (E7/O1).
- G `DefaultTournamentModel` :21-32 (tydzien :27), `SuitableForTournament` :45 private (E7/K).
- G `DefaultAllianceModel` :112, :114, cena :569, :583; `AllianceCampaignBehavior` :463, :489 - koniec zapisany przy zawarciu (E7/K).
- G `CampaignEvents` :659 `VillageStateChanged`, :685 `TournamentStarted`, :687 `WarDeclared` (z `DeclareWarDetail`), :753 `MakePeace` (z `MakePeaceDetail`) (E7/0, W1).
- G `RaidEventComponent` :128 `OnBeforeFinalize`, :160-300 `Update` (zloto z niczego :179-188, magazyn z 50% zniszczen :204-264, towar z niczego :273-299) (R1).
- BK `AiDecisionTracePatches` :22-285 - (v2) 13 celow (12 `SetMove*` + `SetSailAtPosition`), prefiksy bool + postfiksy; BK `BKCaravansBehavior.HourlyTickPartyImpl` :828-839 i `ReleaseCaravanFromHold` :298-367
  (zwolnienie karawany z Hold bez patrzenia na `IsDisabled` - przyczyna 79% obudzen) - E7/O1.
- (v2) G `KingdomDecisionProposalBehavior.GetRandomWarDecision` :234-249 (rozejm 20 dni :242) + BK `GetRandomWarDecisionPatch` (DiplomacyPatches.cs:69-115); BK rozejmy `YearsFromNow` (KingdomDiplomacy.cs:449-453) - E7/K.
- (v2) G `IncreaseSettlementHealthAction.ApplyInternal` :9-15 (+20 milicji) - R1 nie rusza; G `MapEvent.BattleState` setter internal :284-291, `RaidEventComponent.RaidDamage` setter prywatny :30, `OnFinalize` :146-158 - R1.
- (v2) TaleWorlds.Engine `GameEntity.Instantiate(scene, name, frame)` tworzy fizyke (`SCR\e7krit2\GE.cs:398-401`), przeciazenia bez fizyki :379, :403, `PrefabExists` :423-425 - E7/O2.
- Armoury (nasze, w roli "cudzego" dla paczki): `Wayfinder` :28-37, :143-146; `IslandRoads.cs:46`, `MaterialOrders.cs:702`; GrandTourney `SummonLords` (TourneyBehavior.cs:262-320).
- **Do sprawdzenia przed kodem:** ~~(1) prefiksy po `false` i `FreezeWatchdog.Enter/Exit`~~ - (v2) zamkniete: Harmony 2.4.2 pomija prefiksy bool / z ref, postfiksy biegna, `Exit` nieszkodliwy;
  ~~(2) `GameEntity.PrefabExists`~~ - (v2) zamkniete: jest (GE.cs:423-425); (3) NavalDLC `GetTournamentStartChance` -> BaseModel (RB7 6.1 - potwierdzic w linii startowej); (4) czy gra/BK/SCA wolaja `SetMove*` u wodza przy doczepianiu do armii w nocy (`Army.cs`,
  RB7 11); (5) BK `TitleManager` - posiadacz tytulu wsi (odbicie, TECH 1.2) i id wiar BK do tabeli lore (W2); (6) funkcja platnika lupu E5/A7 (nazwa i miejsce - po kodzie E5/A7) (R1);
  (7) ROT `AIThinkPatch` - przerwania rabunku co godzine, takze na morzu (RB K12) i NavalDLC rabunek z morza (ten sam komponent) (R1); (7a, v2) sluchacze `OnItemsLooted` w ROT, BKROT, BEE, DTE, NavalDLC (R1 pkt 7); (8) siec drog sesji "drogi" na ukladzie v4 (D0/D1);
  (9) JIT a `IsLastSpeedCacheInvalid` (D2); (10) AIInfluence (zaciemniony) - wlasne stale sojuszy/turniejow (RB7 11) - tylko odczyt KRONIKI po tescie.

### 5.4 Gra Jeffa

- Klucze w `Armoury.json` (12.10) dotykane przez etap 7: `TerrainEaseEnabled` true (:110), `ForestSpeedPenalty` 0.1 (:79), `DesertSpeedPenalty` 0.2 (:73), `NightSpeedPenalty` 0.5 (:89),
  `WorldPacePercent` 75 (:118), `SpeedAuditEnabled` (:108), `HideoutPurgeEnabled` (:133), `CrossingLawAi` / `CrossingLawEnabled` (:154-155), `AiCampsAtNight` (:291), `NightRestEnabled` (:319),
  `CampBattlePropsEnabled` false (:302). Projekt zmienia tylko `CampBattlePropsEnabled` (decyzja M) - wpis w pliku przy wgraniu (gra zamknieta, `.bak`); reszta bez zmian.
- Nowe klucze (zadnego nie ma w `Armoury.json`): `Stage7MeasureLog`, `TerrainEaseExact`, `TournamentsAllSeason`, `DiplomacyYearLong`, `HideoutPursuitHours`, `SleepersHoldOrders`, `TwinsCrossingCount`,
  `VillageRaidMessages`, `VillageWarMemory`, `VillageTipLore`, `DistrictMenuEnabled`, `RaidBySettlementEnabled`, `RaidDetachmentMen`, `RaidAlertMaxDays`, `RaidPlayerStepPopup`, `BurnedVillageLook`,
  `BurnedVillageLabels`, `VillagePennants`; po tescie: `MapRoadsVisible`, `RoadSpeedBonusPercent`. Przed kodem grep `Armoury.json` jeszcze raz (pulapka CLAUDE.md 7).
- Plik Diplomacy Jeffa (`Configs\ModSettings\Global\Diplomacy\DiplomacySettings_v1.2.json`) - tylko po TAK na pytanie 3 (gra zamknieta, `.bak` + D:).
- NK: tylko E7/R1, R2 (rabunek). Stary zapis: wszystko wczytuje sie (nowe sekcje tekstowe w `arm_district`, brak nowych typow zapisu).
- Wgranie: nowe pliki poza DLL - `ModuleData\arm_map_village_lore.tsv` (E7/L), katalog `GUI\Prefabs` (W2, W4); po tescie `ModuleData` drog (D1/D2). Skrypt wgrania (dzis DLL + ModuleData) - dopisac `GUI`.
- Widoczne dla Jeffa: Polnoc i Dorne wolniejsze w lesie/na pustyni; lordowie nie ruszaja w nocy (armia zwolana noca rusza o swicie, turniej GrandTourney o swicie); namioty przy napadzie na oboz;
  turnieje przez caly rok, dluzsze sojusze; dymek wioski z herbem i rycerzem, "Look over the district"; komunikaty o rabunku jego wsi; rabunek osada po osadzie z okienkiem; spalone wioski ciemne,
  z napisem; proporczyki; w logu linia Bliznakow.

---

## 6. Pytania do Jeffa (tylko kierunek; liczby wybiera projekt)

**ODPOWIEDZI JEFFA (12.10 ok. 15:00 UTC):** (1) premia +15% po drodze - "ok" = rada: NIE (drogi tylko do patrzenia, D2 domyslnie 0); (2) ile drog - "ok" = rada: po zdjeciach proby D0, trakty + droga do kazdej wioski; (3) plik ustawien Diplomacy Jeffa przeliczyc na rok 364 - **TAK** (przy wgraniu koncowym, gra zamknieta, kopia .bak; gdy narzedzie nie pozwoli zmienic pliku - gotowe polecenie dla Jeffa); (4) myto na Bliznakach - **"chce, ale policz"**: E7/B (licznik) zostaje; stawki myta wyliczyc z licznika w tescie zbiorczym (i z lore Freyow), propozycja z liczbami do Jeffa, dopiero potem kod myta (nowa paczka po tescie, zamknieta ekonomia: myto do kiesy pana Bliznakow, placi kiesa partii).

1. **Drogi a predkosc (7.5).** 09.10 powiedziales "po drodze szybciej +15% - TAK", a 12.10 "zostaw predkosc poruszania sie jak jest". Premia +15% przyspiesza swiat tam, gdzie biegnie droga:
   (v2, po krytyce K1-05 - sufit marszu dziala PO premii) kolumny piesze lordow ok. 0% (i tak jada pod sufitem marszu ok. 3.0 jedn./h), jazda i male druzyny do ok. +5% na dobe,
   karawany i wozy wsi ok. +8% (sufit ich nie dotyczy; odcinki miedzy osadami biegna po drodze w 32% / 55% - a08 3.4); AI i tak nie wybiera drog (gra liczy najkrotsza droge bez terenu).
   (A) drogi tylko do patrzenia, predkosc bez zmian; (B) +15% po drodze dla wszystkich (Ty, lordowie, karawany, wozy) - wtedy "tempo jak jest" przestaje byc prawda na drogach.
   **Rada: A** (zgodne z 12.10, zero ryzyka dla ekonomii wozow i dla progow etapow 2 i 5; przy B zyskaliby glownie kupcy i wozy, nie armie); B jako suwak w MCM domyslnie 0 - do wlaczenia, gdy zobaczysz drogi w grze. Jesli B - zawsze dla wszystkich,
   nigdy tylko dla Ciebie.
2. **Ile drog (7.5) - po zdjeciach.** Proba (Fairmarket i Winterfell, farba drog ROT) jedzie w tescie zbiorczym; dostaniesz zdjecia. Wtedy wybierz: (a) tylko trakty miedzy twierdzami,
   (b) trakty + drogi do kazdej wioski ("gdzie wioska, tam droga" - Twoje 09.10), (c) jak (b) + sciezki polne miedzy wioskami. **Rada: (b)**, w Essos poza traktami z lore tylko szlaki,
   trakty valyrianskie ciemniejsze. Teraz nic nie trzeba odpowiadac.
3. **Twoje ustawienia Diplomacy na rok 364 (7.7)** - to Twoj plik, wiec pytam: pakt o nieagresji 84 -> 364 dni, minimalny sojusz 42 -> 182, minimalna wojna 21 -> 90, przerwa przed nowa wojna
   21 -> 90, wojna domowa 240 -> 1000 (wiecej suwak nie pozwala), frakcja buntu 120 -> 520. Skutek: wojny dluzsze i rzadziej wypowiadane na nowo (dzis ta sama para wraca do wojny po 21-24 dniach,
   CS:3334-3873). **Rada: tak** (ten sam rok co reszta swiata) - wpiszemy przy zamknietej grze z kopia; alternatywa: zostawic (wojny w rytmie roku 84).
4. **Myto na Bliznakach - czy w ogole (po liczniku 7.12)?** 09.10 przypisanie odpowiedzi 22/23 bylo niejasne (SP:816-817), a PLAN zapisal "myto: TAK". Licznik niczego nie pobiera.
   **Rada:** potwierdz tylko kierunek ("myto tak/nie"); stawki (np. 2 zl od czlowieka, 1 zl od zwierzecia, rody pana za darmo - ES:215) ustalimy jednym pytaniem z liczbami z testu.

**Bez pytania** (bledy albo decyzje juz podjete): 7.4, 7.6 (sen, namioty - Twoje M), 7.7 turnieje, sojusze i (v2) rozejm gry (PLAN 7.7 "blad"), 7.1, 7.8-7.12 (Twoje decyzje 07-09.10), proporczyk (Twoje "wszystkie 8"),
T-1 i blokady tempa (Twoje 12.10 - nie ruszamy predkosci).

---

## 7. Odlozone (i dlaczego)

| Rzecz | Dlaczego nie teraz | Kiedy |
|---|---|---|
| blokady tempa 7.1 (podloga 1.0, kary terenu x tempo, T-1 WorldPace jako mnoznik) | warunek odrzuconego 7.2; kazda zmienia tylko predkosc (T-1: jazda -7%) | skreslone (12.10) |
| `Campaign.Estimated*Speed` (AI liczy ok. 81 jedn./dobe, partie > 300 ludzi robia ok. 28) | zmienia wybor celow AI, poscigi, wizyty i dlugosc wojen bez zmiany tempa; nikt nie prosil; kolizje `MarketCarts.PerDay` (K:MarketCarts.cs:338-345), `HorseFate.cs:883`, `PartyBase.cs:653` (RB7 2.2) | pomysl - tylko z linia pomiarowa i przegladem KRONIKI |
| `CrossingLaw`: petla AI biegnie tylko przy graczu we wrogiej strefie (K:CrossingLaw.cs:73-76), zegar klatek (`_acc`, `_aiAcc`), meldunek na sekundach realnych | blad (etap 8, nie "przy okazji" - SP:1248); ozywienie zmieni gre (zawracanie wrogow przy 3 przeprawach - CLAUDE.md 8 pkt 4) | etap 8, osobna paczka (zegar gry + biegnie niezaleznie od gracza; spiacych chroni E7/O1) |
| myto na Bliznakach | po liczniku 7.12 i odpowiedzi na pytanie 4; zamknieta ekonomia (kiesa partii -> kasa Bliznakow), nie dublowac BK `CaravanFee`, jedna regula myta (a08 N-1) | po tescie zbiorczym |
| widok drog (D1) i premia (D2) | wymagaja decyzji Jeffa po zdjeciach (pytania 1-2) | po tescie zbiorczym |
| wies gry trwale "looted" przy polowie osad (postfiks `SettlementVisual.RefreshLevelMask`, WM 5.5) | latka na widok gry - najwieksze ryzyko widoku; W4 daje wyglad na kopiach W2 | po W4, osobny wylacznik, tylko gdy W4 nie wystarczy |
| "szesc nocy" - rozne godziny (sen 21-5, wzrok 21-4, warsztaty 23-5, ciemnosc gry 22-02) | rozne zjawiska (sen, wzrok, praca, ciemnosc) - scalenie zmieniloby rozgrywke | porzadek `DayClock` - etap 8 / Z.8 |
| koszt petli obozu (9 ms na tick, L:35797) | optymalizacja | 8.17 |
| cena wezwania 168 skacze (Pentos -> Tyrosh: 5 035 zl, potem 200 217 zl - L:28628, L:32798) | wzor ceny gry (G DefaultAllianceModel.cs:569); E7/K mnozy cene x4.33 przy tej samej racie - do obserwacji | pomiar w tescie zbiorczym; ewentualnie etap 8 |
| data CrashScribe "Day 3641 of Summer" (`GameState.cs:20`) | wyswietlanie | etap 8 |
| HISTORY.md:20, 45 i dokumenty z rokiem 168 | dokumenty | Z.7 |
| proporczyki domyslnie wylaczone, jesli > 5% klatek | koszt klatek | decyzja po tescie (informacja dla Jeffa) |
| ruch lordow w nocach 1-22 (56-134 partio-godzin) i urwanie od nocy 24 (N:46, N:492) | przyczyna nieznana (RA7 1.2); E7/O1 i tak zatrzymuje rozkazy - test pokaze, czy wraca | odczyt w tescie zbiorczym |
| (v2) jeden odczyt terminu AI dla `ForeignHold` i odnawiania (dzis `PropertyInfo.GetValue` w `ForeignHold`) | optymalizacja istniejacego kodu (krytyka K1-08; Jeff 12.10 - etap 8 nie przy okazji) | 8.17 |
| (v2) `RememberOrder` przy zasnieciu nie zapamietuje drogi (`Nav`) ani portu - rozkaz sprzed snu wraca z `Default` | istniejace zachowanie (krytyka O1-nawigacja dotyczy rozkazow zatrzymanych - te maja `Nav`/`Port` od E7/O1) | etap 8 |
| (v2) `IsCamping` (MaterialOrders) na funkcji `SleepKind` | dzis zgodne z bramka; zmiana poszerzylaby pole regresji o kontrakty karawan bez korzysci | tylko gdy test pokaze rozjazd |

---

## 8. Krytyka - co przyjeto/odrzucono (v2, 12.10.2026)

Dwie krytyki: **K1** (logika, decyzje Jeffa, logi Z33 - 8 uwag) i **K2** (kod, latki, UI, koszt - 15 uwag). Kazda sprawdzona w kodzie i logach; pelne dowody - `docs\etap7\KRYTYKA-E7.md`.
**23 przyjete (4 w zmienionej postaci), 0 odrzuconych.** Nic krytycznego (CTD, zepsuty zapis).

| Uwaga | Waga | Werdykt | Co zmienione w v2 |
|---|---|---|---|
| K1-01 budzenie to glownie karawany (BK `ReleaseCaravanFromHold`) | wazne | przyjete (N: 435/551 = 79% karawan) | rozdz. 0 pkt 3, rozdz. 1, "Najwazniejsze przyczyny", E7/O1 (przyczyna, liczniki lordowie/karawany, T1, T4 = "AI wstalo przed switem", ryzyko 5) |
| K1-02 + O1-magazyn rozkaz dluznika do zlego slownika; `_bedPos` bez `_camping` | wazne | przyjete czesciowo (`IsCamping` bez zmian) | R5, E7/O1 pkt 0 i 3 (`SleepKind`, magazyn wedlug snu, `_bedPos` z `_camping`), T6 |
| K1-03 + R1-RaidDead straznik R1 kasowal `RaidState` (jency E4/9, Inni, ksiega, W1) | wazne | przyjete | R15, E7/R1 pkt 1-2 (wspolny rdzen `Strike`, jeden `RaidState`, straznik na gorze `RaidPrefix`, `EndPrefix` bez `Settle`), T10-T11, W1 T1 |
| K1-04 + W1-spalona "burned" przy Looted wbrew ksiedze | wazne | przyjete | R10, R14, E7/W1 pkt 2-4 ("plundered" przy Looted, "burned" przy przejsciu w `CheckUp`), T5 |
| K1-05 sufit `MarchPace` po terenie i premii drogi | wazne | przyjete | rozdz. 0 pkt 2 i 11, rozdz. 1, E7/T (przyklad, probka z rozpiski gry, T1 niezalezny, T3 ponizej sufitu, T6 plon), E7/D2, pytanie 1 |
| K1-06 R8 sprzeczna, rozejmy PLAN 7.7 pominiete | drobne | przyjete w postaci jednej zasady kalendarza (20 -> ok. 87 dni postfiksem jak BK) | R8, E7/K pkt 3-4, klucz `DiplomacyYearLong`, "Pogodzone", rozdz. 4, 5.1, 5.3 |
| K1-07 lordowie w osadzie (`_townHold`) poza faza | drobne | przyjete | R5, E7/O1 pkt 1-2, T3 |
| K1-08 getter `ForeignHold` "przy okazji" | drobne | przyjete | E7/O1 pkt 1, 5.1, rozdz. 7 (8.17) |
| R1-kolejnosc priorytet Harmony R1 wzgledem E4/5 | wazne | przyjete | E7/R1 pkt 1-2 (Priority.First, decyzja na poczatku rabunku), 5.1 |
| R1-koniec skutki uboczne `Update` i koniec bez oryginalu | wazne | przyjete | E7/R1 pkt 7, R2 |
| R1-test "lup z niczego = 0" z konstrukcji | wazne | przyjete | E7/R1 pkt 8, T1-T2, rozdz. 4 |
| O2-fizyka namioty z fizyka, ognisko pod graczem | wazne | przyjete | R18, E7/O2 (bez fizyki, 3 m, `PrefabExists`), rozdz. 0 pkt 4, rozdz. 4 |
| O1-nawigacja `Nav`/port, nieodtwarzalne rozkazy | drobne | przyjete w postaci: pola `Nav`/`Port`/`Point`; ToNearestLand i GoToInteractablePoint bez bramki | E7/O1 pkt 3, rozdz. 7 |
| O1-faza prog 0.3 h za ciasny | drobne | przyjete (< 0.6 h) | E7/O1 pkt 1, "Najwazniejsze przyczyny" |
| O1-BK 13 latek BK, punkt (1) | drobne | przyjete; punkt zamkniety | E7/O1 "Okna", 5.1, 5.3 |
| R1-milicja falszywe "+20 milicji z niczego" | drobne | przyjete | E7/R1 pkt 3, 5.1, 5.2 |
| W1-zapis nazwa partii w zapisie | drobne | przyjete | R14, E7/W1 pkt 3, T6 |
| D0-dwa-DLL testowany inny DLL niz wgrywany | drobne | przyjete | E7/D0, rozdz. 4 Standard, rozdz. 0 pkt 11 |
| W4-warstwa wzor okna modalnego | drobne | przyjete | E7/W4 pkt 3 |
| D2-priorytet rowne Normal z TerrainEase | drobne | przyjete (LowerThanNormal) | E7/D2, 5.1 |

**Kolejnosc paczek po v2** (bez zmian w zaleznosciach): rownolegle teraz - E7/0, T, K, C, O2, L, B, W1, D0 (przeniesienie); O1 po E7/0 (wspolny `NightMarch`); W2 po L i W1; W3 po W2;
R1 po E5/A7 i W1 (i z ustalonym w v2 priorytetem oraz koncem rabunku); R2 i W4 po R1; W5 ostatnia; D1/D2 po tescie zbiorczym i odpowiedziach Jeffa.
