# RAPORT NOCNY 08/09.10.2026 (sesja 7016f733)

Zgoda Jeffa na te noc: "Tak, wgraj sam" - wgrywane TYLKO to, co przeszlo autotest bez bledow, zawsze z kopia poprzedniej wersji; zapisy Jeffa nietkniete.
Szczegoly krok po kroku: docs/PRZEKAZANIE-2026-10-08.md rozdz. 15-16; stan: docs/STAN-PRAC.md (sekcja "NOC 08/09.10"); audyt: docs/audyt-2026-10-09/.

## 1. Co jest W GRZE

| Wgranie | Godz. (zegar komp.) | Armoury | CrashScribe | Co doszlo |
|---|---|---|---|---|
| przed noca | - | c01a54ba | 11fa0214 | 160 + 161 |
| 1 | 19:07 | 28a7456e | aff275de | 170 BetterEconomy domkniete; T1 oboz 0:00-6:00; T2 kalendarz Innych; T3 metal napraw; T4 dezerterzy do puli; T5 rodzina splaca rate Banku; T6 miara marszu (log); T7 mlyny na brzegu + "held by" w dymku |
| 2 | 19:33 | 3bf72dfd | cfb33950 | T8 krainy (papirus i las wedlug klimatu; 23 wsie z uprawa niezgodna z klimatem; bawelna 14 wsi tylko w cieplych krainach); T2b Inni bez dosypki z niczego |
| 3 | 20:08 | 04d1cc99 | cfb33950 | 169 KSIEGA OBIEGU (sam log) |
| 4 | 21:50 | 0f8a80b0 | cfb33950 | 169b poprawki pomiaru; H3 PRZEGRANI UCHODZA (decyzja Jeffa 07.10) |

Kopie kazdej poprzedniej wersji: obok plikow (*.bak-2026-10-09-przed-nocN) i D:\Backup-Bannerlord\wgrane\2026-10-09-nocN-przed.
Armoury.json, CrashScribe.settings.xml i ustawienia BetterEconomy Jeffa bez zmian (nowe ustawienia dzialaja z wartosci domyslnych kodu).
Galezie (GitHub): paczki/noc-wgranie-1, paczki/noc-wgranie-2, noc/sklad2 (wgranie 3), noc/sklad4 (wgranie 4).

## 2. Co sie zmienilo w grze (slowami gracza)

- Obozy: wszyscy (lordowie, armie, karawany) stoja od polnocy do 6:00; wodz armii zawsze rozbija oboz (chyba ze wrog blisko albo poscig); namioty nie "jezdza".
- Nieumarli: nie zdobywaja zadnej warowni przed 3. rokiem (Piesc i Crastera od doby 728), Mur najwczesniej w 6. roku (doba 2184); nie dostaja trupow z niczego
  (bez +100 dla nowej bandy i +2 dziennie, bez ochotnikow z mapy) - rosna tylko z poleglych. Start kampanii bez zmian (616 trupow).
- BetterEconomy: opcje, ktore wrzucaly Twoje zloto w nicosc (wplaty do "skarbca", inwestycje, oboz, szkolenie, zbrojownia, dostep do targu), sa widoczne, ale
  szare z powodem po angielsku; "Lord wealth realism" stoi na OFF; bierne zrodla z niczego zatrzymane.
- Naprawy: kowal bierze tyle metalu, ile naprawde (nity, plytki), nie 14% nowej zbroi; robocizna bez zmian.
- Dezerterzy z nieoplaconych armii ida do lasu (pula wyrzutkow), nie znikaja. Rodzina pomaga glowie rodu splacic Bank, zanim wezmie nowa pozyczke.
- Krainy: bawelna, winnice, daktyle, papirus i drewno lasu tylko tam, gdzie pozwala klimat; suma swiata bez zmian.
- Wioski: mlyny stoja na brzegu, dymek mowi "A village of the X lands, held by House Y of Z" (na zywo).
- Bitwy AI w polu: przegrani gina ok. 18-24% (gra dawala ok. 50%), reszta uchodzi do domu / wsi / lasu; zwyciezcy gina ok. 2% (gra ok. 7%). Bitwy gracza bez zmian.
- Log: ksiega obiegu pieniadza (skad zloto przychodzi i dokad idzie - niewyjasnione straty swiata spadly z ok. -215 do ok. -27 tys. dziennie), miara marszu, zapasy warowni.

## 3. Testy (autotest; zapis Jeffa nigdy nie ruszany)

Kazde wgranie: nowa kampania 40 dob + wczytanie zapisu z doby 362 (8-10 dob) dokladnie tej binarki. Wszystkie: 0 bledow Armoury, 0 potkniec, 6-8 znanych bledow startowych
CrashScribe (cudze mody). Tempo: 13.0-13.3 s/dobe (nowa kampania), 22.8-25.7 s/dobe (zapis z roku gry). Do tego test 120 dob wgrania 3: 0 bledow, ok. 18 s/dobe bez narastania.

## 4. NIE wgrane - i dlaczego

1. **171 zbrojenie zalog + 172 strzaly** - dzialaja (rekrut bez kompletu z niczego, zamki kupuja w miastach, strzelarze robia strzaly z drewna i rudy), ale nie przeszly
   progow: w nowej kampanii rynek zbroi pustoszeje (zbroja korpusu do zera ok. doby 48), partie AI maja 50% zbroi i 23% tarcz, karawany wykupuja 81-95% strzal
   (BannerKings uczynil strzaly towarem handlowym), polowa miast bez strzal, ruda waskim gardlem. Do Twojej decyzji (pkt 6).
2. **Skrypt BetterEconomy (13 kluczy, wariant b)** - zablokowany przez zabezpieczenia Claude Code (zmiana pliku w folderze gry). Do uruchomienia przez Ciebie (pkt 6).
3. **Proba zimy i proba drog na mapie** - gotowe, zablokowane przez zabezpieczenia (zmiana ustawien probnej kopii / plik danych w folderze gry na czas testu).

## 5. Audyt swiata - najwazniejsze (docs/audyt-2026-10-09/00-AUDYT-SWIATA-2026-10-09.md + 10 raportow)

- Ceny, place i zold sa prawie jak w Anglii ok. 1300 (1 moneta = 1 pens) - to sie broni.
- Wojsko idzie 5-6 razy za szybko (armia ok. 117 km/dobe; prawdziwa 15-25 km). Same godziny obozu tego nie zmienia - to "tempo swiata" (pkt 6).
- Mieszczanie nie maja wlasnych pieniedzy (kasa miasta = jeden worek), chlopom pan zabiera ok. 85% gotowki, a jedzenie mieszczan placi gra z niczego -> projekt
  KIESA LUDU (docs/PROJEKT-KIESA-LUDU-2026-10-09.md, po 2 krytykach z symulacja 4 lat; paczka 173, z grupa C i nowa kampania).
- Wies nie produkuje wedlug liczby ludzi (3 progi wielkosci); wioski poboczne sa dzis tylko obrazem - plon od rak i spalona wioska = mniej plonu czeka na ksiege ludzi (108-113).
- Naprawy zjadaly za duzo metalu (poprawione, T3). Strzaly robily warsztaty z niczego (172 to zamyka).
- Okupy: miedzy lordami AI smiesznie niskie (glowa rodu ok. 4-6 tys. = 10-15 dni dochodu), Twoj okup znika w nicosc (do poprawy).
- Tabela ludnosci rosnie o 72% rocznie (blad - do naprawy z ksiega ludzi).
- **Bankructwa (test 120 dob):** fala jak w starym tescie - 26 rodow do doby 120, Bank z 5 do 1.75 mln, skarbce Polnocy i KL puste od ok. doby 98. Glowne przyczyny:
  (a) rody utrzymuja wiecej wojska, niz zarabiaja (naprawa: paczki 165/166/168 - budzet rodu, korona z biezacych wplywow, Bank na zdolnosc splaty);
  (b) **reparacje wojenne Diplomacy**: 11-12 mln w 120 dob, do 1.75 mln naraz; 93% z "pokojow" trwajacych najwyzej dobe (Diplomacy wymusza pokoj, ROT tej samej doby
  wznawia wojne fabularna); przegrany splaca w 3 doby z kies lordow w nicosc, zwyciezca dostaje z niczego (szczegoly: SCRATCH noc\trybut.md, w repo: ten raport).

## 6. Do Twojej decyzji (tylko to, co zmienia gre)

1. Uruchomic skrypt BetterEconomy (gra zamknieta): `powershell -NoProfile -ExecutionPolicy Bypass -File tools\bee\zamknij-ujscia-bee.ps1 -ListaZFundamentu`
   (kopia ustawien: D:\Backup-Bannerlord\bee-2026-10-08\). Zamyka m.in. eskorte karawan BEE placona w nicosc (ok. 665 zl przy kazdym wjezdzie do miasta).
2. MCM Diplomacy: "Scaling War Reparations Gold Cost Multiplier" 50 -> 10 (reparacje 5x mniejsze: lord placi ok. 3 dni dochodu zamiast 17). Rekomendacja: tak.
3. 171/172: czy karawany moga handlowac strzalami (rada: nie); zgoda na 171 przy pustoszejacym rynku nowej kampanii (alternatywa: najpierw skala produkcji zbroi).
4. Tempo swiata (realne odleglosci): "jak w ksiazkach" (armia Winterfell-KP ok. 77 dni) czy "twarda historia" (129) - rekomendacja: ksiazkowy, po naprawach ekonomii.
5. Okupy wedlug majatku (jedna regula dla Ciebie i AI) i "prawo trzecich" (korona bierze 1/3 okupow i lupu wasali) - rekomendacja: tak.
6. Jency w Westeros: wracaja do domu czy ida na Mur (dzis BannerKings robi z nich niewolnikow wbrew kanonowi).
7. KIESA LUDU - 3 pytania w projekcie (glod ma skutki; dziesiecina dla septow; o ok. 5% mniejsze wojsko AI).
8. Nieumarli: Mur w 6. roku (doba 2184) czy pozniej / tylko na Twoje slowo.
9. Pokoj z biedy (krolestwo z pustym skarbcem chetniej zawiera rozejm) - rekomendacja: tak.
10. Proba zimy i proba drog (zdjecia) - zgoda na uruchomienie (wymaga pliku danych w folderze gry na czas testu).

## 7. Co dalej (kolejnosc)

165 KORONA (zwrot zoldu z biezacych wplywow, reparacje korona->korona) -> 166 BUDZET RODU (wojsko wedlug dochodu, zwalnianie nadwyzki) -> 168 DLUG (Bank na zdolnosc
splaty, zajecie zamiast bankructwa) - to zatrzymuje fale bankructw. Rownolegle: 108 ksiega ludzi (plon od rak, wioski poboczne w rachunku, tabela ludnosci), 164 szczelnosc.
Optymalizacja gry na koniec (Twoja decyzja 08.10).
