# PRZEKAZANIE 03.11.2026 ok. 22:30 - "zniknela zywnosc z gry, ja tez nie mam zywnosci" (Jeff, po WGRANIU 161)

Dla drugiego konta Claude. Jeff przenosi sprawe tutaj. Przeczytaj najpierw CLAUDE.md, docs/STAN-PRAC.md (sekcja 03.11 WGRANIE 161)
i gorne wpisy CHANGELOG.md na galezi `noc/323x-integracja` (tam sa wpisy 322a-f i 323a-d; galaz glowna ma tylko docs).

## Co jest w grze
- WGRANIE 161 (03.11 ok. 16:20): Armoury.dll da5a31022ce86f7f87403e031fe64198 (galaz noc/323x-integracja, wypchnieta).
  CrashScribe 55ccc004, ForgeView 8441ab0e. Kopia 160: `Modules/Armoury/bin/Win64_Shipping_Client/Armoury.dll.bak-2026-11-03-przed-wgranie161`
  i `D:/Backup-Bannerlord/wgrane/2026-11-03-wgranie161-przed` (Armoury ca7f92ff = WGRANIE 160).
- 160 (03.11 ok. 03:15) = audyt ekonomii 322a-f, w tym 322c (zboze zima ze spichlerzy wsi na polke miasta), 322e (raty rachunkow
  zywnosci), 322f (chleb dla glodnych z zapasu wojny). 161 = 323a-d (majatek BK, odkup warsztatu, prace okregu - zjedzone, wioska poboczna).
- Zapis Jeffa: Ironman0gXONzURi8yO.sav. Sesja, w ktorej zglosil problem: log `Modules/Armoury/Armoury-2026-11-03_22-00-55.log`
  (dni 109251-109253, zima, dzien roku 54; log urywa sie 22:10:38).

## Co juz zmierzone (dowod z logow, nie hipoteza)
Linia "Glod miast (KS)" - brak jedzenia na rynkach miast (srednia 3 dob) i glodne miasta:

| log | dzien | brak na rynku (mediana / q90 / max) | glodne miasta KS |
|---|---|---|---|
| Armoury-2026-11-02_20-57-50.log (przed 160) | 109204 | 61.1% / 82.7% / Qarth 94.1% | 44 z 45 |
| Armoury-2026-11-03_00-19-16.log (przed 160) | 109250 | 82.1% / 92.9% / Seagard 95.3% | 44 z 45 |
| Armoury-2026-11-03_22-00-55.log (po 161) | 109253 | 83.2% / 97.3% / Stoney Sept 100% | 44 z 45 |

Wniosek: puste rynki miast NIE zaczely sie od 161 ani od 160 - brak rosnie od co najmniej doby 109204 (61% -> 82%), czyli przez cala
zime. 161 nie zmienil obrazu na poziomie swiata (82.1% -> 83.2% w 3 doby). Ale Jeff widzi to teraz wyraznie (Stoney Sept, gdzie stoi
i kuje, ma 100% braku - tam nie kupi jedzenia).

Jednoczesnie "Spichlerze (E4/6)": spichlerze wsi swiata 26 150 mln racji = 628 dni potrzeby, plon zimy 84.6% potrzeby.
Czyli jedzenie JEST - w spichlerzach wsi - tylko nie dociera na polki miast. 322c (zboze ze spichlerza wsi na targ zima) dalo
zaledwie 1413 szt. zboza na 3 doby dla calego swiata (linia "Towary: dzien 109252 - grain").

Inne linie z 22-00-55: "Jedzenie z targu (E5/0)" zamki: polka < 10 dni zuzycia 64 z 130 (mediana 3.0 dnia);
"HungerLaw: glodne partie AI dokupily juz 100 szt. jedzenia DROZSZEGO niz sufit 120"; "Pan miasta placi za dowoz (KS)": potrzeba 1 056 217 zl,
zaplacone przez panow 554 zl, "bez rachunkow 40". "Rachunki zywnosci - dluznik (322e)": miasta z dlugiem za jedzenie po 70-330 tys. (Qarth 330630,
Braavos 245413, Qarkash 203869) - kasy prawie puste, wiec kupcy KS nie dowoza.

## Czego NIE sprawdzilem (zadanie dla ciebie)
1. **Jedzenie druzyny gracza** ("ja tez nie mam zywnosci"). W logu 22-00-55 nie ma linii o ubytku jedzenia z juk gracza. Sprawdzic:
   czy Jeff mial jedzenie po wczytaniu (CrashScribe session-*.log z 03.11 22:00, ksiega towarow GoodsLedger "jedzenie partii"),
   czy cos zdejmuje jedzenie z ItemRoster gracza (musztra/zapas 316a, Rations, HungerLaw, KS, 322f, 323c PayEaten - ten nie rusza towarow,
   tylko zloto kiesy wsi). Najprosciej: zapytac Jeffa, ile mial jedzenia przed i po, i czy po prostu zjadl zapas, a miasto (Stoney Sept) nie ma czego sprzedac.
2. **Czemu jedzenie nie plynie ze spichlerzy wsi do miast** (628 dni zapasu vs 83% braku na rynkach). Podejrzani: KS-H "bez rachunkow 40"
   (kupcy nie dowoza do miast bez kasy), dlugi rachunkow zywnosci (322e raty sa male: 1.3 tys./dobe przy dlugu 130-330 tys.), 322c za slabe
   (1413 szt./3 doby), wozy wsi / KsDeliver. To byl stan przed 160 - ale 322c/322e/322f mialy go lagodzic i widac, ze nie lagodza.
3. Czy 161 ma z tym cokolwiek wspolnego: 323c placi z kiesy wsi (zloto), a kiesa wsi kupuje koszyk ZC4 (jedzenie z miasta). Jesli 323c wydrenuje
   kiesy wsi, wsie kupia mniej - ale to wplywa na wsie, nie na polki miast. Sprawdz w logu "zjedzone w okregu (323c" (wyplacone, brak w wolnej kiesie)
   i ZC4. Jesli chcesz wykluczyc 161 szybko: klucz MCM "Works Pay District Eats" = NIE (323c) i "Side Village Income Real Sales" = NIE (323d).
4. Cofniecie: jesli Jeff chce grac od razu - przywrocic 160 z kopii .bak (gra zamknieta), ale wedlug tabeli wyzej to NIE naprawi pustych rynkow.

## Narzedzia
- `python -I tools/sprawdz_logi.py --grupa <g> <Armoury-*.log>` z galezi noc/323x-integracja (grupy: eat323c, wies323d, majatek323a,
  odsprzedaz323b, npo, chleb322f, zboze322c?, raty322e - nazwy grup w naglowkach paczek pliku).
- Autotest: `C:\Users\GAME\bannerlord-autotest\m113\testy-noc-283.ps1` (pamiec: autotest-bitwy-armii, autotest-poza-temp). Baza DLL test\prod161.
- Uwaga z tej sesji: w Git Bash python nie otwiera sciezek `/c/...` - podawaj `C:/...`; przy wypisywaniu UTF-16 ustaw PYTHONIOENCODING=utf-8.
