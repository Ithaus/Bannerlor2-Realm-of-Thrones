# Raport nocny 10/11.10.2026 (sesja ae222cb7, praca 15:20-03:00 zegara komputera)

Jeff, prosto i krotko: **etap 2 dokonczony, etap 3 caly w grze, bieg dwuletni przeszedl, etap 4 zaczety.**

## 1. Co jest w grze (6 wgran tej nocy, kazde po tescie 120 dni + tescie zapisu, z kopia)

| Wgranie | Co zmienia w grze |
|---|---|
| 23 | **Nocna Straz werbuje znowu** - jeden pulap dla calego zakonu (dotad stala na 1.5-2 tys., bo biedne rody Strazy mialy pulap 0). Blad namiotow naprawiony. |
| 24 | **Mniej dezercji AI i mniej ludzi do band:** kara morale za brak zaopatrzenia BK liczy dni braku (dotad w miescie liczyla cala narosla potrzebe, np. -88); Inni (wighty), ktorzy uciekaja, nie trafiaja do band; nadwyzka zalogi po powrocie patroli BK wraca do domu zamiast do lasu. |
| 25 | **3.2 Zwyciezca bierze cala kiese** rozbitego taboru, karawany i bandy (dotad 10% / 50%, reszta znikala). Korona bierze swoja 1/9. |
| 26 | **3.4-3.7** BK bez zlota z powietrza: lordowie kupuja napitki i drewno pod to, co maja w taborze, i placa kupcom; niewolnikow placi kasa osady; rada BK placi pensje z dochodu, ktory pan ma; wedrowcy BK bez towaru z niczego; ROT: rebelia bierze zloto z kasy zbuntowanego miasta. |
| 27 | **3.8-3.10** stale kwoty w starej monecie (zamowienia u kowala, nagrody szranek), lord dostaje pelna trzecia (33%) ze sprzedazy lupu swoich ludzi, lapowka w niewoli trafia do trzymajacych, Twoj rynsztunek zabrany przez bandytow zostaje w jukach bandy (odzyskasz go), furaz z zapasu wsi, herszt kryjowki z ludzi bandy. |
| 28 | **3.1 Szczelnosc:** jency, lup z oblezen, turnieje i myto placi kasa miasta; majatki BK zarabiaja tylko z utargu; zloto z cial z sakiewek pokonanych; zold karawan i oplaty za awans do sakiewek ludzi; kapital karawany z zaplaty. **Statki (3.3) sa w kodzie, ale WYLACZONE - czekaja na Twoja decyzje (pytanie 1).** |

**Cel etapu 3 osiagniety:** zloto, ktore "znika bez nazwy", spadlo z ok. 19 tys. zl dziennie do **-0.7 tys.** (cel: ponizej 10 tys.). Narzedzie progow etapu 3: 20 z 20 TAK - i w tescie 120 dni, i po dwoch latach.

## 2. Bieg dwuletni (728 dni na wszystkim, co wgrane)

- Przeszedl do konca: 728/728 dni, **0 bledow naszych modow** (2 bledy cudzych modow: BKROTPatch).
- Glowy rodow ponizej 5 000 zl: 0 / 6 / 3 w dniach 120 / 364 / 728 (prog: najwyzej 10). Bankrut: 1 (dzien 225). Bank: najmniej 2.65 mln.
- Wojsko: w pierwszym roku 99-101 tys. w druzynach lordow w wojnie; w drugim roku polowa krolestw zawarla pokoj, wiec wojska jest mniej (72 tys. razem), ale na jedno walczace krolestwo tyle samo co w bazie.
- Dezercja AI: na koniec 15 dziennie (baza 49). Zostaja **fale po wielkich przegranych bitwach** (pobita armia traci przez kilka dni czesc ludzi - pytanie 2).
- **Bandy rosna z wojna:** po roku ok. 25 tys. ludzi (baza 15.6 tys.), potem stoja na tym poziomie. Glowne zrodlo: rozbici z bitew uciekaja "w las" i fale dezercji; z lasu wraca do wsi tylko 2-3 ludzi dziennie. Naprawa jest w etapie 4 (paczka E4/3 - "punkty powrotu": wyrzutki wracaja do domu).
- **Znaleziony blad i poprawka:** gdy Straz zawarla pokoj, budzet zwolnil do domu 5 tys. jej ludzi (7.2 -> 1.2 tys.). Poprawione: **Straz nikogo nie zwalnia** (Mur nie rozpuszcza sie w pokoju) - w tescie razem z pierwsza paczka etapu 4.

## 3. Etap 4 (ksiega ludzi i zima) - zaczety

- Projekt gotowy (12 paczek, po krytyce): `docs/etap4/PROJEKT-ETAP4-KSIEGA-LUDZI-ZIMA-2026-10-10.md`.
- **E4/0 (sam pomiar ludzi, jencow, zimy - gra bez zmian)** + poprawka Strazy: test w toku, wgranie 29, jesli przejdzie.
- **E4/2 (Inni tylko z poleglych za Murem)** - kod gotowy, ale NIE wgrany: przed dniem 728 Inni mogliby slabnac szybciej (pytanie 9).

## 4. Pytania do Ciebie (kierunek; liczby wybieram sam)

1. **Statki:** wlaczyc zamkniecie zlota ze statkow teraz (wojsko lordow -8..-11% do etapu 5) czy razem z podatkiem wojennym w etapie 5? **Rada: z etapem 5.**
2. **Fale dezercji po wielkiej przegranej:** zostawic (pobita armia sie rozpierzcha - historyczne) czy zlagodzic? Rada: zostawic.
3. Kiesa bandy, ktora przylacza sie do Ciebie: dzis znika - ma przejsc do Ciebie?
4. Podatek BK od warsztatow lordow i Twoich w cudzych miastach: dzis nikt go nie placi (zniknal) - ma go placic wlasciciel panu miasta?
5. Doplata przy podmianie rekruta ROT: do sakiewki ludzi (jak awanse)?
6. Reguła "pokonany w bitwie" (komu sakiewka) takze dla druzyn lordow?
7. Napitki i drewno wojska: placi lord (dzis) czy sakiewka zolnierzy (jak odziez)?
8. Wedrowcy BK kiedys z prawdziwym towarem (kupionym w miescie, sprzedanym u celu)?
9. **Inni tylko z poleglych** (etap 4): moga przez to slabnac przed dniem 728 - tak ma byc, czy dac im pule startowa?
10. Etap 4: Sarnor 100 tys. (ROT) czy 20 tys. (kanon)? Kraina bez wolnych rak nie wystawia nowego wojska, az odrobi straty? Spalona wioska wraca po roku od ostatniego najazdu? Ludnosc w ciaglej wojnie moze spadac (do -0.5% rocznie)? Rada: 100 tys., tak, tak, tak.

## 5. Gdzie co jest (dla drugiego konta)

STAN-PRAC "WGRANIE 23..29", PRZEKAZANIE rozdz. 23-24 (START TUTAJ), galezie `noc/wgranie-23..29`, `w-toku/e3-*`, `w-toku/e4-0`, `w-toku/e4-2` (repo i GitHub).
