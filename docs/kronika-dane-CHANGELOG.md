# CHANGELOG galezi w-toku/kronika-dane (Kronika Westeros - warstwa danych, bez C#)

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
