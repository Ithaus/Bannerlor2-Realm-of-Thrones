# MYTO NA BLIZNAKACH (B3) - liczby, stawki, regula, kod

Data: 14.10.2026. Galaz `w-toku/strzaly-myto` (drzewo `zb\tb`, od `w-toku/zbior-e8` 79a8d89). Klucz MCM: `TwinsToll` (domyslnie wlaczony, wymaga `TwinsCrossingCount`).
Zrodla: PLAN 7.12; Jeff 12.10 "chce, ale policz" (projekt etapu 7 rozdz. 6 pyt. 4); audyt 09.10 `10-pomysly.md` Z3 i ESENCJA:215 (stawki 2 / 1 zl);
licznik E7/B (`TwinsCrossing.cs`, linia "Przeprawa Bliznaki (E7/B)") w trzech biegach: fin27 i fin29 (120 dob), dwuletni W50 fin21 (728 dob).

---

## 0. Dla Jeffa - w skrocie

**Ile kosztuje przejscie przez most Freyow:**

| Kto | Placi? | Ile (przyklad) |
|---|---|---|
| Karawana kupiecka (ok. 20-27 ludzi, 23-27 mulow i koni) | TAK | **ok. 60-80 zl za przejscie** (ok. 1% jej kiesy) |
| Woz wsi z obcej wsi | TAK, ale najwyzej dniowka ludzi | ok. 1 zl od czlowieka (kilkanascie-kilkadziesiat zl) |
| Woz wsi Freyow, karawana Freyow | NIE | swoich pan nie myci |
| Lord z innego krolestwa (np. 150 ludzi, 50 koni) | TAK | ok. 350 zl |
| Lord z krolestwa Freyow (Dorzecze) | NIE | sluzy temu samemu krolowi |
| **Ty** (np. 100 ludzi + 40 koni) | TAK, chyba ze jestes w krolestwie pana Bliznakow albo sam je masz | **ok. 240 zl** (jency tez sie licza, 2 zl od glowy) |
| Bandy, Inni | NIE | nie staja przy bramce |
| Ktokolwiek w wojnie z Freyami (lord, karawana, Ty) | NIE | wrog nie staje przy bramce - przechodzi sila albo oblega; Freyowie nie biora pieniedzy od wroga (poprawka recenzji) |

**Stawka:** 2 zl od czlowieka (z jencami) i 1 zl od zwierzecia (kon, mul, wielblad, bydlo). **Nigdy wiecej niz jeden dzien zoldu tej partii** - tyle kosztowalaby
droga dookola, bo Bliznaki to jedyny most przez Zielone Widly na wiele mil. Dlatego nikt nie zawraca: zaplacic jest zawsze taniej niz isc naokolo.
Kto nie ma pieniedzy - placi, ile ma (bez dlugu).

**Dokad ida pieniadze:** do kiesy pana Bliznakow (dzis Walder Frey). Nic nie powstaje z niczego - ile zeszlo z kies kupcow i lordow, tyle przybylo Freyowi
(linia logu to sprawdza codziennie co do zlotowki).

**Ile zarabiaja Freyowie:** przez most idzie 4-5.5 partii na dobe (w tym 2.3-3.7 karawany). Myto da im **ok. 150-350 zl dziennie, czyli ok. 55-130 tys. zl
na rok** (364 doby). To okolo **3-14% ich stalego dochodu w pierwszym roku** (D rodu Frey 2.4-12 tys. zl/d; w biegu 120 dob ok. jedna dziesiata, w biegu dwuletnim 3-7%) - bogaty, "mytny" rod jak w ksiazkach,
ale nie najbogatszy w Westeros.

**Co zobaczysz w grze:** przy przejsciu komunikat, np. *"Toll at the Twins: 240 denars to Walder Frey for 100 men and 40 beasts."* Jesli kiedys zdobedziesz
Bliznaki - raz na dobe *"The toll at the Twins brought you N denars today."* Wylacznik w MCM: "Twins Toll".

---

## 1. Liczby z licznika (myto "gdyby bylo" przy 2 zl / 1 zl, rody krolestwa pana bez myta)

| Bieg | Dob | Przejsc / d | Karawany / d | Wozy wsi / d | Lordowie / d (krolestwo pana / obce) | Inne / d | Myto gdyby bylo, zl / d (karawany / wozy / obce rody / inne) |
|---|---|---|---|---|---|---|---|
| fin27 (W53 + C3-C5) | 120 | 4.22 | 2.26 | 1.04 | 0.60 / 0.03 | 0.30 | 260 (137 / 76 / 4 / 43) |
| fin29 (zbior-e8 + towary2 + ...) | 120 | 4.98 | 3.12 | 1.18 | 0.46 / 0.12 | 0.06 | 323 (203 / 80 / 32 / 8) |
| dwuletni W50 fin21 | 728 | 5.45 | 3.70 | 0.86 | 0.79 / 0.05 | 0.03 | 397 (301 / 59 / 32 / 4) |

- Karawana na przejsciu: fin27 19 ludzi, 23 zwierzeta, kiesa 7 900 zl -> 61 zl (0.8% kiesy); dwuletni 27 ludzi, 27 zwierzat, kiesa 5 900 zl -> 81 zl (1.4%).
- Pan Bliznakow przez caly czas: Frey (Riverlands) - w kazdym biegu kazda doba.
- Bandy: 0-0.04 przejscia na dobe. Gracz: autotest nie przechodzi mostem.
- Dawne "418 przejsc w 120 dob, 27 316 zl" (WGRANIE 34) to ten sam rzad wielkosci.

**"2.8% omija most"** - to NIE bylo omijanie. Pomiar sprzed poprawki F8 (strefa probek byla wokol bramy, nie mostu); po F8 licznik "zmiana strony poza strefa"
pokazuje 0.6-0.7% (fin29 4 z 595, dwuletni 24 z 3 959) i to sa szybkie partie, ktore przeszly MOSTEM miedzy dwiema godzinnymi probkami (ROZPOZNANIE-A17 poz. 5).
Innej przeprawy w promieniu 16 jedn. od mostu nie ma (F8-1: przejsc poza mostem 0-7 w 728 dobach, najdalej 7.7 jedn.). Od B3 te zgubione przejscia tez placa myto.

## 2. Historia i lore - skad stawka

- **Pontage** (myto mostowe w Anglii): ok. 370 krolewskich nadan 1228-1440; przyklad Hulbrigge 1397 - pol pensa od czlowieka z koniem, cwierc pensa od innego
  zwierzecia ([Wikipedia - Pontage](https://en.wikipedia.org/wiki/Pontage)). Myta brano zwykle ok. 0.1-1% wartosci ladunku (`audyt-2026-10-09/09-historyk-liczby.md`).
- **Waluta moda:** 1 zl = 1 pens (d) (`CENY-HISTORYCZNE.md`); zolnierz AI kosztuje ok. 4.5 zl dziennie (budzet-rodow.csv: zold partii Freyow / ludzie).
- **Freyowie:** od ok. 600 lat trzymaja jedyna przeprawe przez Zielone Widly na wiele mil i wzbogacili sie na mycie od kazdego, kto przechodzi - stad przydomek
  i majatek rodu ([AWOIAF - The Twins](https://awoiaf.westeros.org/index.php/The_Twins), [AWOIAF - House Frey](https://awoiaf.westeros.org/index.php/House_Frey)).
  Kanon nie podaje stawek liczbowych.
- **Decyzja (Claude):** 2 zl od czlowieka i 1 zl od zwierzecia = ok. 4-6 x Hulbrigge ("ciezkie myto" Freyow), a dla karawany ok. 1% kiesy - gorna granica
  myta historycznego od ladunku. Wyzsza stawka uderzylaby w kupcow ponad dzien drogi okreznej (patrz sufit), nizsza dalaby Freyom grosze (kilka-kilkadziesiat zl/d).

## 3. Regula (jedna dla gracza i AI)

1. Kazde przejscie mostem (licznik E7/B - zmiana strony rzeki w strefie 8 jedn. od mostu, takze przejscie miedzy probkami) = jedno myto.
2. **Kto zwolniony:** rody krolestwa pana Bliznakow (powinnosc lenna - jak w porownaniu E7/B od 12.10; takze sam pan i gracz - wasal tego krolestwa);
   karawany rodu pana i wozy jego wlasnych wsi (swoich nie myci); bandy i Inni (nie staja przy bramce); **partie w wojnie z panem** (poprawka recenzji:
   CrossingLaw zawraca tylko czesc wrogow - w biegu dwuletnim 23 wrogich lordow przeszlo most; bez zwolnienia wrog, takze oblegajacy Bliznaki, placilby myto
   panu, z ktorym walczy - przelew do wroga).
3. **Ile:** 2 zl x ludzie (z jencami) + 1 zl x zwierzeta (konie pod jezdzcami, luzaki, juczne, bydlo), **najwyzej dzien kosztu partii** = jej zold dnia
   (model gry / BK), a bez zoldu 1 zl od czlowieka (dniowka wyrobnika).
4. **Decyzja AI z biezacego zysku (droga okrezna vs myto):** droga dookola to co najmniej dzien marszu wiecej, czyli co najmniej dzien kosztu partii.
   Pan bierze najwyzej tyle - wiecej, a kupiec poszedlby dookola - wiec zaplacic jest zawsze taniej i partia placi. Nikt nie jest zawracany: zadnej zmiany
   sciezek AI (zero ryzyka zakleszczen jak przy CrossingLaw), ruch przez most taki sam jak bez myta.
5. **Z czego -> do kogo:** kiesa partii (lord i gracz - kiesa wodza; karawana i woz - kiesa kupiecka) -> kiesa glowy rodu pana Bliznakow
   (`GiveGoldAction` - przelew; gdy pan bez zywej glowy - kasa miasta). Pusta kiesa = placi, ile ma.
6. **Gracz:** komunikat po angielsku przy kazdym przejsciu (zaplacone albo "lets you cross free"); gracz panem Bliznakow - komunikat dobowy z mytem.

## 4. Skutek

| Pozycja | Szacunek (zl/d) | Skad |
|---|---|---|
| karawany | 137-301 | prawie wszystkie placa (Freyowie w dwuletnim biegu nie maja karawan: zold karawan 0) i prawie zadna nie trafia w sufit (zold karawany ok. 90-120 zl/d > 61-81 zl myta) |
| wozy wsi | 0-30 | wozy wsi Freyow zwolnione; obce trafiaja w sufit ok. 1 zl od czlowieka |
| obce rody | 4-32 | rzadkie przejscia, zold dnia lorda wyzszy niz myto |
| inne | 0-40 | zalezy od kiesy (patrole czesto bez pieniedzy) |
| **razem** | **ok. 150-350** | **55-130 tys. zl na rok (364 doby)** |

- Rod Frey: D 2 428 zl/d (fin25 d120), w biegu dwuletnim 6.0 tys. (d92) - 11.9 tys. (d365) - 33.5 tys. (d728). Myto = ok. 3-14% D w pierwszym roku.
- Kupcy: ok. 1% kiesy na przejscie. Osobno BK bierze od karawany oplate przy WEJSCIU do miasta (`BKPartyBehavior.AddCaravanFees` -> `EconomicData.CaravanFee`,
  do podatku handlowego miasta) - to clo miejskie, inne zdarzenie; karawana, ktora przechodzi mostem i wchodzi do Bliznakow, placi oba, jak w historii (myto i clo).
- Ruch: bez zmian (nikt nie zawraca). Swiat: zloto przechodzi z kies kupcow i obcych lordow do Freyow - zadnego nowego zrodla ani ujscia.
- Dokladne liczby po tescie 120 dob: `python -I tools/sprawdz_logi.py --grupa tb <Armoury-*.log>` (TB-B3-3 podaje tez % D Freyow z budzet-rodow.csv).

## 5. Kod

- `Armoury/src/TwinsCrossing.cs`: `Toll` (wolane z `Cross` i z licznika "zmiana strony poza strefa"), `TollLine` - linia doby
  "Myto Bliznakow (B3): dzien N | [WL] ... | zaplacilo n partii: X zl (karawany a/x, wozy wsi, obce rody, gracz, inne) | zwolnieni ... (takze "w wojnie z panem") | uciete do dnia kosztu ...
  | nie stac na cale ... | kontrola obiegu: z kies platnikow X zl, do kiesy pana Y zl, roznica 0 | pan Bliznakow ... | myto od poczatku sesji".
- `Armoury/src/Settings.cs`: `TwinsToll` = true (nowy klucz - w Armoury.json Jeffa go nie ma, zadziala domyslna).
- Progi: `tools/sprawdz_logi.py --grupa tb`, wiersze TB-B3-0..6 (linia z [WL] w kazdej dobie licznika, roznica obiegu 0, zaplacone > 0 i <= 1.05 x "gdyby bylo",
  kto placi i % D Freyow INFO, przejscia INFO, potkniecia 0, komunikaty gracza - test reczny).

## 6. Ryzyka

- Probka godzinna: komunikat gracza moze przyjsc do godziny gry po przejsciu (myto pobierane, gdy licznik zobaczy zmiane strony).
- Gracz z jencami placi takze za jencow (2 zl od glowy) - zgodnie z "od czlowieka"; po zdobyciu Bliznakow myto plynie do gracza.
- Zold dnia (sufit) liczy model gry/BK (`MobileParty.TotalWage`) - jesli BK zmieni zold, zmieni sie tylko sufit, nie stawka.
- Fosa Cailin i Krwawa Brama - bez myta (brak lore o mycie; licznik jest tylko przy Bliznakach).
- Droga okrezna = "co najmniej dzien kosztu" to zalozenie projektu, nie pomiar: F8-1 mowi tylko, ze w promieniu 16 jedn. od mostu nikt nie przechodzi rzeki
  inaczej (0-7 przejsc w 728 dobach); odleglosci do nastepnej przeprawy na mapie ROT nie mierzylismy. Sufit sprawia, ze myto nigdy nie przekracza dnia kosztu
  partii, wiec AI nie ma powodu zawracac - sciezek AI nie ruszamy.
