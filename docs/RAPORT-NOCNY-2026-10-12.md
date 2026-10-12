# Raport nocny 11/12.10.2026 (sesja ae222cb7)

Jeff, w skrócie: **etap 4 i etap 5 są w grze w całości, etap 6 częściowo. Wszystko po teście w grze, z kopiami.**

## 1. Co jest w grze (WGRANIE 32, 12.10 ok. 06:30 UTC)

W grze: Armoury `29fdca1e`, CrashScribe `842f64a4`, RealisticCaptivity `943ea0d3`, GrandTourney bez zmian.
Kopie poprzednich wersji: `*.dll.bak-2026-10-12-przed-noc32` obok plików i `D:\Backup-Bannerlord\wgrane\2026-10-12-noc32-przed`.

**Etap 4 - ludzie, zima, głód (cały):**
- Każdy żołnierz jest człowiekiem z konkretnej wsi albo miasta. Polegli ubywają na stałe, rozbici i zwolnieni wracają do domu.
- Pobór z miasta według jego udziału w ludności krainy, reszta ze wsi (Twoja decyzja).
- Spichlerze okręgów i głód: zimą żadna warownia AI nie głoduje, zapas żywności wystarcza na ponad 5 lat.
- Załogi, milicje, karawany i nowe drużyny też są z ludzi. Werbunek do załóg: 2 ludzi dziennie, w granicy żołdu.
- Nocna Straż bierze ludzi z całego Westeros i nikogo nie zwalnia. Niewolni tylko w krajach, gdzie kanon ma niewolę.
- Jeńcy z rabunku są brani spośród napadniętych.
- Weterani w karczmach: tylko prawdziwi zwolnieni żołnierze od 3. poziomu. Niewynajęty wraca po 120 dniach do domu.
- Wioski poboczne: 2441 wiosek w 423 okręgach. Płoną przy najazdach, a po roku wstają razem z powrotem uchodźców.
- Ludność liczona jako wszyscy żywi (Twoja decyzja). Nie ma sztucznego hamulca: jak ubywa, to ubywa.

**Etap 5 - pieniądze (cały):**
- **Kasy miast:** koniec „zakupów z niczego”, gra nie kasuje złota i nie dosypuje go. Nadwyżka kupców idzie do pana i korony.
- **Sakiewka ludu:** ludzie zarabiają i kupują za swoje. Wieś zatrzymuje 65% utargu.
- **Podatek wojenny korony** (dochodzi do daniny), **wyrównanie dla panów zamków**, **targ przy zamku**, **statki do portów** (koniec złota ze statków z niczego).
- **Markietani** (żołnierze wydają w polu), **notable** bez złota z niczego, **łup z bitew** tylko z sakiewki pokonanego.
- Własne pieniądze lordów nie liczą się jako dochód z ziemi, także przy dopłacie korony do żołdu (Twoje decyzje 5 i 7).
- **Pojmanie gracza:** każdy pojmujący (banda, lord, milicja) zabiera Ci całe złoto i cały rynsztunek do siebie, nic nie znika. Lord AI traci tylko część.
- Opłata za werbunek do załóg trafia do notabla albo osady, od której wzięto rekruta.
- **Audyt złota z niczego:** wszystkie duże źródła są znalezione i zamknięte poza dwoma (tabela w `docs/etap5/AUDYT-ZLOTO-Z-NICZEGO-2026-10-11.md`).

**Etap 6 (część):**
- Pomiar uzbrojenia i koni.
- **Kowale kują według kultury swojej krainy:** Północ nie kuje zbroi Lannisterów. Nikt poza Twoją kuźnią nie kuje unikatów. Dothrakowie i Wolni Ludzie nie kują stali.
- Warsztaty idą za surowcem, wielbłądy są tylko w Qarthu (to zadziała w nowej kampanii).

**CrashScribe:**
- **Oblężenie Harrenhal:** dowodzi Roose, a zastępca nie zostaje zamrożony. To samo naprawione pod Murem.
- **Wraca 3 atuty BannerKings:** wpływ +25%, sława +20% i +12%.
- **Strawa Północy:** tłocznia wina zostaje tam, gdzie są winogrona.
- **Logi:** różne błędy nie są już liczone jako jedna „powtórka”.

## 2. Testy (najpierw cały kod, potem jeden zestaw - Twoja zasada)

- **Test końcowy etapu 4** (przed etapem 5): zatrzymany na 403. dniu, gdy zmieniłeś zasadę testów. Wyszły błędy w kodzie i progach, poprawione:
  - jeden słownik gubił drużyny;
  - księga liczyła żołnierzy w trwającej bitwie;
  - zabici ostrzałem w oblężeniach byli poza księgą;
  - Nocna Straż nie brała ludzi z krain według tabeli;
  - wioski poboczne gubiły powiązanie ze swoją wsią.
- **Test zbiorczy etapów 4+5+6:**
  - **Pierwsza próba:** gra zawisła przed startem kampanii. Nowa łatka opłaty za werbunek zakładała się na model finansów rodów za wcześnie, to znana pułapka. Poprawione.
  - **Druga próba:**
    - 120 dni: Armoury bez błędu, 17,6 s na dobę gry;
    - zapis 9 dni z całym nowym stanem: OK;
    - zima 120 dni: OK.
  - **Problem:** wojsko lordów spadło do 78,6 tys., poniżej twardej podłogi 85 tys. Po samym etapie 4 było 106 tys.
    - Przyczyna: rodom ubyło złota z niczego. Kasy miast, statki, wieś i dwór dają im razem kilkaset tysięcy złota dziennie mniej.
    - Podatek wojenny miał wyrównać statki, a dał tylko 3–4 tys. zł dziennie zamiast 30–60 tys.
  - **Poprawka:**
    - renta rodu, który nie spełnia warunku, idzie do rodów, które go spełniają, zamiast zostawać w skarbcu;
    - korona dopłaca 70% żołdu zamiast 50%;
    - zamknięty wyciek, przez który BannerKings kasował złoto z sakiewek wsi.
  - **Test 120 dni po poprawce:** Armoury bez błędu, **wojsko w wojnie 88,4 tys.** (podłoga 85 tys., cel 95 tys.). Wtedy wgrałem.

## 3. Na czym stanęliśmy

- **Etap 6:**
  - **Porcja 2 w kodzie (nie wgrana):** licznik koni naprawiony u źródła i ryby z wiosek rybackich są gotowe, konie (K1, K2, K5) w toku, gałąź `w-toku/e6-p2`. Wejdzie do następnego wspólnego testu.
  - **Na następną sesję:** wytop przy kopalni (6.1), rycerz z własnym koniem (6.14), hodowla koni, naprawy sprzętu i reszta paczek z projektu `docs/etap6/`.
- **Etap 5, porcja C** (kasy bez dosypki, skutki głodu, dziesięcina, napis w menu miasta): po roku gry z porcją B, według projektu.
- **Małe źródła złota z audytu (na później):**
  - łup z rabunku wsi;
  - 5000 zł dla nowej załogi;
  - poziom rodu i atuty;
  - BannerKings i Diplomacy, najpierw pomiar.
- **Etapy 7 i 8:** nie zaczęte. Zabrakło limitu tygodniowego (79%).
- **Długi test (rok albo dwa lata) na całości:** raz, na końcu, gdy nie będziesz czekał na wynik (Twoja zasada).

## 4. Pytania do Ciebie (z radą)

1. **Wojsko 88 tys. przy celu 95 tys.** Dwie możliwości, można obie:
   - **(a)** Korona oddaje swój zapas skarbca dwa razy szybciej przez rok. To pomost, daje ok. +4–5 tys. wojska, potem zanika.
   - **(b)** Na stałe:
     - albo danina od nadwyżki sakiewek wsi 2,5% zamiast 1,5% (wsie się bogacą, bo zatrzymują 65% utargu);
     - albo przeterminowany sprzęt nie idzie do kasy siedziby w czasie wojny.

   Rada: (a) i (b) z daniną wsi. Razem ok. 92–98 tys.
2. **Rachunek ludzi małych krain (Sarnor).** Próg 0,1% jest nie do spełnienia, bo tam pojedyncze ruchy ludzi to już więcej. Rada: próg „0,1% albo 200 ludzi, co większe”.
3. **Juki z towarem i jedzeniem przy pojmaniu.** Dziś pojmujący zabiera 75% juków (Twoje ustawienie RealisticCaptivity). Też wszystko? Rada: tak, skoro „zabiera wszystko”.
4. **Zaułki w oblężonym mieście.** Teraz dochód z nich nadal płaci kasa miasta. Wstrzymać na czas oblężenia, jak inne opłaty? Rada: tak.
5. **Ubogie miasta.** Sakiewka ludu działa, ale średnio 8 z 97 miast nie stać na jedzenie przez cały czas. Rada: zostawić i zobaczyć w biegu rocznym, czy to się wyrównuje.
6. **Pomiar ludności.** Górną granicę przyrostu (+1150 ludzi dziennie) zostawiam jako kontrolę „ludzi z niczego”. Dół jest tylko pomiarem, bez sztucznego prawa. Zgoda?

## 5. Gdzie co jest (dla drugiego konta)

- **Kod:**
  - gałąź `noc/wgranie-32` = `w-toku/zbior-final` 40b975e;
  - CrashScribe: `noc/wgranie-32-cs`;
  - etap 6, porcja 2: `w-toku/e6-p2`.
- **Opis wgrania:** `docs/STAN-PRAC.md`, wpis „WGRANIE 32”. Decyzje z 12.10: w tym samym pliku, wpis „DECYZJE JEFFA”.
- **Przebieg nocy:** `docs/PRZEKAZANIE-2026-10-08.md`, ostatnie wpisy.
- **Projekty:** `docs/etap5/`, `docs/etap6/`. Audyt złota: `docs/etap5/AUDYT-ZLOTO-Z-NICZEGO-2026-10-11.md`.
- **Logi testów:** SCRATCH sesji ae222cb7, foldery `kopia-e4k`, `kopia-fin`, `kopia-fin3`.
