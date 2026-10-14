# -*- coding: utf-8 -*-
# Sklada ROZPOZNANIE-CZAS.md z szablonu i tabel (robocze/czas). Tylko zapis do docs/oblezenia.
base = r'C:/Users/GAME/Bannerlor2-Realm-of-Thrones/docs/oblezenia/robocze/czas/'
out = r'C:/Users/GAME/Bannerlor2-Realm-of-Thrones/docs/oblezenia/ROZPOZNANIE-CZAS.md'
t = open(base + 'szablon.md', encoding='utf-8').read()
tab = open(base + 'tabele.md', encoding='utf-8').read()
parts = ['#### ' + p for p in tab.split('#### ') if p.strip()]
fin30 = [p for p in parts if p.startswith('#### fin30 - wynik')][0].strip()
zdob = [p for p in parts if p.startswith('#### fin30 - wszystkie')][0].strip()
fin21 = [p for p in parts if p.startswith('#### fin21 - wynik')][0].strip()
aneks = open(base + 'aneks_fin30.md', encoding='utf-8').read().strip()

dzw = """| # | Dzwignia | Gdzie | Co jest dzis | Co by dala [S] | Uwagi |
|---|---|---|---|---|---|
| 1 | Czlon czasu w decyzji o szturmie | `BesiegerCamp.cs:408-409` (postfiks na tej metodzie juz mamy - `SiegeRansom.AssaultPost`) | po 96 h -0.02/h; po 5.7 doby mury nic nie znacza, potem pomagaja oblegajacemu [K] | bez ujemnego czlonu 22 ze 113 decyzji (19%) by nie zapadlo [P]; szturmy przy "murach" >= 1.5 (56) dalej ok. 8. doby | samo to nie robi miesiecy - silne armie i tak szturmuja; slabsze zaczna czekac (pkt 5) |
| 2 | Prog szturmu wobec murow | ten sam wzor (`s > 1`, potem 10% x s na mysl) | wystarcza s > 1 | np. s > 2-3 albo wymog wyrwy: dzis "mury" mediana 1.5 [P] - wiekszosc szturmow by poczekala | AI czeka -> glod, choroba, rozpad armii, odsiecz |
| 3 | Sila murow w samej bitwie | `DefaultCombatSimulationModel.cs:241-281` (0.7 x przewaga, prog 100/30 obroncow) | obronca ok. 2.5 raza wiecej ciosow, premia znika ponizej 100/30 [K] | krwawsze szturmy, odparcia mozliwe przy 2.8-3.5:1 | dotyczy tez autobitwy gracza |
| 4 | Glod jako droga zdobycia | zapasy `Town.FoodStocks` | 225-274 dob zapasu (mediana), 0 dob glodu [P] | zadziala dopiero, gdy AI bedzie czekac (pkt 1-2); dzis 7-9 miesiecy - historycznie rozsadne [H] | zalezy od liczby ludzi w murach (uchodzcy `FamineSiegeShelter`) |
| 5 | Wytrzymalosc armii w dlugim oblezeniu | spojnosc (BK `CohesionBoost` 0.5 [U], SCA -3.5 na wrogiej ziemi [K `StrategicAiTuning`]), zold, jedzenie obozu, CampFever | najdluzsze oblezenie 17 dob (fin30) / 24 (fin21) [P] | bez tego dluzsze oblezenia zamienia sie w odstapienia | CampFever po 60 dobach: 0.6% x (1 + 51 x 0.15) = 5.2%/dobe x tlok [S] - zniszczylby oboz |
| 6 | Tempo machin | `SiegePacePercent` 50 [U], BK `LongerSieges` 0.5 [U], RB 1.0 [U], RBM przygotowania 24 osobodni [K] | razem x0.25 (x0.19 bez narzedzi) | wolniej = decyzja proporcjonalnie pozniej (dzis 4-6 z 8.4 doby) | `AUDYT-CZAS-MAPA.md` 3.1: nie zmieniac kilku naraz |
| 7 | Mury i wyrwa | naprawa 0 w oblezeniu, x0.25 dopiero gdy wszystkie odcinki = 0 [K] | szturm nie potrzebuje wyrwy | szturm (albo premia murow) zalezny od zniszczenia murow; trebusz RBM 6000/dobe wobec 50 000 murow 2 = tygodnie [S] | ostrzal zalezy od trafien - nie mierzone |
| 8 | Prog rozpoczecia oblezenia | `DefaultTargetScoreCalculatingModel.cs:232-243` (2 x obrona z murami) | AI bierze cele, gdzie ma ok. 3x przewagi | to temat "sily" (garnizony, np. KP 541 + 340 wobec 4484) | wspolne z rozpoznaniem sil |
| 9 | Poddanie na warunkach | brak w grze; jest okup O1 (26 oblezen, ok. 9. doby) [P] | twierdza nie poddaje sie bez szturmu | historycznie glowna droga [H]: poddanie, gdy brak odsieczy przez N dob albo zapas < X | nowa mechanika - tylko kierunek |"""

niewiem = """- **Czemu 18 (fin30) / 29 (fin21) szturmow skonczylo sie "bez rozstrzygniecia".** Wiem tylko: oblezenie konczylo sie razem ze szturmem, w 2/18 (9/29) strona oblegajacych byla pusta,
  9 z 16 szturmow przy "murach" <= 1 skonczylo sie tak [P]; kod dopuszcza koniec bez zwyciezcy przy braku wodza / koncu wojny [K]. Konkretnej przyczyny nie zmierzono.
- **Czemu 47 oblezen (fin30) odstapiono bez bitwy (mediana 4-5 dob).** Brak linii o odejsciu obozu albo rozwiazaniu armii; nie zakladam (spojnosc, zmiana celu AI, zblizajaca sie odsiecz?).
- **Dokladny czas gotowosci przygotowan i machin.** Gra tego nie loguje (linia `WorldPace: oblezenie` tylko dla gracza); czas w rozdz. 4.1 to szacunek z kodu. Nie wiem, czy partie AI
  maja narzedzia (Tools - x0.75 RB) ani jakie maja Engineering i perki.
- **Czy RBM naprawde nadpisuje przygotowania 48 -> 24 osobodni.** Z kolejnosci modulow i starego audytu (`AUDYT-CZAS-MAPA.md`, "24 osobodni (LOG)"), nie z logu tego biegu.
- **Pelne straty szturmow.** B175 bywa niepelne przy szturmach wielodniowych (KP: 92 obroncow w B175 wobec 541 + 340 w CSV) - wymiana 1.5 oblegajacego na obronce jest orientacyjna.
- **Czestotliwosc mysli AI w oblezeniu.** Sprawdzona tylko gra (0.25 h x 0.6-0.7 x 2); nie sprawdzalem, czy inny mod ja zmienia (w tym biegu i tak szturm zaczynal sie przy pierwszym "true").
- **Trafnosc ostrzalu i realne niszczenie murow** - nie logowane.
- **Historia i lore** - z wiedzy ogolnej, bez sprawdzenia zrodel w tej sesji (daty i dlugosci przyblizone).
- **Skutki dluzszych oblezen** dla CampFever, spojnosci armii, zoldu i jedzenia obozu - nie liczone (poza zakresem "czasu"); potrzebne przed jakakolwiek zmiana pkt 1-2 rozdz. 9."""

t = t.replace('{TAB_FIN30}', fin30).replace('{TAB_FIN21}', fin21).replace('{TAB_ZDOBYCIA}', zdob).replace('{ANEKS}', aneks)
t = t.replace('{DZWIGNIE}', dzw).replace('{NIEWIEM}', niewiem)
assert '{' not in t.replace('{=', '') or True
open(out, 'w', encoding='utf-8').write(t)
print(len(t), 'znakow ->', out)
left = [x for x in ('{TAB_FIN30}', '{TAB_FIN21}', '{TAB_ZDOBYCIA}', '{ANEKS}', '{DZWIGNIE}', '{NIEWIEM}') if x in t]
print('nie wypelnione:', left)
