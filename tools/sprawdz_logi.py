#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
sprawdz_logi.py - zwiezly skrot sesji logu moda Armoury (Bannerlord / Realm of Thrones).

UZYCIE
  python sprawdz_logi.py [sciezka-logu] [opcje]
      bez sciezki: najnowszy  <gra>\\Modules\\Armoury\\Armoury-*.log
  --temat NAZWA      pelny szereg jednego tematu (wszystkie doby, wszystkie kolumny); nazwa bez rozrozniania
                     wielkosci liter: najpierw dokladna ("ruda"), potem poczatek ("przeplywy" = 5 tematow), potem fragment
  --dni A-B          tylko doby A-B (numery porzadkowe 1..N; liczby >= 10000 = dni gry, np. 108840-108850)
  --surowe           linie bez parsowania (z --temat: wszystkie linie tematu w calosci; bez: pierwsza i ostatnia kazdego tematu)
  --porownaj LOG2    ten sam skrot dla dwoch sesji obok siebie: pierwsza i ostatnia doba kazdego tematu
  --csv              skrot pliku ludzie-regiony.csv z katalogu sesji (Logs\\<sesja>\\): 8 regionow o najwiekszym obciazeniu
  --wczytanie N      gdy w jednym logu jest kilka kampanii / wczytan: ktora analizowac (domyslnie ta z najwieksza liczba dob)
  --test NAZWA       kontrole "po czym poznac w logu" z docs\\paczki\\<ogniwo>.md, kazda z wynikiem OK / UWAGA / BRAK DANYCH
                     i liczba z logu. NAZWA: numer ogniwa (102b, 106), kilka (103,104), zakres (102b-105), grupa testowa
                     (surowce = 102b-105, pieniadz = 106-107, ludzie = 108-109, dowoz = 100-101, ksiegi = 102-102b), slowo z
                     opisu ogniwa (paser, zold, karawany, mineral, przyrost...), "wykryte" (wszystkie ogniwa widoczne w logu)
                     albo "wszystkie". Bez tej opcji skrot pokazuje do 10 kontroli najwyzszego ogniwa wykrytego w logu.
  --pelny            bez limitu ok. 150 linii (wszystkie alarmy i wszystkie tematy dodatkowe)
  --szer N           szerokosc wyjscia (domyslnie 160)

UKLAD SKROTU (domyslnie ok. 150 linii, do 160 znakow)
  1. naglowek: plik, rozmiar, doby i dni gry, linie startowe modulow (wpiete / CZYNNE / BRAK / NIE wpieta / WYLACZONE),
     uklad ksiegi pieniadza (wpis 102 albo 102b), tabela ogniw lancucha paczek 100, 101, 102, 102b, 103 ... 109 (po czym
     poznac kazde w logu, ktore jest w grze, ktorych linii startowych nie ma) i linia "OGNIWO W GRZE"
  2. bledy: ERROR / Exception / potkniecia, pogrupowane, z numerem linii pierwszego wystapienia
  3. alarmy: reguly z docs\\paczki\\*.md i z raportu nocnego, kazdy z numerem linii logu (progi = stale na gorze pliku)
  4. co sprawdzic dla ogniwa w grze: do 10 kontroli najwyzszego wykrytego ogniwa, kazda w postaci
     "[OK | UWAGA | BRAK DANYCH] linia N: co sprawdzono -> liczba z logu" (kontrola OK = jedna linia, UWAGA i BRAK DANYCH
     do dwoch linii; "pomiar:" = paczka kaze liczbe zmierzyc, progu nie ma) oraz jedna linia o pozostalych ogniwach
     widocznych w logu; pelna lista i inne ogniwa: --test
  5. szeregi dzienne: wiersz = miara, kolumny = pierwsza doba, kilka rownomiernie rozlozonych, ostatnia
  6. formaty nierozpoznane: tematy, ktore spadly do trybu surowego (zmieniony albo nieznany format linii)

LANCUCH PACZEK (galezie 87c8e96 = wpis 102 w grze -> n102b-ksiega -> n103-karawany -> n104-zapas-startowy ->
  n105-mineral-bk -> n106-paser -> n107-zold-i-skarbiec -> n108-ludzie-jednostka -> n109-ludzie-przyrost). Ksiega pieniadza
  ma dwa uklady linii i skrypt czyta oba: wpis 102 (bilans z pozycja "(zold N", sekcja "zold wyplacony [P]") i 102b (bilans
  z "rozliczenia rodow na plus / na minus" i zdaniem "W tym zold naliczony N", nowa linia "Pieniadz swiata (rody):", sekcja
  "zold naliczony przy rozliczeniach rodow [P]"). Etykiety dnia: "Wyrzutki / Paser / Zold / Korona / Skarbce dzien D" to ten
  sam tick co "Ruda / Karawany / Dowoz / Przeplywy osad / Pieniadz swiata / Ludzie dzien D-1" (tabela PRZESUNIECIE_DNIA).

TEMATY Z PARSEREM KOLUMN (prefiks linii logu)
  surowce:   Ruda, Drewno, Dowoz, Dowoz (skutki), Karawany, Karawany (stan), Warsztaty, Rynek surowcow, Rynek broni,
             Mineraly (dubel BK)
  pieniadz:  Pieniadz swiata, Pieniadz swiata (bilans), Pieniadz swiata (rody), Przeplywy osad, Przeplywy osad (kasy miast |
             kasy zamkow | kiesy wsi), Korona (powinnosci | danina i clo | zwrot zoldu), Skarbce (suma krolestw), Zold,
             Sakiewka ludzi, Paser, IronBank
  ludzie:    Ludzie (z odcinkiem "hearth za ludzi dzis"), Ludzie (przyrost) = linia "Ludzie: przyrost naturalny",
             Ludzie (regiony), Ludnosc, Wyrzutki, Bitwy
  dodatkowe: ZakupyAI, Zuzycie AI, HouseLevies, Werbunek, Pobor, Ochotnicy, Komplet rekruta, Budowy oplacone, Budowy,
             PodazPopyt, WPLYW, UniqueLaw (dzien), LegendaryLaw (targi), Audyt predkosci, ArmsPricing (wojny)
  jednorazowe (naglowek, alarmy, kontrole): StartStock, MineralOnce, MaterialLaw (mnoznik wydobycia), CaravanBulk,
             MoneyLedger, OutlawLaw, SoldierPay, KingdomTreasury, PeopleUnit, PopulationLaw, Kalendarz, ColdStart
  Kazdy inny prefiks, ktory powtarza sie w dobach, jest pokazany w trybie surowym (linia obcieta).

Skrypt niczego nie zapisuje (tylko stdout, UTF-8). Nieznany albo zmieniony format linii nie wywraca skryptu:
temat spada do trybu surowego i jest wymieniony w sekcji "formaty nierozpoznane".

STAN NA 06.10: prawdziwego logu z ogniw 102-109 jeszcze nie ma (ostatnia sesja to 05.10 15:22, sprzed wpisu 101). Uklady
linii tych ogniw sa wyprowadzone z ciagow Log.Info w kodzie galezi i sprawdzone testem test_syntetyczny.py (kotwice wobec
literalow, linie zlozone mechanicznie z wyrazen kodu dla kazdej galezi lancucha, linie wypisane przez prawdziwy kod w
probach poza gra: ksiega 102 i 102b, "Paser:", "Ludzie:" z odcinkiem hearth, "Ludzie: przyrost naturalny"); linii
"Karawany:", "Karawany (stan):", "Mineraly (dubel BK):" i "Zold:" nowego lancucha nikt jeszcze nie wypisal prawdziwym
kodem - sa tylko z literalow. Progi alarmow pochodza z opisow paczek.
Pierwszy prawdziwy log po wgraniu ogniwa trzeba obejrzec takze z --surowe: gdyby kolumna byla pusta ("-") albo temat spadl
do "formatow nierozpoznanych", parser trzeba poprawic, a nie ufac alarmom tego tematu.
"""

import sys
import os
import re
import glob
import argparse
from collections import Counter, OrderedDict, defaultdict

# =====================================================================================================================
# STALE I PROGI ALARMOW (zrodla: docs/paczki/102b..109, docs/RAPORT-NOCNY-2026-10-06.md rozdz. 1-2, CHANGELOG wpisy 100-102)
# =====================================================================================================================
KATALOG_LOGOW = r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury'
WZORZEC_LOGU = 'Armoury-*.log'
PLIK_CSV = 'ludzie-regiony.csv'

SZEROKOSC = 160                 # znakow na linie wyjscia
LIMIT_LINII = 150               # domyslny limit dlugosci skrotu
PROBKI = 6                      # ile dob w szeregu (pierwsza, rownomiernie rozlozone, ostatnia)
MAKS_ALARMOW = 24               # ile alarmow w skrocie (reszta: --pelny)
MAKS_GRUP_BLEDOW = 8
MAKS_KONTROLI = 10              # ile kontroli ogniwa w grze w skrocie (wszystkie: --test NR)

# ruda / karawany (103)
PROG_MIAST_BEZ_RUDY_PROC = 60.0     # "Ruda: miast bez towaru" w ostatniej dobie, % miast (test 15:22: 60 z 97)
KARAWANY_MIN_DOB = 7                # po ilu dobach z ogniwem 103 oceniamy "ruda: bez towaru"
KARAWANY_BEZ_TOWARU_CEL = 20        # oczekiwane "kilka-kilkanascie" miast bez rudy po 10-20 dobach
KARAWANY_MIN_SPADEK_PROC = 25.0     # o ile % ma spasc "bez towaru" od pierwszej do ostatniej doby
KUPNO_STOI_MAKS_DOB = 3             # "KUPNO STOI" dluzej niz tyle dob z rzedu = alarm
KARAWANY_ROZRUCH_DOB = 10           # zero zakupow karawan przez tyle pierwszych dob = uwaga (po 104 rozruch 7-10 dob)
KARAWANY_BRAK_MIEJSCA_KROTNOSC = 2.0  # "brak miejsca w jukach" > tyle x "z zakupem" = uwaga
# ksiega rudy i drewna (102)
BEZ_WYJASNIENIA_RUDA_ABS = 50       # |bez wyjasnienia| rudy ponad tyle ladunkow ...
BEZ_WYJASNIENIA_RUDA_PROC = 2.0     # ... i ponad tyle % zapasu "razem"
BEZ_WYJASNIENIA_DREWNO_ABS = 2600   # drewno: znana dosypka RealisticBannerlord ok. +2100 dziennie; alarm dopiero wyzej
# zapas startowy (104) i mineral (105)
RUDA_RAZEM_PO_104 = 900             # "Ruda: razem" pierwszej doby po przeliczeniu ma byc ponizej
# Ogniwo 105 po przebudowie lancucha 06.10 NIE zmienia wydobycia: mnoznik rudy zostaje 3 ("wydobycie rudy x3.0" jest dobrze),
# wiec dawny alarm o x3.0 przy wgranym 105 zostal usuniety. Alarmem ogniwa 105 jest teraz "latka NIE wpieta".
MNOZNIK_RUDY_OCZEKIWANY = 3.0       # tylko do kontroli ogniwa 105; inna wartosc = ustawienie MineOutputMultiplier z MCM
STOSUNEK_NA_WIES_PO_105 = 1.3       # po 105: ("wsie dopisaly" / kopiace wsie) / ("model" / wsie modelu) ok. 1 (przed: ok. 2)
MINERALY_ZNANE = ('iron', 'salt', 'clay', 'silver', 'marble', 'gold_ore')
MINERALY_LIMIT_WSI = {'iron': 26, 'salt': 12, 'clay': 16, 'silver': 30}
# pieniadz (102, 102b)
RESZTA_BILANSU_PROC_RUCHU = 25.0    # |reszta| bilansu swiata ponad tyle % ruchu (zrodla + ujscia)
RESZTA_KAS_PROC_RUCHU = 25.0        # |reszta| kas miast / zamkow / wsi ponad tyle % sumy pozycji zmierzonych
RESZTA_KAS_MIN_ABS = 20000          # ... i ponad tyle zlota
ZOLD_DUBEL_OD = 0.5                 # reszta bilansu w przedziale 0.5-1.5 x zold = podwojne liczenie zoldu (uklad wpisu 102)
ZOLD_DUBEL_DO = 1.5
TICKI_OSAD_MIN_UDZIAL = 0.5         # "N tickow osad" konsumpcji ponizej tej czesci liczby osad = uwaga (ok. 97 miast, do 130 zamkow)
ROZLICZEN_ROZRZUT_PROC = 15.0       # "w K rozliczeniach": K ma sie wahac o kilka miedzy dobami; rozrzut ponad tyle % mediany = uwaga
# dowoz (100-101)
ZATKANE_ROZNICA_PP = 5.0            # zatkane magazyny: wsie zamkowe % minus wsie miejskie % ponad tyle punktow = uwaga
# paser (106)
PASER_MIN_DOB_BEZ_AWANSOW = 5       # "awanse: od pasera" = 0 przez tyle dob z ogniwem 106 = uwaga
PASER_ZOSTALO_DOB = 3               # "z ladunkiem zostalo" > 0 przez tyle dob z rzedu = alarm (paser ma obslugiwac 100% band)
# zold i skarbiec (107)
ZOLD_ROZJAZD_PROC = 2.0             # "z kies zeszlo" wobec sumy pozycji "przekazano" + "nie przekazano", %
ZOLD_KSIEGA_ROZJAZD_PROC = 10.0     # "Zold: naliczony" wobec licznika zoldu w "Przeplywy osad", %
SAKIEWKA_ROSNIE_DOB = 7             # "najwieksza" sakiewka rosnie tyle dob z rzedu = uwaga
# ludzie (102, 108, 109)
LUDNOSC_ROZJAZD_MLN = 0.2           # "Ludzie: ludnosc" wobec "Ludnosc:" (mln)
TABORY_ZERO_DOB = 3                 # "tabory wsi i lodzie ... za 0 ludzi" przez tyle dob przy istniejacych taborach = uwaga
PRZYROST_MIN_LUDZI = 100            # "Ludzie: przyrost naturalny +N ludzi": N poza tym przedzialem = alarm
PRZYROST_MAKS_LUDZI = 1500          # (rachunek paczki 109: pokoj +592, wojna wszedzie +245, perk u wszystkich +806)
PRZYROST_G_MIN = 0.1                # "g srednio" poza tym przedzialem = uwaga (oczekiwane 0.2-0.5 % rocznie)
PRZYROST_G_MAKS = 0.8
PRZYROST_RESZTA_HEARTH = 25.0       # |RESZTA| hearth wsi na dobe ponad tyle = uwaga (inwestycje BetterEconomy to +65..135)
LUDNOSC_MAKS_ZMIANA_PROC = 0.05     # ludnosc swiata zmienia sie o ponad tyle % dziennie przy ogniwie 109 = alarm

# przesuniecie numeru dnia w linii: 1 = linia drukuje (int)Now.ToDays (dzien biezacy), 0 = dzien wlasnie zakonczony
# (Now.ToDays - 1). Ustalenie przegladu kolizji lancucha: "Wyrzutki / Paser / Zold / Korona / Skarbce dzien D" to ten sam
# tick co "Ruda / Karawany / Dowoz / Przeplywy osad / Pieniadz swiata / Ludzie dzien D-1". Klucz = prefiks linii logu, wiec
# "Ludzie: przyrost naturalny ... dzien N ..." (ogniwo 109) idzie z "Ludzie" (ten sam dzien co linia "Ludzie: dzien N").
# Doba skrotu = dzien zakonczony; tematow spoza tabeli skrypt uczy sie z sasiednich linii tej samej sekundy.
PRZESUNIECIE_DNIA = {
    'Bitwy': 1, 'Bitwa': 1, 'Budowy oplacone': 1, 'Budowy': 1, 'Budowa': 1, 'Korona': 1, 'Skarbce': 1, 'Finanse': 1,
    'Wyrzutki': 1, 'Paser': 1, 'Ludnosc': 1, 'Zold': 1, 'IronBank': 1, 'HouseLevies': 1, 'Audyt predkosci': 1,
    'Kronika unikatow': 1, 'Klimat': 1,
    'Ruda': 0, 'Drewno': 0, 'Dowoz': 0, 'Dowoz (skutki)': 0, 'Karawany': 0, 'Karawany (stan)': 0,
    'Mineraly (dubel BK)': 0, 'Pieniadz swiata': 0, 'Pieniadz swiata (bilans)': 0, 'Pieniadz swiata (rody)': 0,
    'Przeplywy osad': 0, 'Przeplywy osad (kasy miast)': 0, 'Przeplywy osad (kasy zamkow)': 0,
    'Przeplywy osad (kiesy wsi)': 0,
    'Ludzie': 0, 'Ludzie (regiony)': 0, 'ZakupyAI': 0, 'Zuzycie AI': 0, 'Pobor': 0, 'Werbunek': 0,
    'Sakiewka ludzi': 0, 'Komplet rekruta': 0, 'Ochotnicy': 0, 'Warsztaty': 0,
}
ZNACZNIK_KAMPANII = 'Behavior dodany do kampanii'

# =====================================================================================================================
# NARZEDZIA
# =====================================================================================================================
TYPY = {
    'int': r'(-?\d+)',
    'sint': r'([+-]?\d+)',
    'float': r'([+-]?\d+(?:[.,]\d+)?)',
    'slowo': r'([^\s,;.()]+)',
}


def _liczba(s):
    """Tekst -> int albo float albo tekst (nigdy wyjatek)."""
    if s is None:
        return None
    try:
        return int(s)
    except (TypeError, ValueError):
        pass
    try:
        return float(str(s).replace(',', '.'))
    except (TypeError, ValueError):
        return s


class Kol(object):
    """Jedna kolumna tematu: kotwica (doslowny tekst z kodu tuz przed wartoscia) + typ albo wlasne wyrazenie.
    sek = fragment tekstu, po ktorym poznac sekcje linii (miedzy ' | '); krotka = kilka wariantow (dwa uklady tej samej linii).
    zr = plik .cs, z ktorego pochodza literaly kolumny, gdy inny niz plik tematu (tylko dla testu kotwic)."""

    def __init__(self, nazwa, kot=None, typ='int', rx=None, sek=None, wym=False, lit=None, zr=None):
        self.nazwa, self.kot, self.typ, self.wym, self.zr = nazwa, kot, typ, wym, zr
        self.sek = tuple(sek) if isinstance(sek, (list, tuple)) else ((sek,) if sek else None)
        if rx is not None:
            self.rx = re.compile(rx)
            self.lit = list(lit or [])
        elif typ == 'delta':            # "kotwica 1234 (+56" -> +56
            self.rx = re.compile(re.escape(kot) + r'-?\d+(?:[.,]\d+)? \(([+-]\d+)')
            self.lit = list(lit) if lit is not None else [kot]
        else:
            self.rx = re.compile(re.escape(kot) + TYPY[typ])
            self.lit = list(lit) if lit is not None else [kot]

    def czytaj(self, tresc, sekcje):
        zrodlo = tresc
        if self.sek:
            zrodlo = None
            for czesc in sekcje:
                if any(s in czesc for s in self.sek):
                    zrodlo = czesc
                    break
            if zrodlo is None:
                return None
        m = self.rx.search(zrodlo)
        if not m:
            return None
        g = m.groups()
        if len(g) >= 2 and g[1] is not None:    # "+a/-b" -> a - b
            a, b = _liczba(g[0]), _liczba(g[1])
            if isinstance(a, (int, float)) and isinstance(b, (int, float)):
                return a - b
        v = _liczba(g[0])
        # kolumna liczbowa z tekstem zamiast liczby (np. "1.2.3" albo sam przecinek w uszkodzonej linii) = brak wartosci;
        # inaczej tekst trafilby do porownan w regulach alarmow i kontrolach
        if self.typ != 'slowo' and not isinstance(v, (int, float)):
            return None
        return v


def K(nazwa, kot=None, typ='int', rx=None, sek=None, wym=False, lit=None, zr=None):
    return Kol(nazwa, kot, typ, rx, sek, wym, lit, zr)


RX_F = r'([+-]?\d+(?:[.,]\d+)?)'      # liczba z ulamkiem i znakiem (do wlasnych wyrazen kolumn)


def KD(nazwa, kot, wym=False):
    """Wartosc i jej zmiana dobowa w nawiasie: 'kotwica 123 (+4)'."""
    return [K(nazwa, kot, 'int', wym=wym), K(nazwa + '_zm', kot, 'delta')]


class Temat(object):
    """Rodzaj linii logu: prefiks + (opcjonalnie) wzorzec poczatku reszty linii + kolumny."""

    def __init__(self, nazwa, prefiks, rodzaj=None, kol=(), extra=None, skrot=(), grupa='dodatkowe', zrodlo=None,
                 agreg=None, dzienny=True, bezwar=False, wielo=False):
        self.nazwa, self.prefiks = nazwa, prefiks
        self.rodzaj = re.compile(rodzaj) if rodzaj else None
        self.kol = []
        for k in kol:
            if isinstance(k, (list, tuple)):
                self.kol.extend(k)
            else:
                self.kol.append(k)
        self.extra, self.skrot, self.grupa, self.zrodlo = extra, list(skrot), grupa, zrodlo
        self.agreg, self.dzienny, self.bezwar, self.wielo = agreg, dzienny, bezwar, wielo

    def pasuje(self, reszta):
        return self.rodzaj is None or self.rodzaj.search(reszta) is not None

    def czytaj(self, tresc):
        """Zwraca (slownik wartosci, lista brakujacych pol wymaganych)."""
        sekcje = tresc.split(' | ')
        d, braki = OrderedDict(), []
        for k in self.kol:
            try:
                v = k.czytaj(tresc, sekcje)
            except Exception:
                v = None
            d[k.nazwa] = v
            if v is None and k.wym:
                braki.append(k.nazwa)
        if self.extra is not None:
            try:
                dod = self.extra(tresc, d)
                if dod:
                    d.update(dod)
            except Exception as e:
                braki.append('extra:%s' % type(e).__name__)
        return d, braki


def skr(v, znak=False):
    """Krotki zapis liczby do komorki skrotu."""
    if v is None:
        return '-'
    if isinstance(v, bool):
        return 'TAK' if v else 'nie'
    if isinstance(v, float):
        if abs(v) >= 10000:
            return skr(int(round(v)), znak)
        s = ('%.3f' % v).rstrip('0').rstrip('.') if abs(v) < 1000 else '%.0f' % v
        if s in ('-0', ''):
            s = '0'
        return ('+' + s) if (znak and v >= 0) else s
    if isinstance(v, int):
        a = abs(v)
        if a < 10000:
            s = str(v)
        elif a < 1000000:
            s = '%.1fk' % (v / 1000.0)
        else:
            s = '%.2fM' % (v / 1000000.0)
        return ('+' + s) if (znak and v >= 0) else s
    return str(v)


RX_SZABLON = re.compile(r'\{(\w+)(:\+)?\}')


def wypelnij(szablon, d):
    """Szablon komorki '{a}/{b:+}' -> tekst; '-' gdy zadnej wartosci nie ma."""
    if d is None:
        return '-'
    trafione = [0]

    def zamien(m):
        v = d.get(m.group(1))
        if v is None:
            return '-'
        trafione[0] += 1
        return skr(v, bool(m.group(2)))
    try:
        s = RX_SZABLON.sub(zamien, szablon)
    except Exception:
        return '?'
    return s if trafione[0] else '-'


def obetnij(s, n):
    s = s.replace('\t', ' ')
    return s if len(s) <= n else s[:max(0, n - 3)] + '...'


def wycinek(tresc, n):
    """Linia skrocona tak, zeby slowo BRAK / NIE wpiet / WYLACZON bylo widac (inaczej ginie za obcieciem)."""
    m = re.search(r'\bBRAK\b|NIE wpiet|WYLACZON|regula wylaczona', tresc)
    if not m or m.end() + 30 <= n:
        return obetnij(tresc, n)
    pref = tresc.split(':', 1)[0][:30]
    od = max(0, m.start() - 35)
    return obetnij(pref + ': ... ' + tresc[od:], n)


def proc(a, b):
    try:
        return 100.0 * a / b if b else None
    except Exception:
        return None


def lista_par(txt):
    """'ruda 12, drewno 30, skory surowe 4' -> {'ruda': 12, ...}; 'nic' -> {}."""
    d = OrderedDict()
    if not txt:
        return d
    for m in re.finditer(r'([A-Za-z_][\w /-]*?) (-?\d+)(?=,|$|\])', txt.strip()):
        d[m.group(1).strip()] = int(m.group(2))
    return d


def lista_par_f(txt):
    """'ruda 0.05, welna 49.125' -> {'ruda': 0.05, ...}; '-' albo 'nic' -> {}. Liczby z kropka dziesietna."""
    d = OrderedDict()
    if not txt:
        return d
    for m in re.finditer(r'([A-Za-z_][\w /-]*?) (-?\d+(?:\.\d+)?)(?=,|$)', txt.strip()):
        d[m.group(1).strip()] = float(m.group(2))
    return d


def mediana(xs):
    """Mediana listy liczb (None pomijane); None dla pustej."""
    v = sorted(x for x in xs if isinstance(x, (int, float)) and not isinstance(x, bool))
    return v[len(v) // 2] if v else None


def najdluzsza_seria(flagi):
    """Najdluzszy ciag prawdziwych wartosci: (dlugosc, indeks poczatku) albo (0, None)."""
    naj, pocz, biez, biez_pocz = 0, None, 0, None
    for i, f in enumerate(flagi):
        if f:
            if biez == 0:
                biez_pocz = i
            biez += 1
            if biez > naj:
                naj, pocz = biez, biez_pocz
        else:
            biez = 0
    return naj, pocz


# =====================================================================================================================
# TEMATY - kolumny wyprowadzone z ciagow Log.Info (galaz n109-ludzie-przyrost = szczyt lancucha; uklad ksiegi pieniadza
# wpisu 102 z commitu 87c8e96, ktory jest w grze) i z prawdziwych logow
# =====================================================================================================================
def _kol_ksiegi(zrodlo_miast):
    return [
        K('dopisaly', 'wsie dopisaly ', wym=True),
        K('wsi_kopie', rx=r' t, (\d+) wsi', lit=[' t, ', ' wsi; model ']),
        K('model', '; model ', 'float', wym=True, lit=[' wsi; model ']),
        K('model_wsi', rx=r'; model [\d.,]+ w (\d+) wsiach', lit=[' wsiach z wynikiem > 0), ']),
        K('z_miast', rx=r'(?:kopalnie miast i zamkow|, miasta i zamki) \+(-?\d+)', lit=[zrodlo_miast]),
        K('warsztaty', 'zuzycie: warsztaty zbrojne ', wym=True),
        K('linie', rx=r'linie towarowe \([^)]*\) (-?\d+)', lit=[', linie towarowe (']),
        K('budowy', ', budowy '),
        K('miasta', '; zapas: miasta ', wym=True),
        K('miasta_zm', '; zapas: miasta ', 'delta'),
        K('zamki', ', zamki '),
        K('wsie', ', wsie '),
        K('tabory', ', tabory '),
        K('t_wiesniacy', ' (wiesniacy '),
        K('t_karawany', ', karawany '),
        K('t_lordowie', ', lordowie '),
        K('t_inne', ', inne '),
        K('t_bandy', ' - w tym bandy '),
        K('razem', ', razem ', wym=True, lit=['), razem ']),
        K('zmiana', ', razem ', 'delta', lit=['), razem ']),
        K('bez_wyjasnienia', ', bez wyjasnienia ', 'sint'),
        K('bez_towaru', '; miast bez towaru ', wym=True),
        K('miast', rx=r'; miast bez towaru \d+ z (\d+)', lit=['; miast bez towaru ']),
        K('zima', '; zima: ', 'slowo'),
    ]


def _extra_ksiegi(t, d):
    r = {}
    if d.get('dopisaly') is not None and d.get('model'):
        r['dopisaly_do_modelu'] = round(d['dopisaly'] / float(d['model']), 2)
        # na jedna wies: tyle dopisala kopiaca wies wobec tego, co liczy dla niej model (po ogniwie 105 ok. 1, przed ok. 2).
        # Tylko uklad od wpisu 102 ("w N wsiach z wynikiem > 0"); wczesniej model byl pytany takze o wsie bez tego surowca
        # ("model 96.5 w 92 wsiach" przy 26 kopalniach) i iloraz na wies nic nie znaczyl.
        if d.get('wsi_kopie') and d.get('model_wsi') and 'wsiach z wynikiem > 0' in t:
            r['na_wies_do_modelu'] = round((d['dopisaly'] / float(d['wsi_kopie'])) / (float(d['model']) / d['model_wsi']), 2)
    if d.get('bez_towaru') is not None and d.get('miast'):
        r['bez_towaru_proc'] = round(100.0 * d['bez_towaru'] / d['miast'], 1)
    return r


SKROT_KSIEGI = [
    ('wsie dopisaly / model', '{dopisaly}/{model}'),
    ('zuzycie: warsztaty/linie/budowy', '{warsztaty}/{linie}/{budowy}'),
    ('zapas: miasta/tabory/razem', '{miasta}/{tabory}/{razem}'),
    ('bez wyjasnienia | miast bez towaru', '{bez_wyjasnienia:+} | {bez_towaru}'),
]


def _extra_dowoz(t, d):
    return {'wylaczona': 1 if 'LATKA WYLACZONA' in t else 0}


def _extra_skutki(t, d):
    r = {}
    if d.get('zatk_zamkowe') is not None and d.get('wsi_zamkowych'):
        r['zatk_zamkowe_proc'] = round(100.0 * d['zatk_zamkowe'] / d['wsi_zamkowych'], 1)
    if d.get('zatk_miejskie') is not None and d.get('wsi_miejskich'):
        r['zatk_miejskie_proc'] = round(100.0 * d['zatk_miejskie'] / d['wsi_miejskich'], 1)
    return r


def _extra_karawany(t, d):
    r = OrderedDict()
    for klucz, wz in (('sprz', r'sprzedane miastom z brakiem: (.*?) \(miasta zaplacily'),
                      ('kup', r'kupione z nadwyzek: (.*?) \(karawany zaplacily'),
                      ('oddane', r'zakupy gabki BK oddane na polke: (.*?) \(zwrot')):
        m = re.search(wz, t)
        pary = lista_par(m.group(1)) if m else None
        if pary is None:
            continue
        r[klucz + '_szt'] = sum(pary.values())
        if klucz != 'oddane':
            r[klucz + '_ruda'] = pary.get('ruda', 0)
            r[klucz + '_drewno'] = pary.get('drewno', 0)
    # ogniwo 103 po decyzji 06.10: kolejnosc kupna wedle zysku w danej chwili i oczekiwany zysk z kupionego
    m = re.search(r'wyjazdow, ktore zaczely zakupy od: (.*?); oczekiwany zysk', t)
    if m:
        pary = lista_par(m.group(1))
        r['first_szt'] = sum(pary.values())
        r['first_ruda'] = pary.get('ruda', 0)
        r['first_drewno'] = pary.get('drewno', 0)
        r['first_naj'] = max(pary, key=lambda k: pary[k]).replace(' ', '_') if pary else 'nic'
    m = re.search(r'minus zaplacone\): (.*?) \(razem -?\d+ d\)', t)
    if m:
        pary = lista_par_f(m.group(1))
        r['zysk_ruda_kg'] = pary.get('ruda')
        r['zysk_drewno_kg'] = pary.get('drewno')
    r['wylaczona'] = 1 if 'REGULA WYLACZONA' in t else 0
    return r


SUROWCE_KARAWAN = OrderedDict([('ruda', 'ruda'), ('drewno', 'drewno'), ('skory surowe', 'skory'), ('skora', 'skora'),
                               ('len', 'len'), ('plotno', 'plotno'), ('welna', 'welna')])
# "cena zbytu" (ogniwo 103 po decyzji 06.10) jest opcjonalna: linie starszej postaci jej nie maja
RX_STAN_KARAWAN = re.compile(r'(?:- |; )([a-z][a-z ]*?): ponizej zapasu docelowego (\?|-?\d+) -> (-?\d+) '
                             r'\(bez towaru (\?|-?\d+) -> (-?\d+)\), brakuje (-?\d+), w jukach karawan (-?\d+)'
                             r'(?:, cena zbytu (?:(-?\d+(?:[.,]\d+)?) d \(kupuje (\d+) miast\)|(brak - nikt nie kupuje)))?'
                             r'(, KUPNO STOI)?')


def _extra_stan_karawan(t, d):
    r = OrderedDict()
    stoi, bez_ceny = [], []
    n = 0
    for m in RX_STAN_KARAWAN.finditer(t):
        nazwa = SUROWCE_KARAWAN.get(m.group(1).strip(), m.group(1).strip().replace(' ', '_'))
        n += 1
        r[nazwa + '_ponizej'] = int(m.group(3))
        r[nazwa + '_bez'] = int(m.group(5))
        r[nazwa + '_brak'] = int(m.group(6))
        r[nazwa + '_juki'] = int(m.group(7))
        if m.group(8) is not None:
            r[nazwa + '_cena'] = _liczba(m.group(8))
            r[nazwa + '_kupuje'] = int(m.group(9))
        elif m.group(10):
            r[nazwa + '_cena'] = 0
            r[nazwa + '_kupuje'] = 0
            if int(m.group(6)) > 0:             # miasta z kasa maja brak, a nikt nie daje ceny = wycena sie nie udala
                bez_ceny.append(nazwa)
        r[nazwa + '_stoi'] = 1 if m.group(11) else 0
        if m.group(11):
            stoi.append(nazwa)
    if n == 0:
        raise ValueError('brak surowcow')
    r['kupno_stoi'] = ','.join(stoi) if stoi else 'nie'
    r['bez_ceny'] = ','.join(bez_ceny)
    return r


def _extra_rynek_surowcow(t, d):
    m = re.search(r'zapasy na targach miast \[([^\]]*)\]', t)
    if not m:
        raise ValueError('brak listy')
    pary = lista_par(m.group(1))
    if not pary:
        raise ValueError('pusta lista')
    return pary


def _extra_rynek_broni(t, d):
    r = OrderedDict()
    for nazwa, slowo in (('ruda', 'indeks rudy min/med/max'), ('drewno', 'drewna'), ('skora', 'skory'), ('len', 'lnu')):
        m = re.search(re.escape(slowo) + r' (-?[\d.]+)/(-?[\d.]+)/(-?[\d.]+)', t)
        if m:
            r['idx_%s_min' % nazwa] = _liczba(m.group(1))
            r['idx_%s_med' % nazwa] = _liczba(m.group(2))
            r['idx_%s_max' % nazwa] = _liczba(m.group(3))
    m = re.search(r'premie wojenne: (.*?)\.?$', t)
    if m:
        r['premie'] = 0 if m.group(1).strip().startswith('brak') else m.group(1).count('%')
    return r


def _extra_mineraly(t, d):
    r = OrderedDict()
    if 'latka NIE wpieta' in t:
        r['stan'] = 'NIE wpieta'
    elif 'LATKA WYLACZONA' in t:
        r['stan'] = 'WYLACZONA'
    elif 'zadnego powtorzenia' in t:
        r['stan'] = 'zadnych'
    elif 'powtorzenia zdjete' in t:
        r['stan'] = 'zdjete'
    else:
        raise ValueError('nieznany stan')
    m = re.search(r'\(przedmiot i liczba wsi\): (.*?); razem', t)
    pary = lista_par(m.group(1)) if m else OrderedDict()
    for k in MINERALY_ZNANE:
        r[k] = pary.get(k, 0) if m else None
    obce = [k for k in pary if k not in MINERALY_ZNANE]
    r['obce'] = ','.join(obce) if obce else ''
    return r


UKLAD_102, UKLAD_102B = '102', '102b'      # uklad linii ksiegi pieniadza: wpis 102 (w grze dzis) i poprawka n102b-ksiega


def _extra_bilans(t, d):
    r = OrderedDict()
    zr, uj, re_ = d.get('zrodla'), d.get('ujscia'), d.get('reszta')
    if zr is not None and uj is not None and re_ is not None:
        ruch = abs(zr) + abs(uj)
        r['ruch'] = ruch
        r['reszta_proc'] = round(100.0 * abs(re_) / ruch, 1) if ruch else None
    # 102b: zold siedzi w "rozliczeniach rodow" i jest podany osobno ("W tym zold naliczony"); 102: zold jest pozycja ujsc
    r['uklad'] = UKLAD_102B if ('rozliczenia rodow na ' in t or 'W tym zold naliczony' in t) else UKLAD_102
    r['rody_niewpiete'] = 1 if 'licznik rozliczen rodow nie jest wpiety' in t else 0
    return r


def _extra_rody(t, d):
    r = OrderedDict()
    r['niewpiety'] = 1 if 'licznik rozliczen rodow nie jest wpiety' in t else 0
    if d.get('zmiana') is None and not r['niewpiety']:
        raise ValueError('brak zmiany zlota swiata')
    if d.get('zmiana') is None:                 # licznik nie wpiety: linia podaje sam zold
        m = re.search(r' zold naliczony (-?\d+) \[P\] siedzi w tych saldach', t)
        if m:
            r['zold'] = int(m.group(1))
    a, b = d.get('inne_z_niczego'), d.get('inne_w_nicosc')
    r['inne_razem'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    return r


def _extra_przeplywy(t, d):
    r = {'licznik_slepy': 1 if 'licznik nie widzial wyplat' in t else 0}
    m = re.search(r'Potkniecia licznikow: okna taborow niedomkniete (\d+), konsumpcja bez pary (\d+), '
                  r'regulator pytany poza konsumpcja (\d+), wyjatki (\d+)', t)
    r['potkniecia'] = sum(int(x) for x in m.groups()) if m else 0
    r['uklad'] = UKLAD_102B if 'zold naliczony przy rozliczeniach rodow' in t else UKLAD_102
    return r


def _extra_kasy(t, d):
    r = {}
    znane = 0
    for k in ('taborom', 'z_utargu', 'zakupy', 'regulator', 'przelewy', 'poza_tickiem', 'tick'):
        v = d.get(k)
        if isinstance(v, (int, float)):
            znane += abs(v)
    r['ruch'] = znane
    return r


# nazwa regionu moze byc pusta (osada bez nazwy w probie poza gra) - wtedy czesc zaczyna sie od "[miasto, ...]"
RX_REGION = re.compile(r'^(.*?)\s*\[(miasto|zamek), ([^\]]*)\] ludzie (-?\d+)(?: \(([+-]\d+)\))?, zaloga (\d+), '
                       r'wyrzutki (\d+), bandy (\d+) w (\d+), zabici dzis (\d+), zwerbowani dzis (\d+), '
                       r'obciazenie (-?[\d.]+)%')


def _extra_regiony(t, d):
    m = re.search(r'ludnosci\): (.*)$', t)
    if not m:
        raise ValueError('brak listy regionow')
    regiony = []
    for czesc in m.group(1).rstrip('.').split('; '):
        mm = RX_REGION.match(czesc.strip())
        if mm:
            regiony.append((mm.group(1), mm.group(2), mm.group(3), int(mm.group(4)), int(mm.group(6)),
                            int(mm.group(7)), int(mm.group(8)), float(mm.group(12))))
    if not regiony:
        raise ValueError('zaden region nie pasuje')
    r = OrderedDict()
    r['top1'] = regiony[0][0]
    r['top1_kr'] = regiony[0][0][:10].strip()
    r['top1_proc'] = regiony[0][7]
    r['top1_ludzie'] = regiony[0][3]
    r['ostatni_proc'] = regiony[-1][7]
    r['ponad_20'] = sum(1 for x in regiony if x[7] > 20.0)
    r['lista'] = ', '.join('%s %.1f%%' % (x[0], x[7]) for x in regiony)
    return r


def _extra_ludnosc(t, d):
    r = {}
    if d.get('zaplacone') is not None and d.get('nalezne'):
        r['zaplacone_proc'] = round(100.0 * d['zaplacone'] / d['nalezne'], 1)
    return r


def _extra_paser(t, d):
    r = OrderedDict()
    r['skup_wylaczony'] = 1 if 'skup lupu WYLACZONY' in t else 0
    r['kurek_otwarty'] = 1 if 'KUREK OTWARTY w ustawieniach - gra dopisala' in t else 0      # kurek zlota kryjowek
    r['kurek_zywnosci'] = 1 if ('zywnosc z niczego: KUREK OTWARTY' in t or 'zywnosci paser nie bierze' in t) else 0
    r['obieg_wylaczony'] = 1 if 'OBIEG KAS WYLACZONY' in t else 0
    a, b = d.get('zabl_bandom'), d.get('zabl_kryjowkom')
    r['zabl_razem'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    if d.get('z_ladunkiem') is not None and d.get('z_paserem') is not None:
        r['bez_pasera'] = d['z_ladunkiem'] - d['z_paserem']
    a, b = d.get('zycie_bandy'), d.get('zycie_kryjowki')
    r['zycie_razem'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    potk = [d.get(k) for k in ('potk_skup', 'potk_kryjowki', 'potk_zycie', 'potk_awanse')]
    r['potk_razem'] = sum(v or 0 for v in potk)
    return r


def _extra_ludzie(t, d):
    r = OrderedDict()
    r['nieskalibrowana'] = 1 if 'ludnosc nieskalibrowana' in t else 0
    # ogniwo 108: odcinek "hearth za ludzi dzis (...)" - jednostka 1 czlowiek = 1/k hearth
    if 'hearth za ludzi dzis (' in t:
        r['jednostka'] = 'WYLACZONA' if 'jednostka ludzi WYLACZONA' in t else 'czynna'
        r['od_ludnosci'] = 1 if 'wyrzutki liczone od ludnosci' in t else 0
        if d.get('hz_tabory') and d.get('hz_gra') is not None:
            r['hz_gra_do_naszych'] = round(d['hz_gra'] / float(d['hz_tabory']), 1)
    return r


RX_KRAINA = re.compile(r'([\w-]+) ([+-]?\d+(?:[.,]\d+)?)% \(([+-]?\d+)\)')


def _extra_przyrost(t, d):
    r = OrderedDict()
    r['wylaczony'] = 1 if 'przyrost naturalny WYLACZONY' in t else 0
    if d.get('ludzi') is None and not r['wylaczony']:
        raise ValueError('brak liczby ludzi przyrostu')
    r['pierwsza_doba'] = 1 if 'rozliczenie zmiany od jutra' in t else 0
    if ' | miasta: ' in t:
        r['zamrozone'] = 1 if 'stan (zamrozony)' in t else 0
        r['miasta_nieskalibrowane'] = 1 if 'miasta: ludnosc nieskalibrowana' in t else 0
    m = re.search(r'krainy g rocznie \(ludzi dzis\): (.*?)(?: \| |$)', t)
    if m:
        kr = [(x.group(1), _liczba(x.group(2)), int(x.group(3))) for x in RX_KRAINA.finditer(m.group(1))]
        r['krain'] = len(kr)
        if kr:
            r['kraina_g_min'] = min(k[1] for k in kr)
            r['kraina_g_max'] = max(k[1] for k in kr)
    # sprawdzian z opisu paczki 109: N x dni roku / ludnosc wsi ma dawac "g srednio" - tu sama stopa z "w rok ok."
    if d.get('ludzi') is not None and d.get('w_rok') is not None and d['ludzi']:
        r['dni_roku'] = int(round(d['w_rok'] / float(d['ludzi'])))
    return r


def _extra_zold(t, d):
    r = OrderedDict()
    r['tarcza_wl'] = 1 if 'tarcza zoldu w kasach miast: wlaczona' in t else 0
    zeszlo = [d.get('rody_zeszlo'), d.get('gar_zeszlo')]
    dalej = [d.get(k) for k in ('do_sakiewek', 'do_miast', 'do_zamkow', 'nieumarli', 'bez_osady', 'wylaczone')]
    if all(v is not None for v in zeszlo) and all(v is not None for v in dalej):
        r['zeszlo_razem'] = sum(zeszlo)
        r['rozliczone'] = sum(dalej)
        r['rozjazd'] = sum(zeszlo) - sum(dalej)
    if d.get('rody_naliczony') is not None and d.get('gar_naliczony') is not None:
        r['naliczony_razem'] = d['rody_naliczony'] + d['gar_naliczony']
    return r


RX_SKARBIEC = re.compile(
    r'^dzien \d+ - (.+?)( \(WOJNA\))?: skarbiec (-?\d+)(?: \(([+-]?\d+)\))?'
    r'(?:, zwrot zoldu (-?\d+) dla (\d+) rodow(?: \(nalezne (-?\d+) - skarbiec nie mial dosc\))?)?'
    r', krol (.*?) kiesa (-?\d+) bilans ([+-]?\d+)/dzien; rodow (\d+), kiesy razem (-?\d+), '
    r'biednych \(<(-?\d+)\) (\d+), na minusie (\d+); wojsko rodow (\d+) ludzi')


def _extra_skarbiec(t, d):
    m = RX_SKARBIEC.search(t[t.find('dzien '):] if 'dzien ' in t else t)
    if not m:
        raise ValueError('linia krolestwa nie pasuje')
    g = m.groups()
    return OrderedDict([
        ('krolestwo', g[0]), ('wojna', 1 if g[1] else 0), ('skarbiec', int(g[2])),
        ('skarbiec_zm', int(g[3]) if g[3] is not None else None),
        ('zwrot', int(g[4]) if g[4] is not None else None), ('zwrot_rodow', int(g[5]) if g[5] is not None else None),
        ('zwrot_nalezne', int(g[6]) if g[6] is not None else None),
        ('krol', g[7]), ('krol_kiesa', int(g[8])), ('krol_bilans', int(g[9])), ('rodow', int(g[10])),
        ('kiesy', int(g[11])), ('biednych', int(g[13])), ('na_minusie', int(g[14])), ('wojsko', int(g[15])),
    ])


def _agreg_skarbce(wiersze):
    r = OrderedDict()
    r['krolestw'] = len(wiersze)
    r['wojna'] = sum(w.get('wojna') or 0 for w in wiersze)
    r['skarbce'] = sum(w.get('skarbiec') or 0 for w in wiersze)
    zm = [w.get('skarbiec_zm') for w in wiersze if w.get('skarbiec_zm') is not None]
    r['skarbce_zm'] = sum(zm) if zm else None
    zw = [w.get('zwrot') for w in wiersze if w.get('zwrot') is not None]
    r['zwrot'] = sum(zw) if zw else None
    r['zwrot_krolestw'] = len(zw) if zw else None
    r['zwrot_niepelny'] = sum(1 for w in wiersze if w.get('zwrot_nalezne') is not None) if zw else None
    r['kiesy'] = sum(w.get('kiesy') or 0 for w in wiersze)
    r['rodow'] = sum(w.get('rodow') or 0 for w in wiersze)
    r['biednych'] = sum(w.get('biednych') or 0 for w in wiersze)
    r['na_minusie'] = sum(w.get('na_minusie') or 0 for w in wiersze)
    r['wojsko'] = sum(w.get('wojsko') or 0 for w in wiersze)
    naj = min(wiersze, key=lambda w: w.get('skarbiec') if w.get('skarbiec') is not None else 0)
    r['min_skarbiec'] = naj.get('skarbiec')
    r['min_krolestwo'] = naj.get('krolestwo')
    r['ujemnych'] = sum(1 for w in wiersze if (w.get('skarbiec') or 0) < 0)
    r['_nr'] = wiersze[0].get('_nr')
    return r


def _agreg_wojny(wiersze):
    return OrderedDict([('premii', len(wiersze)),
                        ('maks_proc', max((w.get('razem') or 0) for w in wiersze)), ('_nr', wiersze[0].get('_nr'))])


def _kol_pieniadz():
    k = []
    k += KD('razem', '| razem ', wym=True)
    k += KD('kasy_miast', 'osady: kasy miast ', wym=True)
    k += KD('kasy_zamkow', ', kasy zamkow ')
    k += KD('kiesy_wsi', ', kiesy wsi ')
    k += KD('kryjowki', ', kryjowki i inne ')
    k += KD('glowy_rodow', 'bohaterowie: glowy rodow ', wym=True)
    k += KD('lordowie', ', pozostali lordowie ')
    k += KD('gracz', ', gracz ')
    k += KD('notable', ', notable ')
    k += KD('wedrowcy', ', wedrowcy ')
    k += KD('inni', ', inni ')
    k += KD('skarbce', '| skarbce krolestw ')
    k += KD('warsztaty', '| kapital warsztatow ')
    k += KD('sakiewki', '| sakiewki ludzi ')
    k += KD('bank', '| Bank Zelazny ')
    k += KD('tabory_wsi', 'partie bez wodza: tabory wsi ')
    k += KD('karawany', ', karawany ')
    k += KD('garnizony', ', garnizony ')
    k += KD('bandy', ', bandy ')
    k += KD('inne_partie', ', inne ')
    k += KD('cla', 'cla miast i zamkow ')
    k += KD('podatek_wsi', ', podatek wsi ')
    return k


def _kol_kasy():
    def para(nazwa):
        return r'%s \+(-?\d+)/-(-?\d+)' % re.escape(nazwa)
    return [
        K('stan', '| stan ', wym=True),
        K('zmiana', ', zmiana ', 'sint', wym=True),
        K('taborom', 'zaplata taborom wsi ', 'sint'),
        K('z_utargu', 'z utargu taborow ', 'sint'),
        K('zakupy', '"zakupy" mieszkancow ', 'sint'),
        K('zakupy_tickow', rx=r'mieszkancow [+-]?\d+ \[P\] \((\d+) tickow osad', lit=[' tickow osad)']),
        K('regulator', 'regulator kasy ', 'sint'),
        K('dosypal', '(dosypal '),
        K('skasowal', ', skasowal '),
        K('reg_tickow', rx=r'skasowal -?\d+; (\d+) tickow', lit=[' tickow)']),
        K('przelewy', 'przelewy gry (GiveGoldAction) ', 'sint'),
        K('lordowie', rx=para('lordowie'), lit=['lordowie']),
        K('notable', rx=para('notable'), lit=['notable']),
        K('karawany', rx=para('karawany'), lit=['karawany']),
        K('poza_tickiem', 'Armoury poza tickiem dobowym ', 'sint'),
        K('zakupy_ai', rx=para('zakupy sprzetu AI'), lit=['zakupy sprzetu AI']),
        K('najemnicy', rx=para('najemnicy z karczmy'), lit=['najemnicy z karczmy']),
        K('warsztaty_zbr', rx=para('warsztaty zbrojne'), lit=['warsztaty zbrojne']),
        K('paser_sprzet', rx=para('paser band (sprzet dla band)'), lit=['paser band (sprzet dla band)']),
        K('zold_garnizonow', rx=para('zold garnizonow'), lit=['zold garnizonow']),
        K('zycie', rx=para('sakiewki ludzi - zycie w miastach'), lit=['sakiewki ludzi - zycie w miastach']),
        K('tick', 'Armoury tick dobowy ', 'sint'),
        K('renty', rx=r'[(,] ?renty ([+-]\d+)', lit=['renty']),
        K('budowy', rx=r'[(,] ?budowy ([+-]\d+)', lit=['budowy']),
        K('korona', 'korona (danina, clo, mennica) ', 'sint'),
        K('pozostale', 'pozostale moduly ticku ', 'sint'),
        K('paser_skup', 'paser band (skup lupu) ', 'sint'),
        K('zycie_band', 'bandy i kryjowki (zycie w miastach) ', 'sint'),
        K('reszta', rx=r'reszta - [^;|]*? ([+-]?\d+) \[R\]', wym=True,
          lit=['reszta - inne niezmierzone (i regulator, gdy jego licznik stoi na 0 tickow) ',
               'reszta - niezmierzone (m.in. podatek gry od zakupow we wsi) ']),
        K('osad', 'rozklad [P]: osad '),
        K('min', ', min '),
        K('mediana', ', mediana '),
        K('max', ', max '),
        K('ponizej_1000', ', ponizej 1000 zlota '),
    ]


SEK_ZOLN = 'zolnierze: '
SEK_DZIS = 'dzis: zabici '
# sekcja zoldu w "Przeplywy osad:": uklad wpisu 102 i uklad 102b (ten sam licznik, inny naglowek)
SEK_ZOLD = ('zold wyplacony [P]', 'zold naliczony przy rozliczeniach rodow [P]')
SEK_HZ = 'hearth za ludzi dzis ('
SEK_ROZL = 'hearth wsi swiata '

TEMATY = [
    # ------------------------------------------------------------------------------------------------ surowce
    Temat('Ruda', 'Ruda', r'^dzien \d+', _kol_ksiegi('kopalnie miast i zamkow'), _extra_ksiegi, SKROT_KSIEGI, 'surowce',
          'OreLedger.cs', bezwar=True),
    Temat('Drewno', 'Drewno', r'^dzien \d+', _kol_ksiegi('miasta i zamki'), _extra_ksiegi, SKROT_KSIEGI, 'surowce',
          'OreLedger.cs', bezwar=True),
    Temat('Dowoz', 'Dowoz', r'^dzien \d+', [
        K('wyslane', 'wyslane na targ miasta ', wym=True),
        K('brak_targu', 'do zamku: brak targu '),
        K('za_daleko', ', targ za daleko '),
        K('wojna', ', oblezenie albo wojna '),
        K('w_drodze', '); w drodze teraz ', wym=True),
        K('do_miasta', ': do miasta '),
        K('do_zamku', ', do zamku '),
        K('rozbite', 'rozbite tabory wsi (wszystkich) '),
        K('przez_bandy', ', w tym przez bandy '),
        K('wsi_zamkowych', '; wsi zamkowych '),
        K('targ_wlasny', 'targ we wlasnym krolestwie '),
        K('targ_obcy', ', w obcym '),
        K('bez_targu', ', bez targu '),
    ], _extra_dowoz, [('tabory: wyslane/w drodze/rozbite', '{wyslane}/{w_drodze}/{rozbite}')],
          'surowce', 'MarketRoad.cs', bezwar=True),
    Temat('Dowoz (skutki)', 'Dowoz (skutki)', r'^dzien \d+', [
        K('zatk_zamkowe', 'zatkane magazyny: wsie zamkowe ', wym=True),
        K('wsi_zamkowych', rx=r'wsie zamkowe \d+ z (\d+)', lit=['zatkane magazyny: wsie zamkowe ']),
        K('zatk_miejskie', ', wsie miejskie ', wym=True),
        K('wsi_miejskich', rx=r'wsie miejskie \d+ z (\d+)', lit=[', wsie miejskie ']),
        K('zamki_glodne', 'zamki: bilans zywnosci ujemny '),
        K('bez_polki_ujemny', ', bez polki ujemny '),
        K('ratuje_polka', '(dzis ratuje polka '),
        K('zywnosc_z_polki', 'zywnosc z polki razem ', 'float'),
        K('pusty_spichlerz', ', pusty spichlerz '),
        K('kasy_miast', '; kasy miast razem ', wym=True),
        K('ponizej_progu', ', ponizej progu rent '),
    ], _extra_skutki, [('zatkane magazyny %: zamkowe/miejskie', '{zatk_zamkowe_proc}/{zatk_miejskie_proc}'),
                       ('kasy miast | ponizej progu rent', '{kasy_miast} | {ponizej_progu}')],
          'surowce', 'MarketRoad.cs', bezwar=True),
    Temat('Karawany', 'Karawany', r'^dzien \d+', [
        K('wjazdy', 'wjazdy karawan do miast ', wym=True),
        K('ze_sprzedaza', ', w tym ze sprzedaza surowcow '),
        K('miasta_zaplacily', ' (miasta zaplacily '),
        K('wyjazdy', '; wyjazdy z miast ', wym=True),
        K('z_zakupem', ', w tym z zakupem '),
        K('karawany_zaplacily', ' (karawany zaplacily '),
        K('zwrot_gabki', ' (zwrot '),
        K('odp_w_drodze_dosc', 'zakup odpuszczony: w drodze dosc '),
        K('odp_cena', ', cena nie nizsza od sredniej '),
        K('odp_bez_zysku', ', bez zysku wobec ceny zbytu '),
        K('odp_brak_miejsca', ', brak miejsca w jukach '),
        K('odp_sprzedane_tu', ', sprzedane tu w tej wizycie '),
        K('odp_rozkaz', ', rozkaz gracza (tylko zywnosc) '),
        K('wstrzymana_rezerwa', '; sprzedaz wstrzymana rezerwa kasy miasta '),
        K('bk_wyzerowane', '; wyceny zakupu BK wyzerowane '),
        K('zysk_razem', rx=r'minus zaplacone\): .*? \(razem (-?\d+) d\)', lit=[' (razem ', ' d)']),
    ], _extra_karawany, [
        ('wjazdy/sprzedaz | wyjazdy/zakup', '{wjazdy}/{ze_sprzedaza}|{wyjazdy}/{z_zakupem}'),
        ('sprzedane (ruda) | kupione (ruda)', '{sprz_szt}({sprz_ruda})|{kup_szt}({kup_ruda})'),
        ('zaplacily: miasta / karawany', '{miasta_zaplacily}/{karawany_zaplacily}'),
        ('odpuszczone: dosc w drodze/cena/juki', '{odp_w_drodze_dosc}/{odp_cena}/{odp_brak_miejsca}'),
        ('bez zysku | oczek. zysk d | zaczely od', '{odp_bez_zysku}|{zysk_razem}|{first_naj}'),
    ], 'surowce', 'CaravanBulk.cs'),
    Temat('Karawany (stan)', 'Karawany (stan)', r'^dzien \d+', [
        K('miast', ' - miast ', wym=True),
        K('karawan', ', karawan '),
    ], _extra_stan_karawan, [
        ('ruda: miast ponizej zapasu/bez towaru', '{ruda_ponizej}/{ruda_bez}'),
        ('ruda: brakuje / w jukach karawan', '{ruda_brak}/{ruda_juki}'),
        ('ruda: cena zbytu d (kupuje miast)', '{ruda_cena} ({ruda_kupuje})'),
        ('drewno: ponizej/bez | brakuje/w jukach', '{drewno_ponizej}/{drewno_bez}|{drewno_brak}/{drewno_juki}'),
        ('KUPNO STOI (surowce)', '{kupno_stoi}'),
    ], 'surowce', 'CaravanBulk.cs'),
    Temat('Warsztaty', 'Warsztaty', r'^dzien \d+', [
        K('wykonano', ' - wykonano ', wym=True),
        K('koszt', '], koszt '),
        K('sprzedaz', ', sprzedaz '),
        K('bez_zysku', '; odpuszczone: bez zysku '),
        K('brak_surowca', ', brak surowca ', wym=True),
        K('bs_ruda', ' [ruda '),
        K('bs_drewno', rx=r'brak surowca \d+ \[ruda \d+, drewno (\d+)', lit=[', drewno ']),
        K('bs_skora', rx=r'brak surowca \d+ \[[^\]]*?, skora (\d+)', lit=[', skora ']),
        K('bs_len', ', len/welna '),
        K('w_robocie', 'w robocie (cykle) '),
        K('brak_zlota', ', brak zlota/kupca '),
        K('rozpoczete', '; rozpoczete sztuki '),
        K('w_toku', ', w toku teraz '),
        K('skor', 'rzemieslnicy miasta wygarbowali skor '),
        K('plotna', ', utkali plotna '),
        K('z_niczego_cykle', 'z niczego zablokowane: cykle rzemieslnikow '),
        K('losowanie', ', sztabki/wegiel z losowania -> ruda/drewno '),
    ], None, [
        ('wykonano szt | koszt/sprzedaz', '{wykonano}|{koszt}/{sprzedaz}'),
        ('odpuszczone: brak surowca | w robocie', '{brak_surowca} | {w_robocie}'),
        ('brak wg surowca: ruda/drewno/skora/len', '{bs_ruda}/{bs_drewno}/{bs_skora}/{bs_len}'),
    ], 'surowce', 'WorkshopLaw.cs'),
    Temat('Rynek surowcow', 'Rynek surowcow', r'^zapasy na targach miast', [], _extra_rynek_surowcow, [
        ('targi miast: ruda/drewno', '{iron}/{hardwood}'),
        ('targi miast: skora/skory surowe', '{leather}/{hides}'),
        ('targi miast: plotno/len/welna', '{linen}/{flax}/{wool}'),
    ], 'surowce', 'ArmsPricing.cs'),
    Temat('Rynek broni', 'Rynek broni', r'^na polkach', [
        K('na_polkach', 'na polkach ', wym=True),
    ], _extra_rynek_broni, [
        ('bron na polkach | indeks ruda/drewno', '{na_polkach}|{idx_ruda_med}/{idx_drewno_med}'),
    ], 'surowce', 'ArmsPricing.cs'),
    Temat('Mineraly (dubel BK)', 'Mineraly (dubel BK)', r'^dzien \d+', [
        K('razem', '; razem '),
        K('wywolan', ' zdjetych wpisow w '),
        K('wsi_mnoznik', '; wsi z mnoznikiem modelu teraz '),
        K('potkniecia', '; potkniecia od wczytania kampanii ', wym=True),
    ], _extra_mineraly, [
        ('wsie z dublem: iron/salt/clay/silver', '{iron}/{salt}/{clay}/{silver}'),
        ('zdjetych wpisow|stan|wsi z mnoznikiem', '{razem}|{stan}|{wsi_mnoznik}'),
    ], 'surowce', 'MineralOnce.cs'),
    # ------------------------------------------------------------------------------------------------ pieniadz
    Temat('Pieniadz swiata', 'Pieniadz swiata', r'^dzien \d+', _kol_pieniadz(), None, [
        ('zloto swiata razem (zmiana dobowa)', '{razem} ({razem_zm:+})'),
        ('kasy miast / kasy zamkow', '{kasy_miast}/{kasy_zamkow}'),
        ('kiesy wsi / kryjowki i inne', '{kiesy_wsi}/{kryjowki}'),
        ('glowy rodow / pozostali lordowie', '{glowy_rodow}/{lordowie}'),
        ('notable / kapital warsztatow', '{notable}/{warsztaty}'),
        ('skarbce krolestw / Bank Zelazny', '{skarbce}/{bank}'),
        ('sakiewki ludzi / kiesy band', '{sakiewki}/{bandy}'),
    ], 'pieniadz', 'MoneyLedger.cs', bezwar=True),
    Temat('Pieniadz swiata (bilans)', 'Pieniadz swiata (bilans)', r'^dzien \d+', [
        K('zmiana', '| zmiana sumy ', 'sint', wym=True),
        K('zrodla', rx=r'zmierzone zrodla z niczego \+(-?\d+)', wym=True, lit=['zmierzone zrodla z niczego +']),
        K('zakupy', 'mieszkancow miast i zamkow '),
        K('dosypal', ', regulator kas dosypal '),
        # uklad 102b: rozliczenia rodow zmierzone stanem zlota swiata (zrodlo "na plus", ujscie "na minus")
        K('rody_plus', ', rozliczenia rodow na plus '),
        K('rody_plus_n', rx=r'rozliczenia rodow na plus -?\d+ w (\d+) rodach', lit=[' w ', ' rodach, ']),
        # ogniwo 107 na ukladzie 102b: zold, ktory SoldierPay oddal do obiegu, jest osobnym zrodlem
        K('oddany', 'zold oddany do obiegu przez SoldierPay '),
        K('oddany_sakiewki', ' (sakiewki ludzi '),
        K('oddany_kasy', ', kasy osad '),
        K('z_niczego', rx=r', GiveGoldAction z niczego (?:poza rozliczeniami rodow )?(-?\d+)',
          lit=['), GiveGoldAction z niczego poza rozliczeniami rodow ']),
        K('ujscia', ' - zmierzone ujscia w nicosc ', wym=True),
        K('rody_minus', '(rozliczenia rodow na minus '),
        K('rody_minus_n', rx=r'rozliczenia rodow na minus -?\d+ w (\d+) rodach', lit=[' rodach, regulator kas skasowal ']),
        # zold: uklad 102 "(zold N, regulator kas skasowal", uklad 102b " W tym zold naliczony N [P]" na koncu linii
        K('zold', rx=r'(?:\(zold (?:naliczony )?|W tym zold naliczony )(-?\d+)', lit=[' W tym zold naliczony ']),
        K('skasowal', 'regulator kas skasowal '),
        K('zniklo', ', z utargu wsi zniklo '),
        K('w_nicosc', rx=r', GiveGoldAction w nicosc (?:poza rozliczeniami rodow )?(-?\d+)',
          lit=[', GiveGoldAction w nicosc poza rozliczeniami rodow ']),
        K('levy_oddal', rx=r'w nicosc (?:poza rozliczeniami rodow )?-?\d+ minus (-?\d+) oddane',
          lit=[' minus ', ' oddane przez LevyGold notablom i miastom)']),
        K('reszta', ' + reszta ', 'sint', wym=True),
    ], _extra_bilans, [
        ('zmiana sumy | reszta [R]', '{zmiana:+}|{reszta:+}'),
        ('zmierzone zrodla / ujscia', '{zrodla}/{ujscia}'),
        ('reszta jako % ruchu (zrodla + ujscia)', '{reszta_proc}'),
        ('zrodla: zakupy mieszkancow/regulator', '{zakupy}/{dosypal}'),
        ('zold naliczony | regulator skasowal', '{zold}|{skasowal}'),
        ('z niczego|utarg wsi zniklo|w nicosc', '{z_niczego}|{zniklo}|{w_nicosc}'),
        ('rozliczenia rodow: na plus/na minus', '{rody_plus}/{rody_minus}'),
        ('zold oddany do obiegu (SoldierPay)', '{oddany}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Pieniadz swiata (rody)', 'Pieniadz swiata (rody)', r'^dzien \d+', [
        K('zmiana', 'zmienily zloto swiata o ', 'sint'),
        K('rozliczen', rx=r'\[P\] w (\d+) rozliczeniach', lit=[' [P] w ', ' rozliczeniach: ']),
        K('na_plus_n', rx=r'rozliczeniach: (\d+) na plus', lit=[' rozliczeniach: ', ' na plus +']),
        K('na_plus', ' na plus +'),
        K('na_minus_n', rx=r', (\d+) na minus ', lit=[' na minus ']),
        K('na_minus', rx=r', \d+ na minus (-?\d+)', lit=[' na minus ']),
        K('bez_zmiany', rx=r', (\d+) bez zmiany',
          lit=[' bez zmiany (pomiar: suma wszystkich posiadaczy tuz przed i tuz po kazdym rozliczeniu)']),
        K('saldo_plus', ' | z tego saldo dopisane glowom rodow +'),
        K('saldo_plus_n', rx=r'saldo dopisane glowom rodow \+-?\d+ \((\d+) rodow\)', lit=[' rodow) / ']),
        K('saldo_minus', ' rodow) / '),
        K('saldo_minus_n', rx=r' rodow\) / -?\d+ \((\d+) rodow\)',
          lit=[' rodow) [P] (kwota ze zdarzenia gry - z pustej kiesy schodzi mniej), zmiany poza saldem ']),
        K('poza_saldem', ', zmiany poza saldem ', 'sint'),
        K('zold', ' | zold naliczony '),
        K('bez_zoldu', ' [P] jest czescia tych rozliczen; bez niego zmienilyby zloto swiata o ', 'sint'),
        K('oddal', ' Z zoldu SoldierPay oddal do obiegu '),
        K('inne_z_niczego', ' Inne GiveGoldAction w trakcie rozliczen (sa w zmianie zlota swiata): z niczego +'),
        K('inne_w_nicosc', rx=r'w trakcie rozliczen \(sa w zmianie zlota swiata\): z niczego \+-?\d+, w nicosc (-?\d+)',
          lit=[', w nicosc ']),
        K('niedomkniete', ' Rozliczenia niedomkniete (wyjatek w kodzie gry albo moda): '),
    ], _extra_rody, [
        ('rozliczenia rodow: zmiana zlota (ile)', '{zmiana:+} ({rozliczen})'),
        ('saldo glowom rodow: na plus/na minus', '{saldo_plus}/{saldo_minus}'),
        ('poza saldem | bez zoldu byloby', '{poza_saldem:+}|{bez_zoldu:+}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Przeplywy osad', 'Przeplywy osad', r'^dzien \d+ \| utarg', [
        K('miasta_zaplacily', 'utarg taborow wsi [P]: miasta zaplacily ', wym=True),
        K('miasta_wizyt', rx=r'miasta zaplacily -?\d+ \((\d+) wizyt', lit=[' wizyt), zamki ']),
        K('zamki_zaplacily', ' wizyt), zamki '),
        K('zamki_wizyt', rx=r' wizyt\), zamki -?\d+ \((\d+) wizyt', lit=[' wizyt); towar niesprzedany po wizycie: w miastach ']),
        K('niesprz_miasta', 'towar niesprzedany po wizycie: w miastach '),
        K('niesprz_zamki', ' wizyt), w zamkach '),
        K('oddaly', 'powrot do wsi: tabory oddaly ', wym=True),
        K('powrotow', rx=r'\[P\] w (\d+) powrotach', lit=[' powrotach = pan (licznik podatku wsi) ']),
        K('pan', 'pan (licznik podatku wsi) '),
        K('majatki_bk', '+ wlasciciele majatkow BK '),
        K('kiesa_wsi', '+ kiesa wsi '),
        K('zniklo', '+ zniklo ', 'sint'),
        K('zniklo_proc', rx=r'\+ zniklo -?\d+ \[R\] \((-?[\d.,]+)%', lit=[') + zniklo ']),
        K('zold_rody', sek=SEK_ZOLD, rx=r'partie rodow (-?\d+) \(', lit=['partie rodow', ' partii, z niedoplata ']),
        K('zold_garnizony', sek=SEK_ZOLD, rx=r', garnizony (-?\d+) \(', lit=['garnizony']),
        K('zold_karawany', sek=SEK_ZOLD, rx=r', karawany (-?\d+) \(', lit=['karawany']),
        K('zold_inne', sek=SEK_ZOLD, rx=r', inne (-?\d+) \(', lit=['inne']),
        K('niedoplata_rody', sek=SEK_ZOLD, rx=r'partie rodow -?\d+ \(\d+ partii, z niedoplata (\d+)\)',
          lit=[' partii, z niedoplata ']),
        K('zold_razem', ', razem ', sek=SEK_ZOLD, wym=True,
          lit=[' | zold naliczony przy rozliczeniach rodow [P]: ', ', razem ']),
        K('do_sakiewek', '; z tego przekazano do sakiewek ludzi '),
        K('do_kas', ', do kas osad '),
    ], _extra_przeplywy, [
        ('utarg taborow wsi: miasta/zamki', '{miasta_zaplacily}/{zamki_zaplacily}'),
        ('tabory oddaly we wsi | zniklo [R]', '{oddaly}|{zniklo:+}'),
        ('zold: partie rodow/garnizony', '{zold_rody}/{zold_garnizony}'),
        ('zold przekazany: sakiewki/kasy osad', '{do_sakiewek}/{do_kas}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Przeplywy osad (modele)', 'Przeplywy osad', r'^modele czynne', [], None, [], 'pieniadz', 'MoneyLedger.cs',
          dzienny=False),
    Temat('Przeplywy osad (kasy miast)', 'Przeplywy osad (kasy miast)', r'^dzien \d+', _kol_kasy(), _extra_kasy, [
        ('stan (zmiana)', '{stan} ({zmiana:+})'),
        ('regulator kasy | reszta [R]', '{regulator:+}|{reszta:+}'),
        ('renty / zaplata taborom wsi', '{renty:+}/{taborom:+}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Przeplywy osad (kasy zamkow)', 'Przeplywy osad (kasy zamkow)', r'^dzien \d+', _kol_kasy(), _extra_kasy, [
        ('stan (zmiana)', '{stan} ({zmiana:+})'),
        ('regulator kasy | reszta [R]', '{regulator:+}|{reszta:+}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Przeplywy osad (kiesy wsi)', 'Przeplywy osad (kiesy wsi)', r'^dzien \d+', _kol_kasy(), _extra_kasy, [
        ('stan (zmiana)', '{stan} ({zmiana:+})'),
        ('z utargu taborow | reszta [R]', '{z_utargu:+}|{reszta:+}'),
    ], 'pieniadz', 'MoneyLedger.cs'),
    Temat('Korona (powinnosci)', 'Korona', r'^dzien \d+ - powinnosci wasali', [
        K('powinnosci', ' - powinnosci wasali ', wym=True),
        K('rodow', ' zl od '),
        K('gracz', ' (rod gracza '),
    ], None, [('powinnosci wasali (rodow)', '{powinnosci} ({rodow})')], 'pieniadz', 'KingdomTreasury.cs'),
    Temat('Korona (danina i clo)', 'Korona', r'^dzien \d+ - danina wojenna', [
        K('danina', ' - danina wojenna z kas osad ', wym=True),
        K('clo', ', clo od handlu miast '),
        K('mennica', ' (do skarbcow krolestw); mennica '),
        K('monopole', ', monopole '),
    ], None, [('danina wojenna/clo | mennica/monopole', '{danina}/{clo}|{mennica}/{monopole}')],
          'pieniadz', 'KingdomTreasury.cs'),
    Temat('Korona (zwrot zoldu)', 'Korona', r'^dzien \d+ - zwrot zoldu', [
        K('zwrot', ' - zwrot zoldu ze skarbcow krolestw w wojnie: ', wym=True),
        K('rodow', ' zl dla '),
        K('krolestw', rx=r' rodow w (\d+) krolestwach', lit=[' rodow w ', ' krolestwach (']),
        K('procent', rx=r'krolestwach \((-?[\d.,]+)% z ', lit=['% z ']),
        K('zaplacony', rx=r'% z (-?\d+) zaplaconego zoldu', lit=[' zaplaconego zoldu = nalezne ']),
        K('nalezne', ' zaplaconego zoldu = nalezne '),
        K('nie_mial', '); skarbiec nie mial dosc w '),
        K('pusty', ' krolestwach (w tym pusty: '),
        K('niedoplata', '), niedoplata '),
        K('zostalo', '; w skarbcach tych krolestw zostalo '),
        K('gracz', '; rod gracza dostal '),
        K('dluznym', '; z tego '),
        K('dluznych_rodow', rx=r'; z tego -?\d+ dla (\d+) rodow dluznych', lit=[' rodow dluznych koronie (']),
        K('bez_pokoj', 'Bez zwrotu: zold krolestw w pokoju '),
        K('bez_krolestwa', ', rodow bez krolestwa '),
        K('bez_najemnikow', ', najemnikow '),
        K('potkniecia', ' Potkniecia: wyjatek przy '),
    ], None, [
        ('zwrot zoldu / nalezne', '{zwrot}/{nalezne}'),
        ('rodow/krolestw|brak w skarbcu (pusty)', '{rodow}/{krolestw}|{nie_mial}({pusty})'),
    ], 'pieniadz', 'KingdomTreasury.cs'),
    Temat('Skarbce', 'Skarbce', r'^dzien \d+', [], _extra_skarbiec, [
        ('krolestw (wojna) | na minusie/biedne', '{krolestw}({wojna})|{na_minusie}/{biednych}'),
        ('skarbce razem (zmiana dobowa)', '{skarbce} ({skarbce_zm:+})'),
        ('kiesy rodow | wojsko rodow', '{kiesy}|{wojsko}'),
        ('zwrot zoldu (krolestw/niepelnych)', '{zwrot} ({zwrot_krolestw}/{zwrot_niepelny})'),
    ], 'pieniadz', 'KingdomLedger.cs', agreg=_agreg_skarbce, bezwar=True, wielo=True),
    Temat('Zold', 'Zold', r'^dzien \d+', [
        K('rody_naliczony', '| partie rodow: naliczony ', wym=True),
        K('rody_zeszlo', ', z kies zeszlo ', sek='partie rodow: naliczony'),
        K('rody_partii', sek='partie rodow: naliczony', rx=r'z kies zeszlo -?\d+ \((\d+) partii\)',
          lit=[' partii) -> do sakiewek ludzi ']),
        K('do_sakiewek', ' -> do sakiewek ludzi ', wym=True),
        K('do_sakiewek_partii', rx=r'do sakiewek ludzi -?\d+ \((\d+) partii', lit=[' partii, w tym ludzie gracza ']),
        K('gracz', ', w tym ludzie gracza '),
        K('gar_naliczony', '| garnizony: naliczony ', wym=True),
        K('gar_zeszlo', ', z kies zeszlo ', sek='garnizony: naliczony'),
        K('gar_zalog', sek='garnizony: naliczony', rx=r'\((\d+) zalog\)', lit=[' zalog) -> do kas miast ']),
        K('do_miast', ' -> do kas miast '),
        K('do_zamkow', '), do kas zamkow '),
        K('nieumarli', 'nie przekazano: nieumarli '),
        K('bez_osady', ', zaloga bez osady '),
        K('wylaczone', ', wylaczone w ustawieniach '),
        K('inne_partie', '; karawany i inne partie (bez zmian) '),
        K('przyciete', 'saldo rodu nie zmiescilo sie w kiesie glowy: '),
        K('przyciete_rodow', rx=r'w kiesie glowy: -?\d+ w (\d+) rodach',
          lit=[' rodach (w tym brak zapisany przez gre jako dlug wobec korony: ']),
        K('dlug', 'jako dlug wobec korony: '),
        K('dlug_rodow', rx=r'dlug wobec korony: -?\d+ w (\d+) rodach',
          lit=[' rodach); rody z pusta kiesa i nieznanym saldem (nic nie przekazano): ']),
        K('slepe', 'nieznanym saldem (nic nie przekazano): '),
        K('slepe_zl', rx=r'\(nic nie przekazano\): \d+ \((-?\d+)\)', lit=['(nic nie przekazano): ']),
        K('sak_razem', '| sakiewki ludzi: razem ', wym=True),
        K('sak_partii', rx=r'sakiewki ludzi: razem -?\d+ w (\d+) partiach', lit=[' partiach, najwieksza ']),
        K('sak_najwieksza', ' partiach, najwieksza '),
        K('sak_sieroty', ', po partiach, ktorych juz nie ma: '),
        K('sak_sieroty_n', rx=r'ktorych juz nie ma: -?\d+ \((\d+)\)', lit=[', po partiach, ktorych juz nie ma: ']),
        K('znacznik', 'wlaczona, znacznik '),
        K('nie_skasowal', ' miastach, regulator nie skasowal dzis '),
        K('potk_powtorzone', '| potkniecia: powtorzone wyplaty '),
        K('potk_wyjatki', ', wyjatki ', sek='potkniecia: powtorzone wyplaty'),
    ], _extra_zold, [
        ('partie rodow: naliczony/zeszlo z kies', '{rody_naliczony}/{rody_zeszlo}'),
        ('do sakiewek ludzi (w tym gracz)', '{do_sakiewek} ({gracz})'),
        ('zalogi: naliczony/zeszlo z kies', '{gar_naliczony}/{gar_zeszlo}'),
        ('do kas miast / do kas zamkow', '{do_miast}/{do_zamkow}'),
        ('przyciete (dlug korony) | rody slepe', '{przyciete}({dlug})|{slepe}'),
        ('sakiewki: razem / najwieksza', '{sak_razem}/{sak_najwieksza}'),
        ('sakiewki po zniklych partiach (ile)', '{sak_sieroty} ({sak_sieroty_n})'),
    ], 'pieniadz', 'SoldierPay.cs'),
    Temat('Sakiewka ludzi', 'Sakiewka ludzi', r'^dzien \d+ - nadwyzki', [
        K('sprzedane', ' - nadwyzki sprzedane ', wym=True),
        K('za', ' szt. za '),
        K('trzecia_lordow', ' (trzecia lordow AI '),
        K('na_sprzet', '), ludzie wydali na sprzet ', wym=True),
        K('na_zycie', ', na zycie w miastach '),
        K('zold', '; zold wplacony do sakiewek '),
        K('wyplat', rx=r'zold wplacony do sakiewek -?\d+ \((\d+) wyplat', lit=[' wyplat); ruch sakiewek: wplynelo ']),
        K('wplynelo', ' wyplat); ruch sakiewek: wplynelo '),
        K('wyszlo', ' (zold, lup, przejete sakiewki), wyszlo '),
        K('razem', ', w sakiewkach razem '),
    ], None, [
        ('nadwyzki sprzedane: szt/zl', '{sprzedane}/{za}'),
        ('wydane: na sprzet / na zycie', '{na_sprzet}/{na_zycie}'),
        ('zold wplacony | w sakiewkach razem', '{zold}|{razem}'),
        ('ruch sakiewek: wplynelo/wyszlo', '{wplynelo}/{wyszlo}'),
    ], 'pieniadz', 'MenPurse.cs'),
    Temat('Sakiewka ludzi (gracz)', 'Sakiewka ludzi', r'^gracz ', [], None, [], 'pieniadz', 'MenPurse.cs', dzienny=False),
    Temat('Paser', 'Paser', r'^dzien \d+', [
        # skup lupu: kazda banda z ladunkiem ma miec pasera (promien 200, cena maleje z odlegloscia)
        K('udzial', ' | skup lupu (udzial bandy ', 'float'),
        K('udzial_daleko', ' przy miescie -> ', 'float'),
        K('promien', ' na granicy promienia '),
        K('z_ladunkiem', '): band z ladunkiem '),
        K('z_paserem', ', z paserem '),
        K('poza_promieniem', ' (ma byc 100%; w tym poza promieniem '),
        K('sprzedalo', '), sprzedalo '),
        K('w_miastach', rx=r'\), sprzedalo -?\d+ w (\d+) miastach', lit=[' miastach - ']),
        K('szt', ' miastach - '),
        K('ruda', ' szt. (w tym ruda '),
        K('drewno', ', drewno ', sek='skup lupu ('),
        K('zywnosc', ' ladunkow, zywnosc '),
        K('zwierzeta', ', zwierzeta ponad potrzebe '),
        K('za_zl', ') za ', sek='skup lupu ('),
        K('po_cenach', ' zl; po cenach skupu miast '),
        K('dzialka', ' zl, dzialka pasera '),
        K('same_zwierzeta', '; same zwierzeta ponad potrzebe (bez innego ladunku) sprzedalo '),
        K('strefa1_szt', ' | wg odleglosci od miasta, ktore kupilo: do 50 - '),
        K('strefa1_zl', rx=r'do 50 - -?\d+ szt\. za (-?\d+) zl', lit=[' szt. za ']),
        K('strefa2_szt', ' zl, 50-100 - '),
        K('strefa2_zl', rx=r' zl, 50-100 - -?\d+ szt\. za (-?\d+) zl', lit=[' szt. za ']),
        K('strefa3_szt', ' zl, dalej - '),
        K('strefa3_zl', rx=r' zl, dalej - -?\d+ szt\. za (-?\d+) zl', lit=[' szt. za ']),
        K('zostalo', ' | z ladunkiem zostalo '),
        K('zostalo_szt', rx=r'z ladunkiem zostalo -?\d+ band \((-?\d+) szt', lit=[' band (']),
        K('zost_rezerwa', ' szt. razem z bandami poza handlem): kasy miast w zasiegu przy rezerwie '),
        K('zost_brak_miasta', ', brak otwartego miasta '),
        K('poza_bitwa', '; poza handlem z ladunkiem: w bitwie '),
        K('poza_bez_kiesy', ', bez kiesy '),
        K('poza_zadanie', ', zajete przez zadanie '),
        K('poza_nieumarli', ', nieumarli '),
        K('kupno_awansow', ' | kupno sprzetu u pasera: ', wym=True),
        K('kupno_zl', ' awansow za '),
        K('kupno_z_kryjowek', ' zl (w tym z kas kryjowek '),
        K('zycie_bandy', ' | zycie w miastach: bandy wydaly '),
        K('zycie_kryjowki', ' zl, kryjowki z nadwyzki '),
        K('wejsc', ' | kryjowki: wejsc band ', wym=True),
        K('zabl_bandom', rx=r'(?:zloto z niczego zablokowane - bandom|gra dopisala z niczego bandom) (-?\d+) zl',
          lit=[', zloto z niczego zablokowane - bandom ', ', KUREK OTWARTY w ustawieniach - gra dopisala z niczego bandom ']),
        K('zabl_kryjowkom', ' zl, kryjowkom ', sek='kryjowki: wejsc band'),
        K('kryj_doplyw', '; kasy kryjowek: doplyw '),
        K('kryj_odplyw', ' zl (bandy odlozyly z wlasnych kies), odplyw '),
        K('start_dane', ' zl (kiesy startowe nowych band '),
        K('start_chciane', rx=r'kiesy startowe nowych band -?\d+ z (-?\d+) zl', lit=[' z ']),
        K('kryj_sprzet', ' zl, na ktore liczyly; sprzet '),
        K('kryj_zycie', '; zycie w miastach '),
        K('kasy_kryjowek', '); stan '),
        K('kryjowek', rx=r'\); stan -?\d+ zl w (\d+) kryjowkach', lit=[' zl w ']),
        K('kryj_najwieksza', ' kryjowkach, najwieksza '),
        K('skarbce_do', ' zl, skarbce (to, co zostaje dla zdobywcy) razem do '),
        K('start_z_niczego', '; OBIEG KAS WYLACZONY - kiesy startowe z niczego: '),
        K('zywn_zabl', rx=r'zywnosc z niczego: zablokowana (-?\d+)', lit=[' | zywnosc z niczego: ', 'zablokowana ']),
        K('zywn_zdjete', ' nowym bandom, w bitwie zdjete '),
        K('gone_rozbitych', ' | kiesy band, ktore dzis zniknely z mapy: rozbitych '),
        K('gone_rozwiazanych', ' zl (reszta po dzialce zwyciezcy - przepada), rozwiazanych '),
        K('kiesy_band', ' | stan: kiesy band ', wym=True),
        K('juki_ladunek', ' zl; w jukach band: ladunek na sprzedaz '),
        K('juki_ladunek_zl', rx=r'ladunek na sprzedaz -?\d+ szt\. \((-?\d+)', lit=[' szt. (']),
        K('juki_zywnosc', ' zl), zywnosc poza skupem '),
        K('juki_zbroje', ' zl), zbroje na awanse '),
        K('juki_konie', ' zl), konie pod siodlo '),
        K('juki_juczne', ' zl), juczne '),
        K('juki_bron', ' zl), bron i inny sprzet - nie na sprzedaz '),
        K('juki_niehandlowe', ' zl), niehandlowe '),
        K('potk_skup', ' | potkniecia: skup '),
        K('potk_kryjowki', ', kryjowki ', sek='potkniecia: skup'),
        K('potk_zycie', ', zycie ', sek='potkniecia: skup'),
        K('potk_awanse', ', awanse ', sek='potkniecia: skup'),
    ], _extra_paser, [
        ('bandy: z ladunkiem/z paserem/sprzedalo', '{z_ladunkiem}/{z_paserem}/{sprzedalo}'),
        ('z ladunkiem zostalo band (szt.)', '{zostalo} ({zostalo_szt})'),
        ('skup: szt | zaplacono zl | dzialka', '{szt}|{za_zl}|{dzialka}'),
        ('skup szt wg drogi: do 50/50-100/dalej', '{strefa1_szt}/{strefa2_szt}/{strefa3_szt}'),
        ('zycie w miastach zl | awanse u pasera', '{zycie_razem}|{kupno_awansow}'),
        ('kryjowki: wejsc | kasy doplyw/odplyw', '{wejsc}|{kryj_doplyw}/{kryj_odplyw}'),
        ('kiesy band / kasy kryjowek (ile)', '{kiesy_band}/{kasy_kryjowek} ({kryjowek})'),
        ('juki band: ladunek/zywnosc poza skupem', '{juki_ladunek}/{juki_zywnosc}'),
    ], 'pieniadz', 'OutlawLaw.cs'),
    Temat('IronBank', 'IronBank', r'^dzien \d+ - nowe pozyczki', [
        K('nowe', ' - nowe pozyczki ', wym=True),
        K('nowe_suma', rx=r'nowe pozyczki \d+ \((-?\d+)\)', lit=['), splaty ']),
        K('splaty', '), splaty '),
        K('spoznienia', '), spoznienia '),
        K('bankructwa', ', bankructwa dzis '),
        K('dluznikow', '; dluznikow '),
        K('bankrutow', ' (bankrutow '),
        K('dlug', '), dlug razem '),
        K('kapital', ', kapital Banku ', wym=True),
    ], None, [('dluznikow | dlug razem | kapital Banku', '{dluznikow}|{dlug}|{kapital}')],
          'pieniadz', 'IronBank.cs'),
    Temat('IronBank (pozyczki)', 'IronBank', r'pozycza ', [], None, [], 'pieniadz', 'IronBank.cs', dzienny=False),
    # ------------------------------------------------------------------------------------------------ ludzie
    Temat('Ludzie', 'Ludzie', r'^dzien \d+ \| swiat', [
        K('ludnosc_mln', rx=r'swiat: ludnosc (-?\d+(?:[.,]\d+)?) mln', lit=[' | swiat: ', 'ludnosc ', ' mln']),
        K('ludnosc_zm', rx=r' mln \(([+-]\d+) ludzi\)', lit=[' ludzi)']),
        K('wsie_mln', ' [PopulationLaw: wsie ', 'float'),
        K('miasta_mln', rx=r'\[PopulationLaw: wsie [\d.,]+, miasta ' + RX_F + r'\]', lit=[', miasta ']),
        K('mezczyzn_mln', '], mezczyzn 16-60 ok. ', 'float'),
        K('rody', sek=SEK_ZOLN, rx=r'partie rodow (-?\d+) \(', wym=True, lit=[' | zolnierze: ', 'partie rodow', ' partii']),
        K('rody_partii', sek=SEK_ZOLN, rx=r'partie rodow -?\d+ \((\d+) partii', lit=[' partii']),
        K('rody_zm', sek=SEK_ZOLN, rx=r'partie rodow -?\d+ \(\d+ partii; ([+-]\d+)\)', lit=[' partii']),
        K('garnizony', sek=SEK_ZOLN, rx=r', garnizony (-?\d+) \(', wym=True, lit=['garnizony']),
        K('garnizony_zm', sek=SEK_ZOLN, rx=r', garnizony -?\d+ \(\d+ partii; ([+-]\d+)\)', lit=['garnizony']),
        K('milicje', sek=SEK_ZOLN, rx=r', milicje (-?\d+) \(', lit=['milicje']),
        K('bandy', sek=SEK_ZOLN, rx=r', bandy (-?\d+) \(', lit=['bandy']),
        K('bandy_partii', sek=SEK_ZOLN, rx=r', bandy -?\d+ \((\d+) partii', lit=['bandy']),
        K('karawany', sek=SEK_ZOLN, rx=r', karawany (-?\d+) \(', lit=['karawany']),
        K('tabory_wsi', sek=SEK_ZOLN, rx=r', tabory wsi (-?\d+) \(', lit=['tabory wsi']),
        K('inne', sek=SEK_ZOLN, rx=r', inne (-?\d+) \(', lit=['inne']),
        K('jency_partie', '; jency w partiach '),
        K('jency_lochy', ', w lochach osad '),
        K('wyrzutki', ' | wyrzutki w puli '),
        K('zabici', ' | dzis: zabici ', wym=True),
        K('starc', sek=SEK_DZIS, rx=r'zabici \d+ w (\d+) starciach', lit=[' starciach (']),
        K('zabici_rody', sek=SEK_DZIS, rx=r'starciach \(partie rodow (\d+)', lit=[' starciach (']),
        K('zabici_bandy', sek=SEK_DZIS, rx=r'starciach \([^)]*?, bandy (\d+)', lit=[' starciach (']),
        K('ranni', '), ranni '),
        K('rozbici', ', rozbici '),
        K('zwerbowani', '; zwerbowani ', wym=True),
        K('zw_notable_rody', ' (od notabli do partii rodow '),
        K('zw_notable_inne', ', od notabli bez wodza - garnizony, karawany '),
        K('zw_karczma', ', z karczmy '),
        K('zw_bez_osady', ', bez osady - jency albo ochotnicy z mapy '),
        K('zw_gracz', ', gracz ', sek=SEK_DZIS),
        K('dezercja', '); dezercja '),
        K('dezercja_rody', sek=SEK_DZIS, rx=r'dezercja \d+ \(partie rodow (\d+)\)', lit=[' (partie rodow ']),
        K('bil_zmiana', ' | bilans partii rodow: zmiana ', 'sint'),
        K('bil_zwerb', ' = zwerbowani przez wodzow +'),
        K('bil_reszta', sek='bilans partii rodow: zmiana', rx=r' \+ reszta ([+-]?\d+)', lit=[' + reszta ']),
        K('pod_bronia_proc', ' | pod bronia (partie rodow + garnizony): ', 'float'),
        K('regionow_ponad', '% mezczyzn swiata; regionow ponad prog 20% mezczyzn: '),
        K('regionow', rx=r'prog 20% mezczyzn: \d+ z (\d+)', lit=[' (bez ludnosci w ksiedze ']),
        K('bez_ludnosci', ' (bez ludnosci w ksiedze '),
        # ogniwo 108 (PeopleUnit.DayNote): ile hearth wsie oddaly i odzyskaly dzis za ludzi; 1 czlowiek = 1/k hearth
        K('hz_tabory', 'tabory wsi i lodzie -', 'float', sek=SEK_HZ, zr='PeopleUnit.cs'),
        K('hz_tabory_ludzi', rx=r'tabory wsi i lodzie -[\d.,]+ za (-?\d+) ludzi', lit=[' za ', ' ludzi (uzupelnien '],
          zr='PeopleUnit.cs'),
        K('hz_uzupelnien', ' ludzi (uzupelnien ', zr='PeopleUnit.cs'),
        K('hz_nowych', ', nowych ', sek=SEK_HZ, zr='PeopleUnit.cs'),
        K('hz_gra', '; gra zdjela ', 'float', zr='PeopleUnit.cs'),
        K('hz_wyrzutki', ', wyrzutki w las -', 'float', zr='PeopleUnit.cs'),
        K('hz_wyrzutki_ludzi', rx=r', wyrzutki w las -[\d.,]+ za ' + RX_F + ' ludzi', lit=[' ludzi'], zr='PeopleUnit.cs'),
        K('hz_powrot', ', powrot z lasu +', 'float', zr='PeopleUnit.cs'),
        K('hz_powrot_ludzi', rx=r', powrot z lasu \+[\d.,]+ za ' + RX_F + ' ludzi', lit=[' ludzi'], zr='PeopleUnit.cs'),
        K('hz_pobor', ', pobor wymuszony gracza -', 'float', zr='PeopleUnit.cs'),
        K('hz_zadania', ', zadania gracza ', 'float', zr='PeopleUnit.cs'),
        K('hz_incydenty', ', incydenty gracza ', 'float', zr='PeopleUnit.cs'),
        K('hz_niezgodne', '; niezgodne z formula gry i zostawione: ', zr='PeopleUnit.cs'),
        K('hz_bez_k', '; bez przelicznika kultury: ', zr='PeopleUnit.cs'),
        K('hz_potk', '; potkniecia: ', sek=SEK_HZ, zr='PeopleUnit.cs'),
        K('glodne', ' | warownie glodne '),
        K('ujemny_bilans', ', z ujemnym bilansem zywnosci '),
        K('potkniecia', ' Potkniecia ksiegi: '),
    ], _extra_ludzie, [
        ('ludnosc mln (zmiana dobowa)', '{ludnosc_mln} ({ludnosc_zm:+})'),
        ('zolnierze: partie rodow/garnizony', '{rody}/{garnizony}'),
        ('zolnierze: bandy/milicje', '{bandy}/{milicje}'),
        ('dzis: zabici/zwerbowani/dezercja', '{zabici}/{zwerbowani}/{dezercja}'),
        ('pod bronia % | regionow ponad 20%', '{pod_bronia_proc} | {regionow_ponad}'),
        ('hearth za ludzi: tabory/w las/z lasu', '{hz_tabory}/{hz_wyrzutki}/{hz_powrot}'),
        ('tabory: ludzi | gra zdjelaby hearth', '{hz_tabory_ludzi}|{hz_gra}'),
    ], 'ludzie', 'PeopleLedger.cs', bezwar=True),
    # ogniwo 109 (PopulationLaw.GrowthDaily): linia "Ludzie: przyrost naturalny ..." zaraz po linii "Ludzie: dzien N"
    Temat('Ludzie (przyrost)', 'Ludzie', r'^przyrost naturalny', [
        K('ludzi', rx=r'^Ludzie: przyrost naturalny ([+-]?\d+) ludzi \(g srednio', lit=['Ludzie: przyrost naturalny ']),
        K('g_srednio', ' ludzi (g srednio ', 'float'),
        K('hearth', rx=r'; ' + RX_F + r' hearth na \d+ wsi, w rok', lit=[' hearth na ']),
        K('wsi', rx=r' hearth na (\d+) wsi, w rok', lit=[' wsi, w rok ok. ']),
        K('w_rok', ' wsi, w rok ok. ', 'sint'),
        K('uch_ludzi', '; osobno powrot uchodzcow ', 'sint'),
        K('uch_hearth', rx=r'osobno powrot uchodzcow [+-]?\d+ ludzi \(' + RX_F + ' hearth w', lit=[' ludzi (', ' hearth w ']),
        K('uch_wsi', rx=r' hearth w (\d+) wsiach przy dnie', lit=[' wsiach przy dnie - regula ScorchedEarth do kroku 4']),
        K('rosnie', ' | wsie: rosnie '),
        K('kurczy', ', kurczy sie '),
        K('bez_zmiany', ', bez zmiany '),
        K('spalone', ' (spalone, lupione albo pod przymusem '),
        K('g_min', '; g od ', 'float'),
        K('g_med', rx=r'; g od [+-]?[\d.,]+ przez ' + RX_F + r' \(mediana\)', lit=[' przez ', ' (mediana) do ']),
        K('g_max', ' (mediana) do ', 'float'),
        K('sk_podstawa', ' | skladowe g wsi niespalonych (pkt % rocznie, wazone ludnoscia): podstawa ', 'float'),
        K('sk_dobrobyt', ', dobrobyt wobec mediany krainy ', 'float'),
        K('sk_niebezp', ', niebezpieczenstwo ', 'float'),
        K('wsi_wojna', ' (wsi krain w wojnie '),
        K('wsi_spal_reg', ', wsi w regionach ze spalonymi '),
        K('bezp', ', bezpieczenstwo warowni srednio ', 'float'),
        K('sk_glod', rx=r', glod ' + RX_F + r' \(zima tnie plon', lit=[', glod ', ' (zima tnie plon ']),
        K('glod_wsi', rx=r'zima tnie plon (\d+) wsi', lit=[', glod WYLACZONY (na sucho: zima tnie plon ']),
        K('glod_na_sucho', rx=r'; z waga [\d.,]+ byloby ' + RX_F, lit=['; z waga ', ' byloby ']),
        K('premie', ', premie gry srednio x', 'float'),
        K('wsi_premia', ' (wsi z premia '),
        K('pulap_obcial', rx=r'\), pulap \+[\d.,]+ obcial (\d+) wsi', lit=['), pulap +', ' obcial ']),
        K('dno_obcielo', rx=r' wsi, dno -[\d.,]+ obcielo (\d+) wsi', lit=[' wsi, dno -', ' obcielo ']),
        K('uch_regula_wsi', ' wsi, powrot uchodzcow +0.5 hearth: '),
        K('hearth_swiata', ' | hearth wsi swiata ', 'float'),
        K('od_wczoraj', ', od wczoraj ', 'float'),
        K('rozl_przyrost', rx=r' = (?:przyrost naturalny|wynik gry) ' + RX_F, sek=SEK_ROZL,
          lit=[' = ', 'przyrost naturalny ', 'wynik gry ']),
        K('rozl_uchodzcy', ', powrot uchodzcow ', 'float', sek=SEK_ROZL),
        K('rozl_ruch', ', ruch ludzi (tabory, wyrzutki, pobor, zadania, incydenty) ', 'float'),
        K('reszta', ', RESZTA ', 'float'),
        K('prog_0', rx=r'od 600 hearth\): (\d+) / ',
          lit=[' | wsie wedle progow produkcji (ponizej 200 / 200-599 / od 600 hearth): ', ' / ']),
        K('prog_200', rx=r'od 600 hearth\): \d+ / (\d+) / ', lit=[' / ']),
        K('prog_600', rx=r'od 600 hearth\): \d+ / \d+ / (\d+)', lit=[' / ']),
        K('f32_model', ' | float32: model ', 'float'),
        K('f32_wsie', ' hearth, wsie przyjmuja ', 'float'),
        K('f32_male', 'wsi z krokiem ponizej polowy kroku floata: '),
        K('miasta_mln', ' | miasta: ', 'float'),
        K('miasta_n', rx=r' mln w (\d+) miastach', lit=[' mln w ', ' miastach - stan (zamrozony); z dzisiejszego dobrobytu byloby ']),
        K('miasta_live', ' miastach - stan (zamrozony); z dzisiejszego dobrobytu byloby ', 'float'),
        K('miasta_dryf', rx=r' mln \(([+-]?\d+) ludzi dryfu', lit=[' mln (', ' ludzi dryfu od zalozenia stanu)']),
        K('bez_k', ' | wsi bez przelicznika ludzi: '),
        K('potk_rachunek', ' | potkniecia: rachunek wsi '),
        K('potk_linia', ', linia dobowa '),
        # przyrost WYLACZONY: hearth wsi liczy gra
        K('gra_ludzi', rx=r'hearth wsi liczy gra: ([+-]?\d+) ludzi dzis', lit=[') - hearth wsi liczy gra: ', ' ludzi dzis (']),
        K('gra_proc', rx=r' wsi = ' + RX_F + r'% rocznie\)', lit=[' wsi = ', '% rocznie)']),
    ], _extra_przyrost, [
        ('przyrost naturalny ludzi (g % rocznie)', '{ludzi:+} ({g_srednio})'),
        ('wsie: rosnie/kurczy sie/bez zmiany', '{rosnie}/{kurczy}/{bez_zmiany}'),
        ('hearth wsi: zmiana dobowa | RESZTA', '{od_wczoraj:+}|{reszta:+}'),
        ('uchodzcy ludzi | miasta mln (dryf)', '{uch_ludzi}|{miasta_mln} ({miasta_dryf:+})'),
    ], 'ludzie', 'PopulationLaw.cs'),
    Temat('Ludzie (plik)', 'Ludzie', r'^plik wszystkich regionow', [], None, [], 'ludzie', 'PeopleLedger.cs', dzienny=False),
    Temat('Ludzie (pominiete)', 'Ludzie', r'^dzien \d+ - pominiete wiersze', [
        K('pominiete', '(wyjatek przy regionie): ', wym=True),
    ], None, [], 'ludzie', 'PeopleLedger.cs', dzienny=False),
    Temat('Ludzie (regiony)', 'Ludzie (regiony)', r'^dzien \d+', [
        K('pokazanych', rx=r': dzien \d+ - (\d+) najbardziej', lit=[' najbardziej obciazonych z ']),
        K('regionow', ' najbardziej obciazonych z ', wym=True),
    ], _extra_regiony, [
        ('najbardziej obciazony region, %', '{top1_kr} {top1_proc}'),
    ], 'ludzie', 'PeopleLedger.cs'),
    Temat('Ludnosc', 'Ludnosc', r'^dzien \d+', [
        K('mln', rx=r'\| (-?\d+(?:[.,]\d+)?) mln \|', wym=True, lit=[' mln | renty zaplacone ']),
        K('zaplacone', ' mln | renty zaplacone ', wym=True),
        K('nalezne', ' z naleznych '),
    ], _extra_ludnosc, [('ludnosc mln | % rent zaplaconych', '{mln} | {zaplacone_proc}'),
                       ('renty zaplacone/nalezne', '{zaplacone}/{nalezne}')],
          'ludzie', 'PopulationLaw.cs'),
    Temat('Wyrzutki', 'Wyrzutki', r'^dzien \d+', [
        K('pula', ' | pula ', wym=True),
        K('zolnierzy', ' ludzi (zolnierzy '),
        K('prostych', ', prostych '),
        K('dezercja', ' | naplyw: dezercja '),
        K('rozbitkowie', ', rozbitkowie '),
        K('rabunki', ', rabunki '),
        K('bieda', ', bieda/wojna ', 'float'),
        K('rozwiazane', ', rozwiazane bandy '),
        K('powrot', ' | powrot do wsi ', 'float'),
        K('bandy_nowe', ' | bandy nowe '),
        K('nowe_ludzi', rx=r'bandy nowe \d+ \((\d+) ludzi', lit=[' ludzi), odmowione ']),
        K('odmowione', ' ludzi), odmowione '),
        K('puste', ', puste usuniete '),
        K('werb_pula', ' | werbunek: z puli '),
        K('werb_jency', ', jency ', sek='werbunek: z puli'),
        K('aw_lup', ' | awanse: z lupu '),
        K('aw_paser', ', od pasera '),
        K('aw_paser_zl', rx=r', od pasera \d+ \((-?\d+) zl', lit=[' zl), bez sprzetu ']),
        K('aw_bez', ' zl), bez sprzetu '),
        K('band', ' | band ', wym=True),
        K('ludzi', rx=r'\| band \d+, ludzi (\d+)', lit=[', ludzi ']),
    ], None, [
        ('pula | band/ludzi w bandach', '{pula} | {band}/{ludzi}'),
        ('awanse: z lupu/od pasera/bez sprzetu', '{aw_lup}/{aw_paser}/{aw_bez}'),
    ], 'ludzie', 'OutlawLaw.cs', bezwar=True),
    Temat('Wyrzutki (start)', 'Wyrzutki', r'^(pula poczatkowa|wczytano pule)', [], None, [], 'ludzie', 'OutlawLaw.cs',
          dzienny=False),
    Temat('Bitwy', 'Bitwy', r'^dzien \d+', [
        K('starc', rx=r': dzien \d+ - (\d+) starc', wym=True, lit=[' starc (w tym pogromow/nierownych ']),
        K('pogromow', ' starc (w tym pogromow/nierownych '),
        K('malych', ', malych potyczek pominietych '),
        K('zabitych', '), zabitych ', wym=True),
        K('rannych', ', rannych '),
        K('prawdziwych', rx=r'; w (\d+) prawdziwych bitwach', lit=[' prawdziwych bitwach zwyciezcy tracili zabitych srednio ']),
        K('zwyciezcy_proc', ' prawdziwych bitwach zwyciezcy tracili zabitych srednio ', 'float'),
        K('przegrani_proc', ', przegrani ', 'float'),
    ], None, [('starc (bitew) | zabitych/rannych', '{starc}({prawdziwych})|{zabitych}/{rannych}')],
          'ludzie', 'BattleChronicle.cs'),
    # ------------------------------------------------------------------------------------------------ dodatkowe
    Temat('ZakupyAI', 'ZakupyAI', r'^dzien \d+', [
        K('wizyt', rx=r': dzien \d+ - (\d+) wizyt', wym=True, lit=[' wizyt, ']),
        K('szt', ' wizyt, '),
        K('zlota', ' szt. kupionych za '),
        K('garnizony', ' zlota; w tym garnizony '),
        K('garnizony_zl', ' zakupow za '),
    ], None, [('zlota razem / w tym garnizony', '{zlota}/{garnizony_zl}')], 'dodatkowe', 'AiGear.cs'),
    Temat('Zuzycie AI', 'Zuzycie AI', r'^dzien \d+', [
        K('lup_obity', ' - lup obity '),
        K('zuzyte', ' szt., zuzyte w bitwach ', wym=True),
        K('naprawione', ', naprawione w miastach '),
        K('za', rx=r'naprawione w miastach \d+ za (-?\d+)', lit=[' z sakiewek ludzi; obitych razem ']),
        K('obitych', ' z sakiewek ludzi; obitych razem '),
        K('partii', rx=r' szt\. w (\d+) partiach', lit=[' szt. w ', ' partiach.']),
    ], None, [('zuzyte w bitwach/naprawione | obitych', '{zuzyte}/{naprawione}|{obitych}')],
          'dodatkowe', 'AiWear.cs'),
    Temat('HouseLevies', 'HouseLevies', r'^dzien \d+', [
        K('ochotnikow', rx=r': dzien \d+ - (\d+) szlacheckich', wym=True,
          lit=[' szlacheckich ochotnikow na ziemiach rodow to teraz ludzie rodu.']),
    ], None, [('szlacheccy ochotnicy do rodow', '{ochotnikow}')], 'dodatkowe', 'HouseLevies.cs'),
    Temat('Werbunek', 'Werbunek', r'^dzien \d+', [
        K('notablom', ' - zloto AI za ochotnikow do notabli ', wym=True),
        K('miastom', ', za najemnikow do miast '),
        K('bez_kompletu', '; nowe partie AI bez darmowego kompletu DTE: '),
    ], None, [('zloto AI: notablom/miastom', '{notablom}/{miastom}')],
          'dodatkowe', 'LevyGold.cs'),
    Temat('Pobor', 'Pobor', r'^dzien \d+', [
        K('chec', ' - chec do sluzby srednio x', 'float', wym=True),
        K('losowan', ' szansy BK ('),
    ], None, [('chec do sluzby x | losowan', '{chec} | {losowan}')], 'dodatkowe', 'Levy.cs'),
    Temat('Ochotnicy', 'Ochotnicy', r'^dzien \d+', [
        K('awanse', ' - awanse z kupionym sprzetem ', wym=True),
        K('szt', rx=r'z kupionym sprzetem \d+ \((\d+) szt', lit=[' szt. za ']),
        K('zl', rx=r' szt\. za (-?\d+) zl z kiesy', lit=[' zl z kiesy notabli do miast), cofniete (brak towaru albo zlota) ']),
        K('cofniete', ' zl z kiesy notabli do miast), cofniete (brak towaru albo zlota) '),
        K('dodatkow', '; dodatkow nie dokupiono '),
    ], None, [('awanse z kupionym sprzetem / cofniete', '{awanse}/{cofniete}')], 'dodatkowe', 'VolunteerKit.cs'),
    Temat('Ochotnicy (diagnoza)', 'Ochotnicy (diagnoza)', None, [], None, [], 'dodatkowe', 'VolunteerKit.cs', dzienny=False),
    Temat('Komplet rekruta', 'Komplet rekruta', r'^dzien \d+', [
        K('z_kompletem', ' - werbunek AI z kompletem od notabla ', wym=True),
        K('bez_zapisu', ', bez zapisu (wzorzec) '),
        K('tier1', ', tier 1 z wlasnym dobytkiem '),
        K('sprzedane', '; rzeczy ochotnikow, ktorzy odeszli, sprzedane '),
    ], None, [('z kompletem/bez zapisu/tier 1', '{z_kompletem}/{bez_zapisu}/{tier1}')], 'dodatkowe', 'RecruitKit.cs'),
    Temat('Budowy oplacone', 'Budowy oplacone', r'^dzien \d+', [
        K('osad', ' - osad ', wym=True),
        K('wydano', ', wydano '),
        K('place', ' (place i wozy do kas osad '),
        K('materialy', ', materialy z targow '),
        K('punktow', '), punktow budowy '),
        K('brak_materialow', '; wstrzymane: brak materialow '),
        K('wojna', ', wojna (budowle cywilne) '),
    ], None, [('osad|wydano|stoi: materialy/wojna', '{osad}|{wydano}|{brak_materialow}/{wojna}')],
          'dodatkowe', 'BuildFunding.cs'),
    Temat('Budowy', 'Budowy', r'^dzien \d+', [
        K('osad', ' - osad ', wym=True),
        K('w_budowie', ': w budowie '),
        K('idzie', ' (idzie '),
        K('stoi', ', stoi '),
        K('ukonczonych', ', ukonczonych poziomow '),
        K('bez_budowy', '), bez budowy '),
        K('moc', '; srednia moc ', 'float'),
    ], None, [('idzie/stoi | srednia moc pkt', '{idzie}/{stoi} | {moc}')], 'dodatkowe', 'BuildDiary.cs'),
    Temat('PodazPopyt', 'PodazPopyt', r'^kupcy wywiezli', [
        K('wywiezli', 'kupcy wywiezli ', wym=True),
        K('transakcji', ' szt. nadwyzki uzbrojenia w '),
        K('zaplacone', ' transakcjach miedzy osadami (zaplacone '),
        K('bez_odbiorcy', ' zlota); '),
    ], None, [('kupcy wywiezli szt | bez odbiorcy', '{wywiezli} | {bez_odbiorcy}')],
          'dodatkowe', 'SupplyDemand.cs'),
    Temat('WPLYW', 'WPLYW', r'^stan ', [
        K('stan', 'WPLYW: stan ', 'float', wym=True),
        K('zmiana', ', dzienna zmiana ', 'float'),
    ], None, [('wplywy gracza: stan (zmiana)', '{stan} ({zmiana:+})')], 'dodatkowe', 'InfluenceWatch.cs'),
    Temat('UniqueLaw (dzien)', 'UniqueLaw (dzien)', None, [
        K('gracz', ' - u gracza '),
        K('ai', ' szt. unikatow zamienionych na zamienniki, u AI ', wym=True),
        K('znikly', ', bez zamiennika (znikly) '),
    ], None, [('unikaty zamienione: gracz/AI | znikly', '{gracz}/{ai} | {znikly}')], 'dodatkowe', 'UniqueLaw.cs'),
    Temat('LegendaryLaw (targi)', 'LegendaryLaw', r'^targi \(dzien\)', [
        K('zdjete', rx=r'targi \([^)]*\) - (\d+) egzotycznych', wym=True,
          lit=['LegendaryLaw: targi (', ' egzotycznych wierzchowcow zdjetych z polek nie u swoich.']),
    ], None, [('egzotyczne wierzchowce zdjete z targow', '{zdjete}')], 'dodatkowe', 'LegendaryLaw.cs'),
    Temat('LegendaryLaw (inne)', 'LegendaryLaw', None, [], None, [], 'dodatkowe', 'LegendaryLaw.cs', dzienny=False),
    Temat('Audyt predkosci', 'Audyt predkosci', None, [
        K('wynik', ' => ', 'float', wym=True),
    ], None, [('predkosc partii gracza', '{wynik}')], 'dodatkowe', 'TerrainEase.cs'),
    Temat('ArmsPricing (wojny)', 'ArmsPricing', r'^wojna ', [
        K('premia', ' - kupcy oczekuja zakupow broni: premia popytu +', wym=True),
        K('razem', '), razem '),
    ], None, [('premie wojenne: ile | najwyzsza %', '{premii} | {maks_proc}')], 'dodatkowe', 'ArmsPricing.cs',
          agreg=_agreg_wojny, wielo=True),
    Temat('ArmsPricing (start)', 'ArmsPricing', None, [], None, [], 'dodatkowe', 'ArmsPricing.cs', dzienny=False),
    Temat('HistoricalPrices', 'HistoricalPrices', None, [], None, [], 'dodatkowe', 'HistoricalPrices.cs', dzienny=False),
    # linie jednorazowe i zdarzenia znanych modulow (czytane w naglowku i alarmach; tu tylko po to, zeby nie byly "obce")
    Temat('SoldierPay', 'SoldierPay', None, [], None, [], 'pieniadz', 'SoldierPay.cs', dzienny=False),
    Temat('MoneyLedger', 'MoneyLedger', None, [], None, [], 'pieniadz', 'MoneyLedger.cs', dzienny=False),
    Temat('KingdomTreasury', 'KingdomTreasury', None, [], None, [], 'pieniadz', 'KingdomTreasury.cs', dzienny=False),
    Temat('CaravanBulk', 'CaravanBulk', None, [], None, [], 'surowce', 'CaravanBulk.cs', dzienny=False),
    Temat('MineralOnce', 'MineralOnce', None, [], None, [], 'surowce', 'MineralOnce.cs', dzienny=False),
    Temat('StartStock', 'StartStock', None, [], None, [], 'surowce', 'StartStock.cs', dzienny=False),
    Temat('MaterialLaw', 'MaterialLaw', None, [], None, [], 'surowce', 'MaterialLaw.cs', dzienny=False),
    Temat('OutlawLaw', 'OutlawLaw', None, [], None, [], 'ludzie', 'OutlawLaw.cs', dzienny=False),
    Temat('PopulationLaw', 'PopulationLaw', None, [], None, [], 'ludzie', 'PopulationLaw.cs', dzienny=False),
    Temat('PeopleUnit', 'PeopleUnit', None, [], None, [], 'ludzie', 'PeopleUnit.cs', dzienny=False),
    Temat('WarLedger', 'WarLedger', None, [], None, [], 'ludzie', 'WarLedger.cs', dzienny=False),
    Temat('Klimat', 'Klimat', None, [], None, [], 'dodatkowe', 'WesterosClimate.cs', dzienny=False),
]

GRUPY = OrderedDict([('surowce', 'SUROWCE I DOWOZ'), ('pieniadz', 'PIENIADZ'), ('ludzie', 'LUDZIE'),
                     ('dodatkowe', 'DODATKOWE')])
# nazwy tematow w lewej kolumnie skrotu (pelne nazwy obowiazuja w --temat)
NAZWY_KROTKIE = {
    'Pieniadz swiata (bilans)': 'Pieniadz (bilans)',
    'Pieniadz swiata (rody)': 'Pieniadz (rody)',
    'Przeplywy osad (kasy miast)': 'Przepl.: kasy miast',
    'Przeplywy osad (kasy zamkow)': 'Przepl.: kasy zamkow',
    'Przeplywy osad (kiesy wsi)': 'Przepl.: kiesy wsi',
    'Korona (powinnosci)': 'Korona: powinnosci',
    'Korona (danina i clo)': 'Korona: danina, clo',
    'Korona (zwrot zoldu)': 'Korona: zwrot zoldu',
}

TEMATY_WG_PREFIKSU = defaultdict(list)
TEMATY_WG_NAZWY = OrderedDict()
for _t in TEMATY:
    TEMATY_WG_PREFIKSU[_t.prefiks].append(_t)
    TEMATY_WG_NAZWY[_t.nazwa] = _t

# Ogniwa lancucha paczek (87c8e96 = wpis 102 w grze -> n102b-ksiega -> ... -> n109-ludzie-przyrost). Pola:
#   nr, opis, wzorzec linii startowej, tematy dzienne, czy linia startowa jest zawsze (104: tylko przy nowej kampanii),
#   znak = (temat, kolumna): ogniwo widac takze po kolumnie cudzej linii dziennej (108: odcinek "hearth za ludzi dzis"),
#   poznac = po czym poznac ogniwo w logu (do tabeli w naglowku), paczka = plik opisu w docs\paczki albo wpis CHANGELOG
OGNIWA = [
    ('100', 'dowoz na targ', r'^MarketRoad: wsie zamkowe woza plon na targ miasta', ['Dowoz'], True, None,
     'start "MarketRoad: wsie zamkowe woza plon na targ miasta"; co dobe "Dowoz:" i "Dowoz (skutki):"', 'CHANGELOG wpis 100'),
    ('101', 'woz x2', r'^MarketRoad: woz - udzwig taborow', [], True, None,
     'start "MarketRoad: woz - udzwig taborow wsi zamkowych ... x2.0"', 'CHANGELOG wpis 101'),
    ('102', 'ksiegi (log)', r'^MoneyLedger: ', ['Pieniadz swiata', 'Ludzie'], True, None,
     'start "MoneyLedger: ... liczniki wpiete"; co dobe "Pieniadz swiata:", "Przeplywy osad:", "Ludzie:"', 'CHANGELOG wpis 102'),
    ('102b', 'ksiega: rody', r'^MoneyLedger: .*rozliczenie rodow', ['Pieniadz swiata (rody)'], True, None,
     'start "MoneyLedger: ... zold, rozliczenie rodow"; co dobe "Pieniadz swiata (rody):", "W tym zold naliczony"',
     '102b-ksiega-pieniadza.md'),
    ('103', 'karawany wg zysku', r'^CaravanBulk: ', ['Karawany', 'Karawany (stan)'], True, None,
     'start "CaravanBulk: ... latki wpiete"; co dobe "Karawany:" z "kolejnosc kupna wedle zysku" i "cena zbytu"',
     '103-karawany.md'),
    ('104', 'zapas startowy', r'^StartStock: ', [], False, None,
     '4 linie "StartStock:" przy starcie NOWEJ kampanii (wczytany zapis: jedna linia "BEZ przeliczenia" albo zadnej)',
     '104-zapas-startowy.md'),
    ('105', 'mineral BK raz', r'^MineralOnce: ', ['Mineraly (dubel BK)'], True, None,
     'start "MineralOnce: ... latka wpieta"; co dobe "Mineraly (dubel BK):" z "wsi z mnoznikiem modelu"',
     '105-mineral-bk.md'),
    ('106', 'paser', r'^OutlawLaw: .*zloto kryjowek', ['Paser'], True, None,
     'start "OutlawLaw: ... wpiete: ..., zloto kryjowek"; co dobe "Paser:" z "z paserem" i "z ladunkiem zostalo"', '106-paser.md'),
    ('107', 'zold i skarbiec', r'^SoldierPay: zold do obiegu', ['Zold'], True, None,
     'start "SoldierPay: zold do obiegu"; co dobe "Zold:" z "tarcza zoldu ... wlaczona" i "Korona: ... zwrot zoldu"',
     '107-zold-i-skarbiec.md'),
    ('108', 'ludzie: jednostka', r'^PeopleUnit: ', [], True, ('Ludzie', 'hz_tabory'),
     'start "PeopleUnit: jednostka ludzi ... CZYNNA"; co dobe "hearth za ludzi dzis" w linii "Ludzie:"',
     '108-ludzie-jednostka.md'),
    ('109', 'ludzie: przyrost', r'^PopulationLaw: przyrost naturalny', ['Ludzie (przyrost)'], True, None,
     'start "PopulationLaw: przyrost naturalny wsi ... CZYNNY"; co dobe "Ludzie: przyrost naturalny +N ludzi"',
     '109-ludzie-przyrost.md'),
]
NR_OGNIW = [o[0] for o in OGNIWA]
# grupy testowe (propozycja z docs/STAN-PRAC.md: testowac grupami zamiast dziewieciu osobnych kampanii) i nazwy dla --test
GRUPY_TESTOWE = OrderedDict([
    ('dowoz', ['100', '101']), ('ksiegi', ['102', '102b']), ('surowce', ['102b', '103', '104', '105']),
    ('pieniadz', ['106', '107']), ('ludzie', ['108', '109']), ('wszystkie', list(NR_OGNIW)),
])


def ix_ogniwa(nr):
    """Miejsce ogniwa w lancuchu (0 = najstarsze); -1 dla nieznanego numeru."""
    return NR_OGNIW.index(nr) if nr in NR_OGNIW else -1
OCZEKIWANE_STARTU = [
    ('ROT-RBM luki przechwycone', r'^HistoricalPrices: ROT-RBM luki .*przechwycone'),
    ('ColdStart zbrojownie', r'^ColdStart: zbrojownie'),
    ('ColdStart zapas kupiecki', r'^ColdStart: zapas kupiecki'),
    ('ColdStart ochotnicy', r'^ColdStart: ochotnicy tieru 2\+'),
    ('LootPrices 4/4', r'^LootPrices: .*4/4'),
    ('ForgeClock CZYNNY', r'^ForgeClock: .*CZYNNY'),
]


# =====================================================================================================================
# WCZYTANIE I PODZIAL LOGU
# =====================================================================================================================
def dekoduj(b):
    """Bajty -> (tekst, nazwa kodowania). UTF-8 z BOM / bez / cp1250; nigdy wyjatek."""
    if b.startswith(b'\xef\xbb\xbf'):
        return b[3:].decode('utf-8', 'replace'), 'utf-8 z BOM'
    if b.startswith(b'\xff\xfe') or b.startswith(b'\xfe\xff'):
        return b.decode('utf-16', 'replace'), 'utf-16'
    try:
        return b.decode('utf-8'), 'utf-8'
    except UnicodeDecodeError:
        pass
    for utnij in (1, 2, 3):                 # log uciety w polowie znaku wielobajtowego (gra zamknieta w trakcie zapisu)
        try:
            return b[:-utnij].decode('utf-8'), 'utf-8 (uciety ostatni znak)'
        except UnicodeDecodeError:
            pass
    zle = b.decode('utf-8', 'replace').count(u'\ufffd')
    dobre = len(re.findall(br'[\xc2-\xdf][\x80-\xbf]|[\xe0-\xef][\x80-\xbf]{2}|[\xf0-\xf4][\x80-\xbf]{3}', b))
    if dobre >= zle and dobre > 0:
        return b.decode('utf-8', 'replace'), 'utf-8 (uszkodzone bajty: %d)' % zle
    try:
        return b.decode('cp1250'), 'cp1250'
    except UnicodeDecodeError:
        return b.decode('latin-1', 'replace'), 'latin-1 (awaryjnie)'


class Wpis(object):
    __slots__ = ('nr', 'czas', 'tresc', 'pref', 'reszta', 'dzien', 'ciag', 'seg', 'D', 'blok', 'temat', 'dane', 'braki')

    def __init__(self, nr, czas, tresc):
        self.nr, self.czas, self.tresc = nr, czas, tresc
        self.pref = self.reszta = self.dzien = self.D = self.blok = self.temat = self.dane = None
        self.braki = None
        self.ciag = []
        self.seg = 0


RX_LINIA = re.compile(r'^\[(\d\d:\d\d:\d\d)\] ?(.*)$')
RX_PREFIKS = re.compile(r'^([^:]{1,60}?)(?: \(dzien (\d+)[^)]*\))?:(?: (.*))?$', re.S)
RX_DZIEN = re.compile(r'^dzien (\d+)\b')
# "Ludzie: przyrost naturalny +N ludzi (g srednio X% rocznie, wazone ludnoscia wsi; dzien D; ..." (ogniwo 109): numer dnia
# stoi w pierwszym nawiasie, nie na poczatku linii
RX_DZIEN_PRZYROSTU = re.compile(r'^przyrost naturalny [^|]{0,160}?; dzien (\d+)[;)]')


def rozloz(tekst):
    """Tekst logu -> (naglowek sesji, lista wpisow). Linie bez znacznika czasu doklejane do poprzedniego wpisu."""
    wpisy, naglowek = [], None
    for i, linia in enumerate(tekst.splitlines(), 1):
        m = RX_LINIA.match(linia)
        if not m:
            if wpisy:
                wpisy[-1].ciag.append(linia)
            elif linia.strip() and naglowek is None:
                naglowek = linia.strip()
            continue
        w = Wpis(i, m.group(1), m.group(2))
        if w.tresc.startswith('ERROR in '):
            w.pref = 'ERROR'
            w.reszta = w.tresc
        else:
            p = RX_PREFIKS.match(w.tresc)
            if p and p.group(1).strip() and not p.group(1)[0].isspace():
                w.pref = p.group(1).strip()
                w.reszta = p.group(3) or ''
                if p.group(2):
                    w.dzien = int(p.group(2))
                else:
                    d = RX_DZIEN.match(w.reszta)
                    if not d and w.pref == 'Ludzie':
                        d = RX_DZIEN_PRZYROSTU.match(w.reszta)
                    if d:
                        w.dzien = int(d.group(1))
            else:
                w.reszta = w.tresc
        wpisy.append(w)
    return naglowek, wpisy


class Blok(object):
    __slots__ = ('D', 'nr_od', 'nr_do', 'tematow', 'niepelny')

    def __init__(self, D):
        self.D, self.nr_od, self.nr_do, self.tematow, self.niepelny = D, None, None, 0, False


def ustal_przesuniecia(wpisy, uwagi):
    """Przesuniecie numeru dnia kazdego prefiksu: tabela z kodu + nauka z linii tej samej sekundy."""
    glosy = defaultdict(Counter)
    i, n = 0, len(wpisy)
    while i < n:
        j = i
        grupa = []
        while j < n and wpisy[j].czas == wpisy[i].czas:
            if wpisy[j].dzien is not None and wpisy[j].pref:
                grupa.append(wpisy[j])
            j += 1
        znane = set(w.dzien - PRZESUNIECIE_DNIA[w.pref] for w in grupa if w.pref in PRZESUNIECIE_DNIA)
        if len(znane) == 1:
            dref = list(znane)[0]
            for w in grupa:
                glosy[w.pref][w.dzien - dref] += 1
        i = j
    prz = {}
    for pref, licz in glosy.items():
        naj, ile = licz.most_common(1)[0]
        razem = sum(licz.values())
        if pref in PRZESUNIECIE_DNIA:
            if naj != PRZESUNIECIE_DNIA[pref] and ile >= 3 and ile >= 0.8 * razem and naj in (-1, 0, 1, 2):
                prz[pref] = naj
                uwagi.append('numer dnia w liniach "%s:" przesuniety o %+d wobec tabeli z kodu (%d z %d linii) - '
                             'przyjeto wartosc z logu' % (pref, naj - PRZESUNIECIE_DNIA[pref], ile, razem))
        elif naj in (-1, 0, 1, 2):
            prz[pref] = naj
    return prz


def podziel_na_doby(wpisy, uwagi):
    """Nadaje wpisom segmentu numer bloku (doby). Zwraca liste blokow."""
    if not wpisy:
        return []
    prz = ustal_przesuniecia(wpisy, uwagi)
    for w in wpisy:
        if w.dzien is not None and w.pref:
            w.D = w.dzien - prz.get(w.pref, PRZESUNIECIE_DNIA.get(w.pref, 0))
    # linie bez numeru dnia: doba najblizszej linii z numerem w tej samej sekundzie, inaczej poprzedniej
    ostatni = None
    for idx, w in enumerate(wpisy):
        if w.D is not None:
            ostatni = w
            continue
        if ostatni is not None and ostatni.czas == w.czas:
            w.D = ostatni.D
            continue
        nast = None
        for k in range(idx + 1, min(idx + 60, len(wpisy))):
            if wpisy[k].czas != w.czas:
                break
            if wpisy[k].dzien is not None and wpisy[k].D is not None:
                nast = wpisy[k]
                break
        if nast is not None:
            w.D = nast.D
        elif ostatni is not None:
            w.D = ostatni.D
    dni = sorted(set(w.D for w in wpisy if w.D is not None))
    if not dni:
        return []
    tematy_dnia = defaultdict(set)
    for w in wpisy:
        if w.dzien is not None and w.D is not None:
            tematy_dnia[w.D].add(w.pref)
    licznosci = sorted(len(tematy_dnia[d]) for d in dni)
    mediana = licznosci[len(licznosci) // 2]
    # chude doby na poczatku (np. samotna linia startowa z numerem dnia) to nie doby - wracaja do linii startowych
    while len(dni) > 1 and len(tematy_dnia[dni[0]]) < max(2, 0.25 * mediana):
        d0 = dni.pop(0)
        for w in wpisy:
            if w.D == d0:
                w.D = None
    # linie sprzed pierwszej linii dziennej pierwszej doby to start kampanii
    pierwsza = None
    for w in wpisy:
        if w.dzien is not None and w.D == dni[0]:
            pierwsza = w
            break
    if pierwsza is not None:
        for w in wpisy:
            if w.nr >= pierwsza.nr:
                break
            if w.czas != pierwsza.czas or w.dzien is None:
                w.D = None
    indeks = dict((d, i) for i, d in enumerate(dni))
    bloki = [Blok(d) for d in dni]
    for w in wpisy:
        if w.D is None or w.D not in indeks:
            w.blok = None
            continue
        w.blok = indeks[w.D]
        b = bloki[w.blok]
        b.nr_od = w.nr if b.nr_od is None else min(b.nr_od, w.nr)
        b.nr_do = w.nr if b.nr_do is None else max(b.nr_do, w.nr)
    for i, d in enumerate(dni):
        bloki[i].tematow = len(tematy_dnia[d])
    if len(bloki) > 1 and bloki[-1].tematow < 0.5 * mediana:
        bloki[-1].niepelny = True
    return bloki


# =====================================================================================================================
# SESJA
# =====================================================================================================================
class Sesja(object):
    def __init__(self):
        self.sciezka = self.kodowanie = self.naglowek = None
        self.rozmiar = 0
        self.linii = 0
        self.wpisy = []
        self.segmenty = []          # [(nr_linii_znacznika, [wpisy], [bloki])]
        self.wybrany = 0
        self.bloki = []
        self.wyb = []               # indeksy blokow po --dni
        self.szer = defaultdict(dict)       # nazwa tematu -> {blok: slownik}
        self.wiersze = defaultdict(lambda: defaultdict(list))   # temat wielolinijkowy -> {blok: [slowniki]}
        self.linie_tematu = defaultdict(list)
        self.zle = defaultdict(list)        # nazwa tematu -> [(wpis, braki)]
        self.obce = defaultdict(list)       # prefiks bez parsera -> wpisy w dobach
        self.start = []             # linie startowe (gra + kampania wybranego segmentu)
        self.uwagi = []             # uwagi techniczne (sekcja "formaty nierozpoznane")
        self.alarmy = []
        self.bledy = []             # (klucz, licznik, nr pierwszej, przyklad)
        self.n_error = self.n_exception = self.n_potkniec = 0
        self.ogniwa = []
        self.ogniwo_w_grze = None
        self.fakty = OrderedDict()

    # ----- dostep do szeregow
    def ser(self, nazwa):
        """[(indeks bloku, slownik)] dla wybranych dob."""
        s = self.szer.get(nazwa) or {}
        return [(bi, s[bi]) for bi in self.wyb if bi in s]

    def doba(self, bi):
        return bi + 1

    def pelne(self):
        return [bi for bi in self.wyb if not self.bloki[bi].niepelny] or list(self.wyb)


def status_linii(tresc):
    if re.search(r'\bBRAK\b', tresc):
        return 'BRAK'
    if 'NIE wpiet' in tresc:
        return 'NIE wpieta'
    if (re.search(r'WYLACZON', tresc) or 'regula wylaczona w ustawieniach' in tresc
            or 'wylaczona - same liczniki' in tresc                    # PeopleUnit (108)
            or 'hearth dziennie) wylaczony w ustawieniach' in tresc):   # PopulationLaw: przyrost naturalny (109)
        return 'WYLACZONE'
    if 'CZYNN' in tresc:
        return 'CZYNNE'
    if 'wpiet' in tresc:
        return 'wpiete'
    return 'inne'


def analizuj(tekst, sciezka='(tekst)', kodowanie='?', rozmiar=None, wczytanie=None, dni=None):
    ses = Sesja()
    ses.sciezka, ses.kodowanie = sciezka, kodowanie
    ses.rozmiar = rozmiar if rozmiar is not None else len(tekst.encode('utf-8', 'replace'))
    ses.linii = tekst.count('\n') + (0 if tekst.endswith('\n') or not tekst else 1)
    ses.naglowek, ses.wpisy = rozloz(tekst)

    # --- segmenty: kazde "Behavior dodany do kampanii." to nowa kampania albo wczytanie
    seg, granice = 0, []
    for w in ses.wpisy:
        if w.tresc.startswith(ZNACZNIK_KAMPANII):
            seg += 1
            granice.append(w.nr)
        w.seg = seg
    liczba_seg = max(1, seg)
    for s in range(1, liczba_seg + 1):
        ws = [w for w in ses.wpisy if w.seg == s or (seg == 0)]
        uw = []
        try:
            bloki = podziel_na_doby(ws, uw)
        except Exception as e:
            bloki = []
            uw.append('podzial na doby nie powiodl sie (%s: %s)' % (type(e).__name__, e))
        ses.segmenty.append((granice[s - 1] if granice else 1, ws, bloki, uw))
    if wczytanie is not None and 1 <= wczytanie <= len(ses.segmenty):
        ses.wybrany = wczytanie - 1
    else:
        ses.wybrany = max(range(len(ses.segmenty)), key=lambda i: (len(ses.segmenty[i][2]), i))
    _, ws, ses.bloki, uw = ses.segmenty[ses.wybrany]
    ses.uwagi.extend(uw)
    ses.wpisy_seg = ws

    # --- zakres dob
    ses.wyb = list(range(len(ses.bloki)))
    if dni and ses.bloki:
        a, b = dni
        if a >= 10000 or b >= 10000:
            ses.wyb = [i for i, bl in enumerate(ses.bloki) if a <= bl.D <= b]
        else:
            ses.wyb = [i for i in range(len(ses.bloki)) if a <= i + 1 <= b]

    # --- linie startowe: start gry (przed pierwszym znacznikiem) + start kampanii wybranego segmentu
    pierwszy_nr = ses.bloki[0].nr_od if ses.bloki else None
    for w in ses.wpisy:
        if w.seg == 0 and seg > 0:
            ses.start.append(w)
    for w in ws:
        if w.blok is None and (pierwszy_nr is None or w.nr < pierwszy_nr) and not (w.seg == 0 and seg > 0):
            ses.start.append(w)

    _rozdaj_tematy(ses, ws)
    _zbierz_bledy(ses)
    _zbierz_fakty(ses, ws)
    _ustal_lancuch(ses, ws)
    try:
        _alarmy(ses, ws)
    except Exception as e:      # reguly maja wlasne zabezpieczenia; to jest ostatnia siatka
        ses.uwagi.append('alarmy: blad wewnetrzny (%s: %s)' % (type(e).__name__, e))
    return ses


def _rozdaj_tematy(ses, ws):
    for w in ws:
        if not w.pref or w.pref == 'ERROR':
            continue
        kandydaci = TEMATY_WG_PREFIKSU.get(w.pref)
        if not kandydaci:
            if w.blok is not None:
                ses.obce[w.pref].append(w)
            continue
        temat = None
        for t in kandydaci:
            if t.pasuje(w.reszta or ''):
                temat = t
                break
        if temat is None:
            if w.blok is not None:
                ses.obce[w.pref + ' (inny rodzaj linii)'].append(w)
            continue
        w.temat = temat
        ses.linie_tematu[temat.nazwa].append(w)
        if not temat.kol and temat.extra is None:
            continue
        try:
            d, braki = temat.czytaj(w.tresc)
        except Exception as e:
            d, braki = OrderedDict(), ['wyjatek:%s' % type(e).__name__]
        d['_nr'] = w.nr
        w.dane, w.braki = d, braki
        if braki:
            ses.zle[temat.nazwa].append((w, braki))
            continue
        if w.blok is None:
            continue
        if temat.wielo:
            ses.wiersze[temat.nazwa][w.blok].append(d)
        else:
            if w.blok in ses.szer[temat.nazwa]:
                ses.szer[temat.nazwa][w.blok]['_powtorzen'] = ses.szer[temat.nazwa][w.blok].get('_powtorzen', 1) + 1
                d['_powtorzen'] = ses.szer[temat.nazwa][w.blok]['_powtorzen']
            ses.szer[temat.nazwa][w.blok] = d
    for nazwa, wg_bloku in ses.wiersze.items():
        t = TEMATY_WG_NAZWY[nazwa]
        for bi, lista in wg_bloku.items():
            try:
                ses.szer[nazwa][bi] = t.agreg(lista)
            except Exception as e:
                ses.uwagi.append('%s: agregacja doby %d nie powiodla sie (%s)' % (nazwa, bi + 1, type(e).__name__))


RX_POTK = re.compile(r'[Pp]otkniecia[^.|]*')


def _zbierz_bledy(ses):
    grupy = OrderedDict()

    def dodaj(klucz, w, przyklad):
        if klucz not in grupy:
            grupy[klucz] = [0, w.nr, przyklad]
        grupy[klucz][0] += 1
    for w in ses.wpisy:
        if w.pref == 'ERROR':
            ses.n_error += 1
            m = re.match(r'^(ERROR in [^:]{1,60}): ?([\w.]+)?:? ?', w.tresc)
            # przyklad: komunikat wyjatku i pierwsza linia sladu stosu
            reszta = (w.tresc[m.end():] if m else '') + ((' | ' + w.ciag[0].strip()) if w.ciag else '')
            dodaj((m.group(1) + ': ' + (m.group(2) or '')) if m else w.tresc[:70], w, reszta)
            continue
        calosc = w.tresc if not w.ciag else w.tresc + ' ' + ' '.join(w.ciag[:3])
        if 'Exception' in calosc or 'exception' in calosc:
            ses.n_exception += 1
            m = re.search(r'([\w.]*Exception)', calosc)
            dodaj('%s: %s' % (w.pref or 'bez prefiksu', m.group(1) if m else 'Exception'), w, w.tresc)
            continue
        for m in RX_POTK.finditer(w.tresc):
            liczby = [int(x) for x in re.findall(r'(?<![\w.])(\d+)(?![\w.%])', m.group(0))]
            if any(x > 0 for x in liczby):
                ses.n_potkniec += 1
                dodaj('%s: %s' % (w.pref or '?', re.sub(r'\d+', 'N', m.group(0)).split(':')[0][:40]), w, m.group(0))
                break
    ses.bledy = sorted(([k] + v for k, v in grupy.items()), key=lambda g: (0 if g[0].startswith('ERROR') else 1, g[2]))


def _zbierz_fakty(ses, ws):
    f = ses.fakty
    wszystkie = ses.start + [w for w in ws if w.blok is not None]
    for w in wszystkie:
        t = w.tresc
        if w.pref == 'Kalendarz':
            m = re.search(r'rok ma (\d+) dni', t)
            if m:
                f['rok_dni'] = int(m.group(1))
        elif w.pref == 'MaterialLaw':
            m = re.search(r'wydobycie rudy x(-?\d+(?:[.,]\d+)?), drewna x(-?\d+(?:[.,]\d+)?)', t)
            if m:
                f['mnoznik_rudy'] = float(m.group(1).replace(',', '.'))
                f['mnoznik_drewna'] = float(m.group(2).replace(',', '.'))
                f['mnoznik_nr'] = w.nr
        elif w.pref == 'ColdStart' or (w.pref == 'StartKit' and 'zloto startowe' in t):
            f['nowa_kampania'] = True
        elif w.pref == 'Wyrzutki' and 'pula poczatkowa' in t:
            m = re.search(r'pula poczatkowa (\d+) ludzi', t)
            if m:
                f['pula_poczatkowa'] = int(m.group(1))
            f['pula_nr'] = w.nr
            m = re.search(r'; zdjeto ' + RX_F + r' hearth: (.*?)\)\.?$', t)      # dopisek ogniwa 108
            if m:
                f['zdjeto_hearth'] = _liczba(m.group(1))
                f['zdjeto_jak'] = m.group(2)
            f['nowa_kampania'] = True
        elif w.pref == 'Wyrzutki' and 'wczytano pule' in t:
            f['wczytana'] = True
        elif w.pref == 'KingdomTreasury':
            m = re.search(r'podmienionych stalych (\d+), oczekiwane (\d+)', t)
            if m:
                f['stale_skarbca'] = (int(m.group(1)), int(m.group(2)), w.nr)
        elif w.pref == 'StartStock':
            f.setdefault('startstock', []).append(w)
        elif w.pref == 'Ludzie' and 'plik wszystkich regionow' in t:
            f['csv'] = t.split('): ', 1)[-1].strip() if '): ' in t else None
        elif w.pref == 'Przeplywy osad' and (w.reszta or '').startswith('modele czynne'):
            f['modele'] = w
        elif w.pref == 'MoneyLedger' and 'liczniki wpiete' in t:
            f['moneyledger'] = w
        elif w.pref == 'CaravanBulk':
            f.setdefault('caravanbulk', w)
        elif w.pref == 'MineralOnce':
            f.setdefault('mineralonce', w)
        elif w.pref == 'OutlawLaw' and 'wpiete: ' in t:
            f['outlaw'] = w
        elif w.pref == 'MarketRoad' and 'woz - udzwig taborow' in t:
            f['woz'] = w
        elif w.pref == 'SoldierPay':
            if 'zold do obiegu' in t:
                f['soldierpay'] = w
            elif 'latki modelu finansow' in t:
                f['soldierpay_latki'] = w
            elif 'tarcza zoldu w kasach miast' in t:
                f['soldierpay_tarcza'] = w
        elif w.pref == 'PeopleUnit':
            f.setdefault('peopleunit', w)
        elif w.pref == 'PopulationLaw':
            if 'przyrost naturalny' in t:
                f.setdefault('przyrost_start', w)
            elif 'czynny model dobrobytu gry' in t:
                f['model_dobrobytu'] = w
            elif 'kalibracja ludnosci' in t:
                f.setdefault('kalibracja', w)
            elif 'srednia swiata' in t:
                f['srednia_swiata'] = w
            elif 'ludnosc miast zapisana jako stan' in t:
                f.setdefault('miasta_stan', w)
    # uklad linii ksiegi pieniadza: wpis 102 (w grze dzis) albo 102b - z linii dziennych, a gdy ich nie ma, z linii startowej
    uklady = Counter()
    for nazwa in ('Pieniadz swiata (bilans)', 'Przeplywy osad'):
        for bi, d in (ses.szer.get(nazwa) or {}).items():
            if d.get('uklad'):
                uklady[d['uklad']] += 1
    if ses.linie_tematu.get('Pieniadz swiata (rody)'):
        uklady[UKLAD_102B] += len(ses.linie_tematu['Pieniadz swiata (rody)'])
    f['uklad_linii'] = dict(uklady)
    if uklady:
        f['uklad_ksiegi'] = UKLAD_102B if uklady[UKLAD_102B] >= uklady[UKLAD_102] else UKLAD_102
        f['uklad_mieszany'] = bool(uklady[UKLAD_102B] and uklady[UKLAD_102])
    elif 'moneyledger' in f:
        f['uklad_ksiegi'] = UKLAD_102B if 'rozliczenie rodow' in f['moneyledger'].tresc else UKLAD_102


def _ustal_lancuch(ses, ws):
    wszystkie = ses.start + [w for w in ws if w.blok is not None]
    najwyzsze = None
    for ix, (nr, opis, wz, dzienne, zawsze, znak, poznac, paczka) in enumerate(OGNIWA):
        rx = re.compile(wz)
        trafienia = [w for w in wszystkie if rx.search(w.tresc)]
        dob = OrderedDict((n, len(ses.szer.get(n) or {})) for n in dzienne)
        linii = OrderedDict((n, len(ses.linie_tematu.get(n) or [])) for n in dzienne)
        znak_dob = None
        if znak:
            znak_dob = len([1 for d in (ses.szer.get(znak[0]) or {}).values() if d.get(znak[1]) is not None])
        jest = bool(trafienia)
        ses.ogniwa.append({'nr': nr, 'ix': ix, 'opis': opis, 'jest': jest, 'wpis': trafienia[0] if trafienia else None,
                           'ile': len(trafienia), 'dob': dob, 'linii': linii, 'zawsze': zawsze, 'znak': znak,
                           'znak_dob': znak_dob, 'poznac': poznac, 'paczka': paczka,
                           'status': status_linii(trafienia[0].tresc) if trafienia else None})
        if jest or any(linii.values()) or znak_dob:
            najwyzsze = nr
    ses.ogniwo_w_grze = najwyzsze


def ma_ogniwo(ses, nr):
    for o in ses.ogniwa:
        if o['nr'] == nr:
            return bool(o['jest'] or any(o['linii'].values()) or o.get('znak_dob'))
    return False


def ogniwo(ses, nr):
    for o in ses.ogniwa:
        if o['nr'] == nr:
            return o
    return None


# =====================================================================================================================
# ALARMY
# =====================================================================================================================
def _alarmy(ses, ws):
    def al(poziom, tekst, nr=None):
        ses.alarmy.append((poziom, nr, tekst))

    reguly = []

    def regula(f):
        reguly.append(f)
        return f


    @regula
    def r_bledy():
        if ses.n_error:
            g = [b for b in ses.bledy if b[0].startswith('ERROR')]
            al('ALARM', 'ERROR w logu: %d linii w %d grupach, pierwsza: %s' % (ses.n_error, len(g), obetnij(g[0][0], 70)),
               g[0][2])
        if ses.n_exception:
            g = [b for b in ses.bledy if not b[0].startswith('ERROR') and 'Exception' in b[0]]
            if g:
                al('ALARM', 'Exception poza liniami ERROR: %d linii, pierwsza: %s' % (ses.n_exception, obetnij(g[0][0], 70)),
                   g[0][2])

    @regula
    def r_start():
        for w in ses.start:
            st = status_linii(w.tresc)
            if st in ('BRAK', 'NIE wpieta'):
                al('ALARM', 'linia startowa %s: %s' % (st, wycinek(w.tresc, 110)), w.nr)
            elif st == 'WYLACZONE':
                al('uwaga', 'linia startowa WYLACZONE: %s' % wycinek(w.tresc, 105), w.nr)
        for w in ws:
            if w.blok is not None and w.pref in ('SoldierPay', 'MoneyLedger', 'CaravanBulk', 'MineralOnce', 'OutlawLaw',
                                                 'PeopleUnit', 'PopulationLaw'):
                if status_linii(w.tresc) in ('BRAK', 'NIE wpieta'):
                    al('ALARM', 'linia %s w kampanii: %s' % (status_linii(w.tresc), wycinek(w.tresc, 105)), w.nr)
        st = ses.fakty.get('stale_skarbca')
        if st and st[0] != st[1]:
            al('ALARM', 'KingdomTreasury: podmienionych stalych %d, oczekiwane %d - zloto z niczego do skarbcow moze plynac'
               % (st[0], st[1]), st[2])

    @regula
    def r_lancuch():
        pewne = [o for o in ses.ogniwa if o['zawsze'] and o['ix'] >= ix_ogniwa('102')]
        obecne = [o for o in pewne if o['jest']]
        if obecne:
            szczyt = max(obecne, key=lambda o: o['ix'])
            for o in pewne:
                if o['ix'] < szczyt['ix'] and not o['jest']:
                    al('ALARM', 'lancuch niespojny: jest linia startowa ogniwa %s, a brak ogniwa %s (%s)'
                       % (szczyt['nr'], o['nr'], o['opis']))
        for o in ses.ogniwa:
            # linia startowa z BRAK / WYLACZONE ma juz wlasny alarm - brak linii dziennych jest wtedy skutkiem, nie nowina
            if o['jest'] and o['zawsze'] and ses.bloki and o['status'] not in ('BRAK', 'NIE wpieta', 'WYLACZONE'):
                for n, ile in o['linii'].items():
                    if ile == 0 and len(ses.pelne()) >= 2:
                        al('ALARM', 'ogniwo %s wpiete, ale w %d dobach nie ma ani jednej linii "%s:"'
                           % (o['nr'], len(ses.pelne()), n), o['wpis'].nr if o['wpis'] else None)
                if o['znak'] and not o['znak_dob'] and len(ses.szer.get(o['znak'][0]) or {}) >= 2:
                    al('ALARM', 'ogniwo %s wpiete, ale linie "%s:" nie maja jego odcinka (%d dob) - %s'
                       % (o['nr'], o['znak'][0], len(ses.szer.get(o['znak'][0]) or {}), o['poznac'].split('; ')[-1]),
                       o['wpis'].nr if o['wpis'] else None)
        if ses.fakty.get('uklad_mieszany'):
            u = ses.fakty.get('uklad_linii') or {}
            al('uwaga', 'ksiega pieniadza: w jednej kampanii linie w dwoch ukladach (wpis 102: %d, 102b: %d) - DLL podmieniony '
               'w trakcie sesji?' % (u.get(UKLAD_102, 0), u.get(UKLAD_102B, 0)))

    @regula
    def r_luki():
        for i in range(1, len(ses.bloki)):
            a, b = ses.bloki[i - 1], ses.bloki[i]
            if b.D - a.D > 1 and i in ses.wyb:
                al('ALARM', 'brak dob: po dniu gry %d nastepuje dzien %d (%d dob bez zadnej linii dziennej)'
                   % (a.D, b.D, b.D - a.D - 1), b.nr_od)
        pelne = ses.pelne()
        if len(pelne) < 3:
            return
        for t in TEMATY:
            if not t.bezwar:
                continue
            s = ses.szer.get(t.nazwa) or {}
            obecne = [bi for bi in pelne if bi in s]
            if len(obecne) < 2:
                continue
            brak = [bi for bi in pelne if obecne[0] < bi < obecne[-1] and bi not in s]
            if brak:
                al('ALARM', 'temat "%s": brak linii w dobach %s (tick dobowy przerwany albo zmieniony format)'
                   % (t.nazwa, ', '.join(str(b + 1) for b in brak[:8]) + (' ...' if len(brak) > 8 else '')),
                   ses.bloki[brak[0]].nr_od)

    @regula
    def r_ruda():
        ruda = ses.ser('Ruda')
        stan = ses.ser('Karawany (stan)')
        if ma_ogniwo(ses, '103') and len(stan) >= KARAWANY_MIN_DOB:
            a, b = stan[0][1], stan[-1][1]
            pa, pb = a.get('ruda_bez'), b.get('ruda_bez')
            if pa is not None and pb is not None and pb > KARAWANY_BEZ_TOWARU_CEL:
                spadek = proc(pa - pb, pa) if pa else 0.0
                if spadek is None or spadek < KARAWANY_MIN_SPADEK_PROC:
                    al('ALARM', 'karawany: "ruda: bez towaru" nie spada - %d -> %d miast po %d dobach (cel <= %d), '
                       'w jukach karawan %s ladunkow' % (pa, pb, len(stan), KARAWANY_BEZ_TOWARU_CEL,
                                                         skr(b.get('ruda_juki'))), b.get('_nr'))
        elif ruda:
            d = ruda[-1][1]
            p = d.get('bez_towaru_proc')
            if p is not None and p >= PROG_MIAST_BEZ_RUDY_PROC:
                al('uwaga', 'ruda: %d z %d miast bez towaru w ostatniej dobie (%.0f%%, prog %.0f%%)'
                   % (d.get('bez_towaru'), d.get('miast'), p, PROG_MIAST_BEZ_RUDY_PROC), d.get('_nr'))
        for nazwa, abs_prog, proc_prog, dop in (('Ruda', BEZ_WYJASNIENIA_RUDA_ABS, BEZ_WYJASNIENIA_RUDA_PROC, ''),
                                                ('Drewno', BEZ_WYJASNIENIA_DREWNO_ABS, 0.0,
                                                 ' (znana dosypka RealisticBannerlord ok. +2100 jest ponizej progu)')):
            zle = []
            for bi, d in ses.ser(nazwa):
                bw, raz = d.get('bez_wyjasnienia'), d.get('razem')
                if bw is None:
                    continue
                if abs(bw) > abs_prog and (not raz or 100.0 * abs(bw) / raz > proc_prog):
                    zle.append((abs(bw), bw, bi, d.get('_nr')))
            if zle:
                naj = max(zle)
                al('ALARM', '%s: "bez wyjasnienia" ponad prog %d w %d dobach, najwiecej %+d w dobie %d%s'
                   % (nazwa, abs_prog, len(zle), naj[1], naj[2] + 1, dop), naj[3])
        bud = [(bi, d) for bi, d in ruda if (d.get('budowy') or 0) > 0]
        if bud:
            al('uwaga', 'Ruda: pozycja "budowy" niezerowa w %d dobach (budowy nie powinny kupowac rudy), np. %d'
               % (len(bud), bud[0][1].get('budowy')), bud[0][1].get('_nr'))

    @regula
    def r_karawany():
        kar = ses.ser('Karawany')
        stan = ses.ser('Karawany (stan)')
        wyl = [(bi, d) for bi, d in kar if d.get('wylaczona')]
        if wyl:
            al('ALARM', 'Karawany: REGULA WYLACZONA w ustawieniach (%d dob)' % len(wyl), wyl[0][1].get('_nr'))
        for nazwa in SUROWCE_KARAWAN.values():
            seria, najdl, pocz = 0, 0, None
            biez_pocz = None
            for bi, d in stan:
                if d.get(nazwa + '_stoi'):
                    if seria == 0:
                        biez_pocz = d.get('_nr')
                    seria += 1
                    if seria > najdl:
                        najdl, pocz = seria, biez_pocz
                else:
                    seria = 0
            if najdl > KUPNO_STOI_MAKS_DOB:
                al('ALARM', 'Karawany (stan): %s - "KUPNO STOI" przez %d dob z rzedu (prog %d)'
                   % (nazwa, najdl, KUPNO_STOI_MAKS_DOB), pocz)
        if len(kar) >= KARAWANY_ROZRUCH_DOB:
            pierwsze = kar[:KARAWANY_ROZRUCH_DOB]
            if all((d.get('z_zakupem') or 0) == 0 for _, d in pierwsze):
                al('uwaga', 'Karawany: zero zakupow z nadwyzek przez pierwsze %d dob (rozruch po 104 mial trwac 7-10 dob)'
                   % KARAWANY_ROZRUCH_DOB, pierwsze[-1][1].get('_nr'))
        if kar:
            brak = sum(d.get('odp_brak_miejsca') or 0 for _, d in kar)
            zak = sum(d.get('z_zakupem') or 0 for _, d in kar)
            if brak > 20 and brak > KARAWANY_BRAK_MIEJSCA_KROTNOSC * max(1, zak):
                al('uwaga', 'Karawany: "brak miejsca w jukach" %d razy wobec %d wyjazdow z zakupem - rozwazyc zakup przed '
                   'zakupami BK' % (brak, zak), kar[-1][1].get('_nr'))
        # 103: miasta z kasa maja brak, a zadne nie daje ceny = wycena bez partii sie nie udala (cudzy model cen)
        bez_ceny = [(bi, d) for bi, d in stan if d.get('bez_ceny')]
        if bez_ceny:
            al('ALARM', 'Karawany (stan): "cena zbytu brak - nikt nie kupuje" przy niezerowym "brakuje" (%s; %d dob) - wycena '
               'zbytu sie nie udala, karawany tego nie kupia' % (bez_ceny[0][1].get('bez_ceny'), len(bez_ceny)),
               bez_ceny[0][1].get('_nr'))

    @regula
    def r_startstock():
        linie = ses.fakty.get('startstock') or []
        przeliczone = False
        for w in linie:
            t = w.tresc
            m = re.search(r'StartStock: (\w+) - (\d+) szt\. po .*? -> (\d+) ladunkow po .*?cel (-?\d+)', t)
            if m:
                przeliczone = True
                if int(m.group(3)) != int(m.group(4)):
                    al('ALARM', 'StartStock: %s po przeliczeniu %s ladunkow, cel %s - suma sie nie zgadza'
                       % (m.group(1), m.group(3), m.group(4)), w.nr)
            m = re.search(r'gotowe - .*?; potkniecia (\d+)', t)
            if m and int(m.group(1)) > 0:
                al('ALARM', 'StartStock: potkniecia %s przy przeliczeniu zapasu startowego' % m.group(1), w.nr)
            if 'BEZ przeliczenia' in t or 'WYLACZONE' in t or 'nie da sie odczytac' in t or 'przelicznik ladunku 1' in t:
                al('uwaga', 'StartStock: zapas startowy NIE przeliczony - %s' % obetnij(t[12:], 95), w.nr)
        ruda = ses.ser('Ruda')
        if przeliczone and ruda and ruda[0][0] == 0:
            raz = ruda[0][1].get('razem')
            if raz is not None and raz >= RUDA_RAZEM_PO_104:
                al('ALARM', 'po StartStock "Ruda: razem" pierwszej doby = %d (oczekiwane ponizej %d)'
                   % (raz, RUDA_RAZEM_PO_104), ruda[0][1].get('_nr'))
        if not linie and ses.fakty.get('nowa_kampania') and ma_ogniwo(ses, '105'):
            al('ALARM', 'ogniwo 105 wgrane, nowa kampania, a nie ma ani jednej linii "StartStock:" (ogniwo 104)')

    @regula
    def r_mineral():
        # Mnoznik rudy: po przebudowie lancucha 06.10 ogniwo 105 nie zmienia wydobycia i "wydobycie rudy x3.0" jest dobrze
        # (dawny alarm "x3.0 przy wgranym 105" usuniety). Alarmem ogniwa 105 jest latka NIE wpieta.
        jest105 = ma_ogniwo(ses, '105')
        w = ses.fakty.get('mineralonce')
        if w is not None and status_linii(w.tresc) not in ('NIE wpieta', 'BRAK') and 'latka wpieta' not in w.tresc:
            al('ALARM', 'MineralOnce: linia startowa bez "latka wpieta": %s' % wycinek(w.tresc, 90), w.nr)
        mi = ses.ser('Mineraly (dubel BK)')
        for bi, d in mi:
            if d.get('stan') in ('NIE wpieta', 'WYLACZONA'):
                al('ALARM', 'Mineraly (dubel BK): latka %s - BK dopisuje mineral dwa razy' % d.get('stan'), d.get('_nr'))
                break
        obce = [(bi, d) for bi, d in mi if d.get('obce')]
        if obce:
            al('ALARM', 'Mineraly (dubel BK): na liscie powtorzen inne przedmioty: %s (BK dubluje produkcje typu wsi?)'
               % obce[0][1].get('obce'), obce[0][1].get('_nr'))
        for k, lim in MINERALY_LIMIT_WSI.items():
            ponad = [(bi, d) for bi, d in mi if (d.get(k) or 0) > lim]
            if ponad:
                al('uwaga', 'Mineraly (dubel BK): %s w %d wsiach, wiecej niz %d wsi tego typu na mapie'
                   % (k, ponad[0][1].get(k), lim), ponad[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in mi if (d.get('potkniecia') or 0) > 0]
        if potk:
            al('ALARM', 'Mineraly (dubel BK): potkniecia %d' % potk[-1][1].get('potkniecia'), potk[0][1].get('_nr'))
        # powtorzenia zdjete z listy, a zadna wies nie ma mnoznika modelu = zdjety wpis nie jest oddawany (pol wydobycia)
        bez_mn = [(bi, d) for bi, d in mi if d.get('stan') == 'zdjete' and d.get('wsi_mnoznik') == 0]
        if bez_mn:
            al('ALARM', 'Mineraly (dubel BK): powtorzenia zdjete, a "wsi z mnoznikiem modelu teraz 0" (%d dob) - zdjety wpis nie '
               'jest oddawany, wydobycie mineralow spada o polowe' % len(bez_mn), bez_mn[0][1].get('_nr'))
        ruda = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
        na_wies = [d['na_wies_do_modelu'] for _, d in ruda if d.get('na_wies_do_modelu') is not None]
        if jest105 and len(na_wies) >= 3:
            med = mediana(na_wies)
            if med > STOSUNEK_NA_WIES_PO_105:
                al('ALARM', 'Ruda: kopiaca wies dopisuje %.2f x tyle, ile liczy dla niej "model" (mediana, prog %.1f) przy ogniwie '
                   '105 - mineral nadal podwojny?' % (med, STOSUNEK_NA_WIES_PO_105), ruda[-1][1].get('_nr'))

    @regula
    def r_dowoz():
        dw = ses.ser('Dowoz')
        wyl = [(bi, d) for bi, d in dw if d.get('wylaczona')]
        if wyl:
            al('ALARM', 'Dowoz: LATKA WYLACZONA w ustawieniach (%d dob)' % len(wyl), wyl[0][1].get('_nr'))
        sk = ses.ser('Dowoz (skutki)')
        if sk:
            d = sk[-1][1]
            a, b = d.get('zatk_zamkowe_proc'), d.get('zatk_miejskie_proc')
            if a is not None and b is not None and a - b > ZATKANE_ROZNICA_PP:
                al('uwaga', 'Dowoz (skutki): zatkane magazyny wsi zamkowych %.1f%% wobec %.1f%% wsi miejskich '
                   '(roznica ponad %.0f pp)' % (a, b, ZATKANE_ROZNICA_PP), d.get('_nr'))

    @regula
    def r_pieniadz():
        bil = ses.ser('Pieniadz swiata (bilans)')
        stary_uklad = any(d.get('uklad') == UKLAD_102 for _, d in bil)
        # uklad wpisu 102 liczy zold dwa razy (raz w ujsciach, raz w saldach rodow) - reszta jest dodatnia i bliska zoldowi;
        # w ukladzie 102b zold siedzi w rozliczeniach rodow i reszta rzedu zoldu znaczylaby, ze poprawka nie dziala
        stos = [(d['reszta'] / float(d['zold']), bi, d) for bi, d in bil if d.get('zold') and d.get('reszta') is not None]
        blisko = [x for x in stos if ZOLD_DUBEL_OD <= x[0] <= ZOLD_DUBEL_DO]
        jak_zold = len(stos) >= 2 and len(blisko) >= 0.6 * len(stos)
        znany_dubel = jak_zold and stary_uklad
        zle, ponad_w_dublu = [], 0
        for bi, d in bil:
            rp, re_, zold = d.get('reszta_proc'), d.get('reszta'), d.get('zold')
            if rp is None or rp <= RESZTA_BILANSU_PROC_RUCHU:
                continue
            if znany_dubel and zold and re_ is not None and ZOLD_DUBEL_OD * zold <= re_ <= ZOLD_DUBEL_DO * zold:
                ponad_w_dublu += 1
            else:
                zle.append((rp, re_, bi, d.get('_nr')))
        if zle:
            naj = max(zle)
            al('ALARM', 'Pieniadz swiata (bilans): reszta ponad %.0f%% ruchu w %d dobach, najwiecej %s (%.0f%%) w dobie %d'
               % (RESZTA_BILANSU_PROC_RUCHU, len(zle), skr(naj[1], True), naj[0], naj[2] + 1), naj[3])
        if jak_zold:
            st = sorted(x[0] for x in blisko)
            d = blisko[-1][2]
            if znany_dubel:
                al('uwaga', 'Pieniadz swiata (bilans): znane podwojne liczenie zoldu (uklad wpisu 102, znika po 102b) - reszta '
                   'bliska zoldowi w %d z %d dob (mediana reszta/zold %.2f; np. reszta %s, zold %s); ponad %.0f%% ruchu: %d dob'
                   % (len(blisko), len(stos), st[len(st) // 2], skr(d.get('reszta'), True), skr(d.get('zold')),
                      RESZTA_BILANSU_PROC_RUCHU, ponad_w_dublu), d.get('_nr'))
            else:
                al('uwaga', 'Pieniadz swiata (bilans): uklad 102b, a reszta nadal rzedu zoldu w %d z %d dob (mediana reszta/zold '
                   '%.2f; np. reszta %s, zold %s) - po poprawce nie powinna' % (
                       len(blisko), len(stos), st[len(st) // 2], skr(d.get('reszta'), True), skr(d.get('zold'))), d.get('_nr'))
        niewp = [(bi, d) for bi, d in bil if d.get('rody_niewpiete')]
        if niewp:
            al('ALARM', 'Pieniadz swiata (bilans): licznik rozliczen rodow nie jest wpiety (%d dob) - salda rodow w pozycjach '
               'GiveGoldAction' % len(niewp), niewp[0][1].get('_nr'))
        # 102b: linia "Pieniadz swiata (rody):"
        rody = ses.ser('Pieniadz swiata (rody)')
        nd = [(bi, d) for bi, d in rody if (d.get('niedomkniete') or 0) > 0]
        if nd:
            al('ALARM', 'Pieniadz swiata (rody): rozliczenia niedomkniete w %d dobach (np. %d) - wyjatek w kodzie gry albo moda '
               'w trakcie rozliczenia rodu' % (len(nd), nd[0][1].get('niedomkniete')), nd[0][1].get('_nr'))
        nw = [(bi, d) for bi, d in rody if d.get('niewpiety')]
        if nw and not niewp:
            al('ALARM', 'Pieniadz swiata (rody): licznik rozliczen rodow nie jest wpiety (%d dob)' % len(nw), nw[0][1].get('_nr'))
        bild = dict(bil)
        rozne = []
        for bi, d in rody:
            b = bild.get(bi)
            if b is None or d.get('na_plus') is None or b.get('rody_plus') is None:
                continue
            if d['na_plus'] != b['rody_plus'] or abs(d.get('na_minus') or 0) != abs(b.get('rody_minus') or 0):
                rozne.append((bi, d, b))
        if rozne:
            bi, d, b = rozne[0]
            al('ALARM', 'Pieniadz swiata (rody): rozliczenia +%s/%s, a bilans liczy na plus %s, na minus %s (%d dob z rozjazdem)'
               % (d.get('na_plus'), d.get('na_minus'), b.get('rody_plus'), b.get('rody_minus'), len(rozne)), d.get('_nr'))
        zoldy = []
        for bi, d in rody:
            b = bild.get(bi)
            if b is not None and d.get('zold') is not None and b.get('zold') is not None and d['zold'] != b['zold']:
                zoldy.append((bi, d, b))
        if zoldy:
            bi, d, b = zoldy[0]
            al('uwaga', 'Pieniadz swiata (rody): zold naliczony %s, a w bilansie "W tym zold naliczony" %s (%d dob)'
               % (d.get('zold'), b.get('zold'), len(zoldy)), d.get('_nr'))
        if len(rody) >= 4:
            k = [d.get('rozliczen') for _, d in rody if d.get('rozliczen') is not None]
            med = mediana(k)
            if med and (max(k) - min(k)) > ROZLICZEN_ROZRZUT_PROC / 100.0 * med and (max(k) - min(k)) > 10:
                al('uwaga', 'Pieniadz swiata (rody): liczba rozliczen na dobe waha sie od %d do %d (mediana %d) - gra ma rozliczac '
                   'kazdy rod raz na dobe' % (min(k), max(k), med), rody[-1][1].get('_nr'))
        prz = ses.ser('Przeplywy osad')
        slepy = [(bi, d) for bi, d in prz if d.get('licznik_slepy')]
        if slepy:
            al('ALARM', 'Przeplywy osad: licznik zoldu nie widzial wyplat w %d dobach' % len(slepy), slepy[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in prz if d.get('potkniecia')]
        if potk:
            al('ALARM', 'Przeplywy osad: potkniecia licznikow w %d dobach (suma %d)'
               % (len(potk), sum(d.get('potkniecia') for _, d in potk)), potk[0][1].get('_nr'))
        for nazwa in ('Przeplywy osad (kasy miast)', 'Przeplywy osad (kasy zamkow)', 'Przeplywy osad (kiesy wsi)'):
            ser = ses.ser(nazwa)
            if 'wsi' not in nazwa:
                zero = [(bi, d) for bi, d in ser if d.get('reg_tickow') == 0]
                if zero:
                    al('ALARM', '%s: regulator kasy 0 tickow w %d dobach - regulator siedzi w reszcie'
                       % (nazwa, len(zero)), zero[0][1].get('_nr'))
                malo = [(bi, d) for bi, d in ser if d.get('zakupy_tickow') is not None and d.get('osad')
                        and d['zakupy_tickow'] < TICKI_OSAD_MIN_UDZIAL * d['osad']]
                if malo:
                    al('uwaga', '%s: konsumpcja policzona w %d z %d osad (doba %d; %d dob ponizej %.0f%%) - tick osad dlawiony?'
                       % (nazwa, malo[0][1]['zakupy_tickow'], malo[0][1]['osad'], malo[0][0] + 1, len(malo),
                          100 * TICKI_OSAD_MIN_UDZIAL), malo[0][1].get('_nr'))
            zle = []
            for bi, d in ser:
                re_, ruch = d.get('reszta'), d.get('ruch')
                if re_ is None or abs(re_) < RESZTA_KAS_MIN_ABS:
                    continue
                if not ruch or 100.0 * abs(re_) / (ruch + abs(re_)) > RESZTA_KAS_PROC_RUCHU:
                    zle.append((abs(re_), re_, bi, d.get('_nr')))
            if zle:
                naj = max(zle)
                al('uwaga', '%s: reszta [R] ponad %.0f%% ruchu w %d dobach, najwiecej %s w dobie %d'
                   % (nazwa, RESZTA_KAS_PROC_RUCHU, len(zle), skr(naj[1], True), naj[2] + 1), naj[3])
        sk = ses.ser('Skarbce')
        uj = [(bi, d) for bi, d in sk if d.get('ujemnych')]
        if uj:
            al('ALARM', 'Skarbce: skarbiec ponizej zera w %d dobach (np. %s: %s)'
               % (len(uj), uj[0][1].get('min_krolestwo'), uj[0][1].get('min_skarbiec')), uj[0][1].get('_nr'))

    @regula
    def r_ludzie():
        for w in ses.linie_tematu.get('Ludzie (pominiete)') or []:
            al('ALARM', 'Ludzie: pominiete wiersze pliku regionow: %s' % obetnij(w.tresc, 80), w.nr)
            break
        lu = ses.ser('Ludzie')
        potk = [(bi, d) for bi, d in lu if d.get('potkniecia')]
        if potk:
            al('ALARM', 'Ludzie: potkniecia ksiegi w %d dobach (np. %d)' % (len(potk), potk[0][1].get('potkniecia')),
               potk[0][1].get('_nr'))
        niesk = [(bi, d) for bi, d in lu if d.get('nieskalibrowana')]
        if niesk:
            al('uwaga', 'Ludzie: ludnosc nieskalibrowana w %d dobach (PopulationLaw wylaczone albo przed rentami)'
               % len(niesk), niesk[0][1].get('_nr'))
        ln = dict(ses.ser('Ludnosc'))
        for bi, d in lu:
            a, b = d.get('ludnosc_mln'), (ln.get(bi) or {}).get('mln')
            if a is not None and b is not None and abs(a - b) > LUDNOSC_ROZJAZD_MLN:
                al('uwaga', 'Ludzie: ludnosc %.3f mln wobec %.1f mln w linii "Ludnosc:" (doba %d)' % (a, b, bi + 1),
                   d.get('_nr'))
                break

    @regula
    def r_jednostka():
        # ogniwo 108: odcinek "hearth za ludzi dzis" w linii "Ludzie:" (PeopleUnit)
        lu = [(bi, d) for bi, d in ses.ser('Ludzie') if d.get('jednostka')]
        if not lu:
            return
        wyl = [(bi, d) for bi, d in lu if d.get('jednostka') == 'WYLACZONA']
        if wyl:
            al('uwaga', 'Ludzie: jednostka ludzi WYLACZONA (%d dob) - wsie oddaja hearth po stawkach gry (ustawienie People Unit '
               'Enabled)' % len(wyl), wyl[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in lu if (d.get('hz_potk') or 0) > 0]
        if potk:
            al('ALARM', 'Ludzie: potkniecia w odcinku "hearth za ludzi dzis" w %d dobach (np. %d)'
               % (len(potk), potk[0][1].get('hz_potk')), potk[0][1].get('_nr'))
        for klucz, opis in (('hz_niezgodne', 'niezgodne z formula gry i zostawione'),
                            ('hz_bez_k', 'bez przelicznika kultury (ludzie taborow)')):
            zle = [(bi, d) for bi, d in lu if (d.get(klucz) or 0) > 0]
            if zle:
                al('uwaga', 'Ludzie: "hearth za ludzi dzis" - %s: %d dob, najwiecej %d (inny mod albo inna wersja gry zmienia '
                   'te same liczby)' % (opis, len(zle), max(d.get(klucz) for _, d in zle)), zle[0][1].get('_nr'))
        czynne = [(bi, d) for bi, d in lu if d.get('jednostka') == 'czynna']
        if len(czynne) >= TABORY_ZERO_DOB and all((d.get('hz_tabory_ludzi') or 0) == 0 for _, d in czynne) \
                and any((d.get('tabory_wsi') or 0) > 0 for _, d in czynne):
            al('uwaga', 'Ludzie: "tabory wsi i lodzie ... za 0 ludzi" przez %d dob przy %s ludzi w taborach wsi - latka taborow '
               'nie jest wolana?' % (len(czynne), skr(czynne[-1][1].get('tabory_wsi'))), czynne[-1][1].get('_nr'))

    @regula
    def r_przyrost():
        # ogniwo 109: linia "Ludzie: przyrost naturalny" i ludnosc swiata
        jest109 = ma_ogniwo(ses, '109')
        pr = ses.ser('Ludzie (przyrost)')
        w = ses.fakty.get('model_dobrobytu')
        if w is not None:
            m = re.search(r'postfiksy w kolejnosci biegu: (.*?); prefiksy', w.tresc)
            lancuch = [x.strip() for x in m.group(1).split(' -> ')] if m else []
            if lancuch and 'PopulationLaw.GrowthPostfix' not in lancuch[-1]:
                al('ALARM', 'PopulationLaw: GrowthPostfix nie biegnie ostatni na CalculateHearthChange (ostatni: %s) - wynik '
                   'przyrostu moze byc nadpisany' % obetnij(lancuch[-1], 60), w.nr)
            if 'dopisuje PO naszej latce' in w.tresc:
                al('uwaga', 'PopulationLaw: czynny model dobrobytu dopisuje hearth PO naszej latce (wynik trzymaja limity) - %s'
                   % wycinek(w.tresc, 60), w.nr)
        if not pr:
            return
        wyl = [(bi, d) for bi, d in pr if d.get('wylaczony')]
        if wyl:
            al('uwaga', 'Ludzie: przyrost naturalny WYLACZONY w %d dobach - hearth wsi liczy gra (np. %s ludzi dzis)'
               % (len(wyl), skr(wyl[0][1].get('gra_ludzi'), True)), wyl[0][1].get('_nr'))
        poza = [(bi, d) for bi, d in pr if d.get('ludzi') is not None
                and not (PRZYROST_MIN_LUDZI <= d['ludzi'] <= PRZYROST_MAKS_LUDZI)]
        if poza:
            naj = max(poza, key=lambda x: abs(x[1]['ludzi']))
            al('ALARM', 'Ludzie: przyrost naturalny poza przedzialem %d-%d ludzi dziennie w %d z %d dob (np. %s w dobie %d; '
               'oczekiwane 250-600)' % (PRZYROST_MIN_LUDZI, PRZYROST_MAKS_LUDZI, len(poza), len(pr),
                                        skr(naj[1]['ludzi'], True), naj[0] + 1), naj[1].get('_nr'))
        gz = [(bi, d) for bi, d in pr if d.get('g_srednio') is not None
              and not (PRZYROST_G_MIN <= d['g_srednio'] <= PRZYROST_G_MAKS)]
        if gz and not poza:
            al('uwaga', 'Ludzie: przyrost naturalny - "g srednio" %s%% rocznie poza %.1f-%.1f w %d dobach (oczekiwane 0.2-0.5)'
               % (skr(gz[0][1]['g_srednio'], True), PRZYROST_G_MIN, PRZYROST_G_MAKS, len(gz)), gz[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in pr if (d.get('potk_rachunek') or 0) + (d.get('potk_linia') or 0) > 0]
        if potk:
            al('ALARM', 'Ludzie: przyrost naturalny - potkniecia (rachunek wsi %s, linia dobowa %s) w %d dobach'
               % (potk[0][1].get('potk_rachunek'), potk[0][1].get('potk_linia'), len(potk)), potk[0][1].get('_nr'))
        rz = [(bi, d) for bi, d in pr if d.get('reszta') is not None and abs(d['reszta']) > PRZYROST_RESZTA_HEARTH]
        if rz:
            naj = max(rz, key=lambda x: abs(x[1]['reszta']))
            plus = naj[1]['reszta'] > 0
            al('uwaga', 'Ludzie: przyrost naturalny - RESZTA hearth wsi ponad %.0f na dobe w %d dobach, najwiecej %s (doba %d) - %s'
               % (PRZYROST_RESZTA_HEARTH, len(rz), skr(naj[1]['reszta'], True), naj[0] + 1,
                  'hearth z niczego: inwestycje BetterEconomy (13 kluczy nie zamknietych)?' if plus else
                  'hearth w nicosc: rabunki, marsz armii (spalona wies nie odrasta do kroku 4)'), naj[1].get('_nr'))
        uch = [(bi, d) for bi, d in pr if d.get('uch_ludzi') is not None and d.get('ludzi') is not None
               and d['uch_ludzi'] > max(0, d['ludzi'])]
        if uch:
            naj = max(uch, key=lambda x: x[1]['uch_ludzi'])
            al('uwaga', 'Ludzie: powrot uchodzcow %s ludzi dziennie w %s wsiach przy dnie - wiecej niz caly przyrost naturalny '
               '(%s); regula "+0.5 hearth ponizej 40" to ludzie z niczego do kroku 4 (%d dob)'
               % (skr(naj[1]['uch_ludzi'], True), skr(naj[1].get('uch_wsi')), skr(naj[1]['ludzi'], True), len(uch)),
               naj[1].get('_nr'))
        bezk = [(bi, d) for bi, d in pr if (d.get('bez_k') or 0) > 0]
        if bezk:
            al('uwaga', 'Ludzie: przyrost naturalny - wsi bez przelicznika ludzi: %d (ich przyrost nie wchodzi do sumy ludzi)'
               % bezk[-1][1].get('bez_k'), bezk[-1][1].get('_nr'))
        zam_ = [(bi, d) for bi, d in pr if d.get('zamrozone') == 1 and d.get('miasta_mln') is not None]
        if len(zam_) >= 2:
            stany = [d['miasta_mln'] for _, d in zam_]
            if max(stany) - min(stany) > 0.0015:
                al('ALARM', 'Ludzie: ludnosc miast jest stanem (zamrozona), a zmienia sie: %.3f -> %.3f mln (od %.3f do %.3f)'
                   % (stany[0], stany[-1], min(stany), max(stany)), zam_[-1][1].get('_nr'))
        odm = [(bi, d) for bi, d in pr if d.get('zamrozone') == 0 and not d.get('miasta_nieskalibrowane')]
        if odm:
            al('uwaga', 'Ludzie: ludnosc miast liczona z dobrobytu (Town People Frozen wylaczone) w %d dobach' % len(odm),
               odm[0][1].get('_nr'))
        if not jest109:
            return
        # ludnosc swiata: po ogniwie 109 koniec wzrostu o 0.2-0.35% dziennie
        lu = ses.ser('Ludzie')
        skoki = []
        for bi, d in lu:
            zm, mln = d.get('ludnosc_zm'), d.get('ludnosc_mln')
            if zm is not None and mln:
                pc = 100.0 * abs(zm) / (mln * 1e6)
                if pc > LUDNOSC_MAKS_ZMIANA_PROC:
                    skoki.append((pc, zm, bi, d.get('_nr')))
        if skoki:
            naj = max(skoki)
            al('ALARM', 'Ludzie: ludnosc swiata zmienia sie o ponad %.2f%% dziennie w %d dobach, najwiecej %s ludzi = %.3f%% '
               '(doba %d) - przy ogniwie 109 ma stac' % (LUDNOSC_MAKS_ZMIANA_PROC, len(skoki), skr(naj[1], True), naj[0],
                                                         naj[2] + 1), naj[3])
        ln = [(bi, d) for bi, d in ses.ser('Ludnosc') if d.get('mln')]
        if len(ln) >= 2 and not skoki:
            a, b = ln[0], ln[-1]
            dob = max(1, b[0] - a[0])
            pc = 100.0 * abs(b[1]['mln'] - a[1]['mln']) / a[1]['mln'] / dob
            if pc > LUDNOSC_MAKS_ZMIANA_PROC:
                al('ALARM', '"Ludnosc:" zmienia sie o %.3f%% dziennie (%.1f -> %.1f mln w %d dob; prog %.2f%%) - przy ogniwie '
                   '109 ma stac' % (pc, a[1]['mln'], b[1]['mln'], dob, LUDNOSC_MAKS_ZMIANA_PROC), b[1].get('_nr'))

    @regula
    def r_paser():
        pa = ses.ser('Paser')
        if not pa:
            return
        kurek = [(bi, d) for bi, d in pa if d.get('kurek_otwarty')]
        if kurek:
            al('ALARM', 'Paser: KUREK OTWARTY - gra dopisuje bandom i kryjowkom zloto z niczego (%d dob, np. %s zl)'
               % (len(kurek), skr(kurek[0][1].get('zabl_razem'))), kurek[0][1].get('_nr'))
        wyl = [(bi, d) for bi, d in pa if d.get('skup_wylaczony')]
        if wyl:
            al('uwaga', 'Paser: skup lupu WYLACZONY w ustawieniach (%d dob)' % len(wyl), wyl[0][1].get('_nr'))
        zyw = [(bi, d) for bi, d in pa if d.get('kurek_zywnosci')]
        if zyw:
            al('uwaga', 'Paser: kurek zywnosci z niczego OTWARTY w ustawieniach (%d dob) - gra daje zywnosc kazdej nowej bandzie, '
               'paser jej nie bierze' % len(zyw), zyw[0][1].get('_nr'))
        obieg = [(bi, d) for bi, d in pa if d.get('obieg_wylaczony')]
        if obieg:
            al('uwaga', 'Paser: OBIEG KAS kryjowek WYLACZONY (%d dob) - kiesy startowe nowych band z niczego: %s zl'
               % (len(obieg), skr(sum(d.get('start_z_niczego') or 0 for _, d in obieg))), obieg[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in pa if (d.get('potk_razem') or 0) > 0]
        if potk:
            d = potk[0][1]
            al('ALARM', 'Paser: potkniecia (skup %s, kryjowki %s, zycie %s, awanse %s) w %d dobach'
               % (d.get('potk_skup'), d.get('potk_kryjowki'), skr(d.get('potk_zycie')), skr(d.get('potk_awanse')), len(potk)),
               d.get('_nr'))
        # decyzja 06.10: paser dla 100% band - kazda banda z ladunkiem ma miec pasera i sprzedac ladunek
        bez = [(bi, d) for bi, d in pa if (d.get('bez_pasera') or 0) > 0]
        if bez:
            naj = max(bez, key=lambda x: x[1]['bez_pasera'])
            al('ALARM', 'Paser: "z paserem" mniej niz "band z ladunkiem" w %d z %d dob, najwiecej %d z %d band bez pasera (doba %d) - '
               'paser ma obslugiwac 100%% band' % (len(bez), len(pa), naj[1]['bez_pasera'], naj[1]['z_ladunkiem'], naj[0] + 1),
               naj[1].get('_nr'))
        ponad = [(bi, d) for bi, d in pa if d.get('sprzedalo') is not None and d.get('z_ladunkiem') is not None
                 and d['sprzedalo'] > d['z_ladunkiem']]
        if ponad:
            al('ALARM', 'Paser: "sprzedalo" %d wiecej niz "band z ladunkiem" %d (doba %d) - licznik liczy bande dwa razy'
               % (ponad[0][1]['sprzedalo'], ponad[0][1]['z_ladunkiem'], ponad[0][0] + 1), ponad[0][1].get('_nr'))
        ile, pocz = najdluzsza_seria([(d.get('zostalo') or 0) > 0 for _, d in pa])
        if ile >= PASER_ZOSTALO_DOB:
            ost = pa[pocz + ile - 1][1]
            al('ALARM', 'Paser: "z ladunkiem zostalo" ponad 0 przez %d dob z rzedu (od doby %d; ostatnio %d band, %s szt.: kasy '
               'miast przy rezerwie %s, brak otwartego miasta %s)' % (ile, pa[pocz][0] + 1, ost.get('zostalo'),
                                                                      skr(ost.get('zostalo_szt')), skr(ost.get('zost_rezerwa')),
                                                                      skr(ost.get('zost_brak_miasta'))), pa[pocz][1].get('_nr'))
        # ksiega kas osad ma liczyc ten sam skup i te same wydatki na zycie
        km = dict(ses.ser('Przeplywy osad (kasy miast)'))
        kz = dict(ses.ser('Przeplywy osad (kasy zamkow)'))
        skup_zle, zycie_zle = [], []
        for bi, d in pa:
            k = km.get(bi)
            if k is None:
                continue
            z = kz.get(bi) or {}
            if d.get('za_zl') is not None:
                skup = (k.get('paser_skup') or 0) + (z.get('paser_skup') or 0)
                if abs(skup) != d['za_zl']:
                    skup_zle.append((bi, d, skup))
            if d.get('zycie_razem') is not None:
                zycie = (k.get('zycie_band') or 0) + (z.get('zycie_band') or 0)
                if zycie != d['zycie_razem']:
                    zycie_zle.append((bi, d, zycie))
        if skup_zle:
            bi, d, skup = skup_zle[0]
            al('ALARM', 'Paser: skup za %d zl, a ksiega kas miast liczy "paser band (skup lupu)" %+d (%d dob z rozjazdem)'
               % (d['za_zl'], skup, len(skup_zle)), d.get('_nr'))
        if zycie_zle:
            bi, d, zycie = zycie_zle[0]
            al('ALARM', 'Paser: zycie w miastach %d zl (bandy %s + kryjowki %s), a ksiega kas liczy "bandy i kryjowki (zycie w '
               'miastach)" %+d (%d dob z rozjazdem)' % (d['zycie_razem'], skr(d.get('zycie_bandy')), skr(d.get('zycie_kryjowki')),
                                                       zycie, len(zycie_zle)), d.get('_nr'))
        wy = ses.ser('Wyrzutki')
        if len(wy) >= PASER_MIN_DOB_BEZ_AWANSOW and all((d.get('aw_paser') or 0) == 0 for _, d in wy):
            al('uwaga', 'Wyrzutki: "awanse: od pasera" = 0 przez %d dob mimo ogniwa 106 (bandy nie maja za co kupowac?)'
               % len(wy), wy[-1][1].get('_nr'))
        wej = [(bi, d) for bi, d in pa if (d.get('wejsc') or 0) > 0 and not d.get('kurek_otwarty')]
        if len(wej) >= 3 and all((d.get('zabl_razem') or 0) == 0 for _, d in wej):
            al('uwaga', 'Paser: wejscia band do kryjowek w %d dobach, a "zloto z niczego zablokowane" stale 0' % len(wej),
               wej[-1][1].get('_nr'))
        ost = pa[-1][1]
        if (ost.get('juki_zywnosc') or 0) > 0 and not ost.get('kurek_zywnosci'):
            al('uwaga', 'Paser: w jukach band "zywnosc poza skupem" %d szt. (przy zamknietym kurku zywnosci ma byc 0)'
               % ost['juki_zywnosc'], ost.get('_nr'))

    @regula
    def r_zold():
        zo = ses.ser('Zold')
        if ma_ogniwo(ses, '107') and len(ses.pelne()) >= 1:
            latki = [w for w in ws if w.pref == 'SoldierPay' and 'latki modelu finansow' in w.tresc]
            if not latki:
                al('ALARM', 'SoldierPay: brak linii "latki modelu finansow" mimo %d dob - latki na model nie zalozone?'
                   % len(ses.pelne()))
            if not zo and len(ses.pelne()) >= 2:
                al('ALARM', 'SoldierPay wpiete, a nie ma linii "Zold:" w %d dobach' % len(ses.pelne()))
        if not zo:
            return
        # decyzja 06.10: tarcza zoldu w kasach miast WLACZONA (ogniwo 107) - bez niej regulator kas kasuje ok. 81% zoldu miast
        bez_tarczy = [(bi, d) for bi, d in zo if not d.get('tarcza_wl')]
        if bez_tarczy:
            al('uwaga', 'Zold: tarcza zoldu w kasach miast wylaczona w %d z %d dob (od ogniwa 107 domyslnie wlaczona - ustawienie '
               'Town Wage Shield zapisane w MCM?)' % (len(bez_tarczy), len(zo)), bez_tarczy[0][1].get('_nr'))
        z_tarcza = [(bi, d) for bi, d in zo if d.get('tarcza_wl') and (d.get('do_miast') or 0) > 0]
        if len(z_tarcza) >= 2 and 'soldierpay_tarcza' not in ses.fakty:
            al('uwaga', 'Zold: tarcza wlaczona i zold plynie do kas miast (%d dob), a nie ma linii "SoldierPay: tarcza zoldu w '
               'kasach miast ... regulator kasy z ..." - latka na regulator nie zalozona?' % len(z_tarcza),
               z_tarcza[0][1].get('_nr'))
        slepe = [(bi, d) for bi, d in zo if (d.get('slepe') or 0) > 0]
        if slepe:
            al('ALARM', 'Zold: rody z pusta kiesa i nieznanym saldem w %d dobach (np. %d rodow, %s zl)'
               % (len(slepe), slepe[0][1].get('slepe'), skr(slepe[0][1].get('slepe_zl'))), slepe[0][1].get('_nr'))
        potk = [(bi, d) for bi, d in zo if (d.get('potk_powtorzone') or 0) + (d.get('potk_wyjatki') or 0) > 0]
        if potk:
            al('ALARM', 'Zold: potkniecia (powtorzone wyplaty %s, wyjatki %s) w %d dobach'
               % (potk[0][1].get('potk_powtorzone'), potk[0][1].get('potk_wyjatki'), len(potk)), potk[0][1].get('_nr'))
        roz = []
        for bi, d in zo:
            r, z = d.get('rozjazd'), d.get('zeszlo_razem')
            if r is not None and z and abs(r) > max(10, ZOLD_ROZJAZD_PROC / 100.0 * z):
                roz.append((abs(r), r, bi, d.get('_nr')))
        if roz:
            naj = max(roz)
            al('uwaga', 'Zold: "z kies zeszlo" rozni sie od sumy przekazane + nieprzekazane o %s (doba %d; %d dob ponad %.0f%%)'
               % (skr(naj[1], True), naj[2] + 1, len(roz), ZOLD_ROZJAZD_PROC), naj[3])
        if len(zo) >= 3:
            a, b = zo[0][1].get('sak_sieroty'), zo[-1][1].get('sak_sieroty')
            if a is not None and b is not None and b > a and b > 0:
                al('uwaga', 'Zold: sakiewki po partiach, ktorych juz nie ma, rosna: %s -> %s' % (skr(a), skr(b)),
                   zo[-1][1].get('_nr'))
            a, b = zo[0][1].get('dlug_rodow'), zo[-1][1].get('dlug_rodow')
            if a is not None and b is not None and b > a and b >= 5:
                al('uwaga', 'Zold: rodow z brakiem zapisanym jako dlug wobec korony przybywa: %d -> %d' % (a, b),
                   zo[-1][1].get('_nr'))
        if len(zo) > SAKIEWKA_ROSNIE_DOB:
            ogon = [d.get('sak_najwieksza') for _, d in zo[-(SAKIEWKA_ROSNIE_DOB + 1):]]
            if all(v is not None for v in ogon) and all(ogon[i] < ogon[i + 1] for i in range(len(ogon) - 1)):
                al('uwaga', 'Zold: najwieksza sakiewka rosnie %d dob z rzedu: %s -> %s'
                   % (SAKIEWKA_ROSNIE_DOB, skr(ogon[0]), skr(ogon[-1])), zo[-1][1].get('_nr'))
        prz = dict(ses.ser('Przeplywy osad'))
        roz = []
        for bi, d in zo:
            k = prz.get(bi)
            nal = d.get('naliczony_razem')
            if k is None or nal is None or not k.get('zold_razem'):
                continue
            ks = (k.get('zold_rody') or 0) + (k.get('zold_garnizony') or 0)
            if ks and abs(nal - ks) > ZOLD_KSIEGA_ROZJAZD_PROC / 100.0 * ks:
                roz.append((abs(nal - ks), nal, ks, bi, d.get('_nr')))
        if roz:
            naj = max(roz)
            al('uwaga', 'Zold: naliczony %s wobec licznika ksiegi pieniadza %s (partie rodow + garnizony), doba %d'
               % (skr(naj[1]), skr(naj[2]), naj[3] + 1), naj[4])
        # uklad 102b: bilans podaje, ile zoldu SoldierPay oddal do obiegu - ma to byc suma z linii "Zold:"
        bil = dict(ses.ser('Pieniadz swiata (bilans)'))
        roz = []
        for bi, d in zo:
            b = bil.get(bi)
            if b is None or b.get('oddany') is None:
                continue
            czesci = [d.get('do_sakiewek'), d.get('do_miast'), d.get('do_zamkow')]
            if any(v is None for v in czesci):
                continue
            razem = sum(czesci)
            if abs(razem - b['oddany']) > max(10, ZOLD_KSIEGA_ROZJAZD_PROC / 100.0 * max(razem, b['oddany'])):
                roz.append((abs(razem - b['oddany']), razem, b['oddany'], bi, d.get('_nr')))
        if roz:
            naj = max(roz)
            al('uwaga', 'Zold: do sakiewek i kas osad %s, a bilans "zold oddany do obiegu przez SoldierPay" %s (doba %d; %d dob '
               'z rozjazdem ponad %.0f%%)' % (skr(naj[1]), skr(naj[2]), naj[3] + 1, len(roz), ZOLD_KSIEGA_ROZJAZD_PROC), naj[4])
        zw = ses.ser('Korona (zwrot zoldu)')
        pusty = [(bi, d) for bi, d in zw if (d.get('pusty') or 0) > 0]
        if pusty:
            al('uwaga', 'Korona: zwrot zoldu - pusty skarbiec w %d krolestwach (doba %d)'
               % (pusty[-1][1].get('pusty'), pusty[-1][0] + 1), pusty[-1][1].get('_nr'))
        potk = [(bi, d) for bi, d in zw if (d.get('potkniecia') or 0) > 0]
        if potk:
            al('ALARM', 'Korona: zwrot zoldu - wyjatek przy %d krolestwach' % potk[0][1].get('potkniecia'),
               potk[0][1].get('_nr'))
        sk = ses.ser('Skarbce')
        if ma_ogniwo(ses, '107') and not zw and any((d.get('wojna') or 0) > 0 for _, d in sk) and len(ses.pelne()) >= 2:
            al('uwaga', 'krolestwa sa w wojnie, a nie ma linii "Korona: ... zwrot zoldu" (CrownWageRefundEnabled wylaczone?)')
        sa = ses.ser('Sakiewka ludzi')
        zle = []
        for i in range(1, len(sa)):
            p, d = sa[i - 1][1], sa[i][1]
            if sa[i][0] != sa[i - 1][0] + 1:
                continue
            if None in (p.get('razem'), d.get('razem'), d.get('wplynelo'), d.get('wyszlo')):
                continue
            r = (d['razem'] - p['razem']) - (d['wplynelo'] - d['wyszlo'])
            if r != 0:
                zle.append((abs(r), r, sa[i][0], d.get('_nr')))
        if zle:
            naj = max(zle)
            al('uwaga', 'Sakiewka ludzi: stan zmienil sie inaczej niz wplynelo - wyszlo o %s (doba %d; %d dob z rozjazdem)'
               % (skr(naj[1], True), naj[2] + 1, len(zle)), naj[3])

    for f in reguly:
        try:
            f()
        except Exception as e:
            ses.uwagi.append('regula alarmu %s: blad wewnetrzny (%s: %s)' % (f.__name__, type(e).__name__, e))
    ses.alarmy.sort(key=lambda a: (0 if a[0] == 'ALARM' else 1, a[1] if a[1] is not None else 0))


# =====================================================================================================================
# KONTROLE OGNIW - "po czym poznac w logu" z docs\paczki\<ogniwo>.md i z wpisow 100-102 CHANGELOG
# Kazda kontrola: (status, co sprawdzono, wynik z liczba z logu, numer linii logu albo None).
# Status: OK = jest tak, jak opisuje paczka; UWAGA = jest inaczej (albo liczba poza oczekiwanym przedzialem);
# BRAK DANYCH = w logu nie ma linii, z ktorej da sie to odczytac. "pomiar:" = paczka kaze liczbe zmierzyc, nie ma progu.
# =====================================================================================================================
OK, UW, BD = 'OK', 'UWAGA', 'BRAK DANYCH'


class Kontrole(object):
    def __init__(self):
        self.lista = []

    def dodaj(self, status, co, wynik, nr=None):
        self.lista.append((status, co, wynik, nr))

    def gdy(self, warunek, co, wynik, nr=None):
        self.dodaj(OK if warunek else UW, co, wynik, nr)

    def brak(self, co, czego):
        self.dodaj(BD, co, czego, None)


def _wart(ser, klucz):
    """Liczby jednej kolumny szeregu (bez pustych i bez tekstow)."""
    return [d.get(klucz) for _, d in ser if isinstance(d.get(klucz), (int, float)) and not isinstance(d.get(klucz), bool)]


def _pierw_ost(ser, klucz):
    """(pierwsza, ostatnia, doba pierwszej, doba ostatniej) albo None."""
    z = [(bi, d.get(klucz)) for bi, d in ser if d.get(klucz) is not None]
    return (z[0][1], z[-1][1], z[0][0] + 1, z[-1][0] + 1) if z else None


def _zakres(v):
    """'mediana M (od A do B)' dla listy liczb."""
    if not v:
        return '-'
    return '%s (od %s do %s)' % (skr(mediana(v)), skr(min(v)), skr(max(v)))


def _start(K, ses, nr, co, wymagane=(), zakazane=('BRAK', 'NIE wpiet')):
    """Kontrola linii startowej ogniwa: jest, ma wymagane slowa, nie ma BRAK / NIE wpieta."""
    o = ogniwo(ses, nr)
    if not o or not o['jest']:
        K.brak(co, 'nie ma linii startowej (%s)' % (o['poznac'].split(';')[0] if o else nr))
        return None
    t = o['wpis'].tresc
    brakuje = [x for x in wymagane if x not in t]
    zle = [x for x in zakazane if re.search(r'\b' + re.escape(x), t)]
    K.gdy(not brakuje and not zle, co, ('brakuje: %s; ' % ', '.join(brakuje) if brakuje else '') +
          ('w linii jest %s; ' % ', '.join(zle) if zle else '') + wycinek(t, 110), o['wpis'].nr)
    return o['wpis']


def _k100(K, ses):
    _start(K, ses, '100', 'start: latka dowozu na targ miasta wpieta', ['wpieta'])
    dw, sk = ses.ser('Dowoz'), ses.ser('Dowoz (skutki)')
    if dw:
        wys = _wart(dw, 'wyslane')
        K.gdy(bool(wys) and mediana(wys) > 0, 'co dobe tabory wsi zamkowych jada na targ miasta',
              'wyslane na dobe: mediana %s w %d dobach; w drodze ostatnio %s' % (_zakres(wys), len(dw),
                                                                               skr(dw[-1][1].get('w_drodze'))), dw[-1][1].get('_nr'))
        d = dw[-1][1]
        K.gdy((d.get('bez_targu') or 0) == 0, 'kazda wies zamkowa ma targ miasta',
              'targ we wlasnym krolestwie %s, w obcym %s, bez targu %s; dzis zostalo przy zamku: targ za daleko %s, wojna %s'
              % (skr(d.get('targ_wlasny')), skr(d.get('targ_obcy')), skr(d.get('bez_targu')), skr(d.get('za_daleko')),
                 skr(d.get('wojna'))), d.get('_nr'))
        K.dodaj(OK, 'pomiar: rozbite tabory wsi (ladunek idzie do jukow band)', 'razem %s w %d dobach, w tym przez bandy %s'
                % (skr(sum(_wart(dw, 'rozbite'))), len(dw), skr(sum(_wart(dw, 'przez_bandy')))), d.get('_nr'))
    else:
        K.brak('co dobe tabory wsi zamkowych jada na targ miasta', 'nie ma linii "Dowoz:"')
    if sk:
        a, d = sk[0][1], sk[-1][1]
        za, zm = d.get('zatk_zamkowe_proc'), d.get('zatk_miejskie_proc')
        if za is not None and zm is not None:
            K.gdy(za - zm <= ZATKANE_ROZNICA_PP, 'zatkane magazyny: wsie zamkowe nie gorzej niz miejskie',
                  '%.1f%% zamkowych wobec %.1f%% miejskich w ostatniej dobie (pierwsza doba: %s%% / %s%%)'
                  % (za, zm, skr(a.get('zatk_zamkowe_proc')), skr(a.get('zatk_miejskie_proc'))), d.get('_nr'))
        K.gdy((d.get('pusty_spichlerz') or 0) == 0, 'zamki nie gloduja bez plonu z polki',
              'bilans zywnosci ujemny w %s zamkach, bez polki ujemny %s, pusty spichlerz %s (ostatnia doba)'
              % (skr(d.get('zamki_glodne')), skr(d.get('bez_polki_ujemny')), skr(d.get('pusty_spichlerz'))), d.get('_nr'))
        K.gdy((d.get('ponizej_progu') or 0) <= 5, 'kasy miast udzwigna skup plonu od wsi',
              'kasy miast razem %s -> %s, ponizej progu rent %s miast' % (skr(a.get('kasy_miast')), skr(d.get('kasy_miast')),
                                                                          skr(d.get('ponizej_progu'))), d.get('_nr'))
    else:
        K.brak('zatkane magazyny: wsie zamkowe nie gorzej niz miejskie', 'nie ma linii "Dowoz (skutki):"')
    po = _pierw_ost(ses.ser('Ruda'), 'bez_towaru')
    if po:
        K.gdy(po[1] <= po[0], 'ruda: miast bez towaru ubywa', '%s -> %s miast bez rudy (doby %d-%d)' % (po[0], po[1], po[2], po[3]),
              ses.ser('Ruda')[-1][1].get('_nr'))


def _k101(K, ses):
    w = ses.fakty.get('woz')
    if w is not None:
        m = re.search(r'x(\d+(?:[.,]\d+)?)', w.tresc)
        K.gdy(bool(m) and abs(float(m.group(1).replace(',', '.')) - 2.0) < 0.01, 'start: udzwig taboru wsi zamkowej x2',
              wycinek(w.tresc, 110), w.nr)
    else:
        K.brak('start: udzwig taboru wsi zamkowej x2', 'nie ma linii "MarketRoad: woz - udzwig taborow ..."')
    sk = ses.ser('Dowoz (skutki)')
    if sk:
        d = sk[-1][1]
        za, zm = d.get('zatk_zamkowe_proc'), d.get('zatk_miejskie_proc')
        if za is not None and zm is not None:
            K.gdy(za - zm <= ZATKANE_ROZNICA_PP, 'zatkane magazyny wsi zamkowych jak miejskich (ok. 8%)',
                  '%.1f%% zamkowych wobec %.1f%% miejskich w ostatniej dobie; w szeregu zamkowe %s'
                  % (za, zm, _zakres(_wart(sk, 'zatk_zamkowe_proc'))), d.get('_nr'))
    else:
        K.brak('zatkane magazyny wsi zamkowych jak miejskich (ok. 8%)', 'nie ma linii "Dowoz (skutki):"')
    for nazwa in ('Ruda', 'Drewno'):
        ser = [(bi, d) for bi, d in ses.ser(nazwa) if bi > 0]
        po = _pierw_ost(ser, 'wsie')
        if po:
            K.gdy(po[1] <= po[0] * 1.05, '%s: zapas lezacy we wsiach nie rosnie' % nazwa,
                  '%s -> %s ladunkow we wsiach (doby %d-%d); wsie dopisaly na dobe: %s'
                  % (skr(po[0]), skr(po[1]), po[2], po[3], _zakres(_wart(ser, 'dopisaly'))), ser[-1][1].get('_nr'))
        else:
            K.brak('%s: zapas lezacy we wsiach nie rosnie' % nazwa, 'nie ma linii "%s:" z pozycja "wsie" (ksiega wpisu 102)' % nazwa)
    dw = ses.ser('Dowoz')
    if dw:
        K.dodaj(OK, 'pomiar: rozbite tabory (wiekszy woz = wiecej do stracenia)', 'razem %s w %d dobach, w tym przez bandy %s'
                % (skr(sum(_wart(dw, 'rozbite'))), len(dw), skr(sum(_wart(dw, 'przez_bandy')))), dw[-1][1].get('_nr'))


def _k102(K, ses):
    f = ses.fakty
    w = _start(K, ses, '102', 'start: ksiega pieniadza - liczniki wpiete, bez "BRAK"',
               ['konsumpcja mieszkancow', 'regulator kasy w', 'zold'])
    if w is not None:
        m = re.search(r'regulator kasy w (\d+) modelach', w.tresc)
        K.gdy(bool(m) and int(m.group(1)) == 3, 'regulator kasy liczony w 3 modelach (gra, BK, BetterEconomy)',
              ('w %s modelach' % m.group(1)) if m else 'liczby modeli nie ma w linii startowej', w.nr)
    pelne = len(ses.pelne())
    jest = [('"Przeplywy osad: modele czynne"', 'modele' in f), ('"Ludzie: plik wszystkich regionow"', 'csv' in f),
            ('"Pieniadz swiata: ... Pierwsza doba ksiegi"', any('Pierwsza doba ksiegi' in x.tresc for x in
                                                               ses.linie_tematu.get('Pieniadz swiata') or []))]
    if ses.linie_tematu.get('Pieniadz swiata') or ses.linie_tematu.get('Ludzie'):
        K.gdy(all(j for _, j in jest), 'pierwsza doba: trzy linie rozruchu ksiag',
              ', '.join('%s %s' % (n, 'jest' if j else 'BRAK') for n, j in jest))
        tem = ['Pieniadz swiata', 'Pieniadz swiata (bilans)', 'Przeplywy osad', 'Przeplywy osad (kasy miast)',
               'Przeplywy osad (kasy zamkow)', 'Przeplywy osad (kiesy wsi)', 'Ludzie', 'Ludzie (regiony)']
        dob = [(n, len(ses.ser(n))) for n in tem]
        chce = dict((n, pelne if n in ('Pieniadz swiata', 'Ludzie', 'Ludzie (regiony)') else max(0, pelne - 1)) for n in tem)
        zle = [(n, c) for n, c in dob if c < chce[n]]
        K.gdy(not zle, 'od drugiej doby komplet: 6 linii pieniadza i 2 linie ludzi',
              ('komplet w %d dobach' % pelne) if not zle else 'niepelne: ' + ', '.join('%s %d z %d dob' % (n, c, chce[n])
                                                                                    for n, c in zle))
    else:
        K.brak('od drugiej doby komplet: 6 linii pieniadza i 2 linie ludzi', 'nie ma linii "Pieniadz swiata:" ani "Ludzie:"')
    for nazwa, ile in (('Przeplywy osad (kasy miast)', 'ok. 97 miast'), ('Przeplywy osad (kasy zamkow)', 'do 130 zamkow')):
        ser = ses.ser(nazwa)
        t, osad = _wart(ser, 'zakupy_tickow'), _wart(ser, 'osad')
        if t and osad:
            K.gdy(mediana(t) >= TICKI_OSAD_MIN_UDZIAL * mediana(osad), '%s: konsumpcja policzona w kazdej osadzie (%s)'
                  % (nazwa.split('(')[1].rstrip(')'), ile), 'tickow osad: mediana %s przy %s osadach; regulator kasy: %s tickow'
                  % (_zakres(t), skr(mediana(osad)), _zakres(_wart(ser, 'reg_tickow'))), ser[-1][1].get('_nr'))
    prz = ses.ser('Przeplywy osad')
    if prz:
        zr = _wart(prz, 'zold_razem')
        K.dodaj(OK, 'pomiar: licznik zoldu (porownac z CSV CrashScribe)',
                'zold razem na dobe: mediana %s' % _zakres(zr), prz[-1][1].get('_nr'))
        zp = _wart(prz, 'zniklo_proc')
        K.gdy(bool(zp) and 10.0 <= mediana(zp) <= 20.0, 'z utargu taborow wsi znika ok. 15% (znana dziura gry, krok K7)',
              'zniklo: mediana %s%% utargu oddanego we wsi, %s zlota na dobe' % (_zakres(zp), _zakres(_wart(prz, 'zniklo'))),
              prz[-1][1].get('_nr'))
        potk = sum(_wart(prz, 'potkniecia'))
        K.gdy(potk == 0, 'potkniecia licznikow ksiegi pieniadza: brak', 'suma %d w %d dobach' % (potk, len(prz)),
              prz[-1][1].get('_nr'))
    else:
        K.brak('licznik zoldu, utarg taborow, potkniecia licznikow', 'nie ma linii "Przeplywy osad:" (pojawia sie od drugiej doby)')
    dr, ru = [(bi, d) for bi, d in ses.ser('Drewno') if bi > 0], [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
    bd = _wart(dr, 'budowy')
    if bd:
        K.gdy(1200 <= mediana(bd) <= 2200, 'Drewno: budowy biora ok. 1500-1900 ladunkow na dobe',
              'budowy: mediana %s; "bez wyjasnienia": mediana %s (ok. +2100 to znana dosypka RealisticBannerlord)'
              % (_zakres(bd), _zakres(_wart(dr, 'bez_wyjasnienia'))), dr[-1][1].get('_nr'))
    br = _wart(ru, 'budowy')
    if br:
        K.gdy(max(br) == 0, 'Ruda: budowy nie kupuja rudy (pozycja 0)', 'budowy: najwiecej %s na dobe' % skr(max(br)),
              ru[-1][1].get('_nr'))
    lu, ln = ses.ser('Ludzie'), dict(ses.ser('Ludnosc'))
    pary = [(d.get('ludnosc_mln'), (ln.get(bi) or {}).get('mln')) for bi, d in lu]
    pary = [(a, b) for a, b in pary if a is not None and b is not None]
    if pary:
        naj = max(abs(a - b) for a, b in pary)
        K.gdy(naj <= LUDNOSC_ROZJAZD_MLN, '"Ludzie: ludnosc" zgodna z linia "Ludnosc:"',
              'najwieksza roznica %.3f mln w %d dobach (ostatnio %.3f wobec %.1f mln)' % (naj, len(pary), pary[-1][0], pary[-1][1]),
              lu[-1][1].get('_nr'))
    elif lu:
        K.brak('"Ludzie: ludnosc" zgodna z linia "Ludnosc:"', 'brak liczby ludnosci w jednej z linii')
    bil = ses.ser('Pieniadz swiata (bilans)')
    if bil and f.get('uklad_ksiegi') == UKLAD_102:
        st = [d['reszta'] / float(d['zold']) for _, d in bil if d.get('zold') and d.get('reszta') is not None]
        if st:
            K.dodaj(UW, 'bilans swiata: reszta [R] zawyzona o zold (naprawia 102b)',
                    'reszta / zold: mediana %.2f w %d dobach; reszta %s, zold %s' % (mediana(st), len(st),
                                                                                    _zakres(_wart(bil, 'reszta')),
                                                                                    _zakres(_wart(bil, 'zold'))),
                    bil[-1][1].get('_nr'))


def _k102b(K, ses):
    _start(K, ses, '102b', 'start: licznik rozliczen rodow wpiety', ['rozliczenie rodow'])
    rody, bil = ses.ser('Pieniadz swiata (rody)'), ses.ser('Pieniadz swiata (bilans)')
    pelne = len(ses.pelne())
    if not rody:
        K.brak('co dobe linia "Pieniadz swiata (rody):"', 'nie ma jej w logu (pojawia sie od drugiej doby ksiegi)')
    else:
        K.gdy(len(rody) >= pelne - 1, 'co dobe linia "Pieniadz swiata (rody):" (od drugiej doby)',
              '%d linii w %d dobach' % (len(rody), pelne), rody[0][1].get('_nr'))
        k = _wart(rody, 'rozliczen')
        if k:
            med = mediana(k)
            K.gdy((max(k) - min(k)) <= max(10, ROZLICZEN_ROZRZUT_PROC / 100.0 * med),
                  'liczba rozliczen na dobe stala (tyle, ile rodow bez band)', 'rozliczen: mediana %s' % _zakres(k),
                  rody[-1][1].get('_nr'))
        zm = _wart(rody, 'zmiana')
        if zm:
            K.gdy(mediana(zm) < 0, 'rozliczenia rodow: zmiana zlota swiata (ok. -400..-600 tys.)',
                  'zmiana zlota swiata przez rozliczenia: mediana %s; bez zoldu byloby %s'
                  % (_zakres(zm), _zakres(_wart(rody, 'bez_zoldu'))), rody[-1][1].get('_nr'))
        nd = sum(_wart(rody, 'niedomkniete'))
        K.gdy(nd == 0 and not any(d.get('niewpiety') for _, d in rody), 'rozliczenia niedomkniete: brak',
              'niedomknietych %d w %d dobach%s' % (nd, len(rody), '; LICZNIK NIE WPIETY' if any(d.get('niewpiety')
                                                                                              for _, d in rody) else ''),
              rody[-1][1].get('_nr'))
    if bil:
        u = ses.fakty.get('uklad_ksiegi')
        K.gdy(u == UKLAD_102B, 'bilans w ukladzie 102b (rozliczenia rodow, "W tym zold naliczony")',
              'uklad linii: %s' % ('102b' if u == UKLAD_102B else 'wpis 102 - poprawka ksiegi nie jest wgrana'),
              bil[-1][1].get('_nr'))
        st = [d['reszta'] / float(d['zold']) for _, d in bil if d.get('zold') and d.get('reszta') is not None]
        if st:
            med = mediana(st)
            K.gdy(not (ZOLD_DUBEL_OD <= med <= ZOLD_DUBEL_DO), 'reszta [R] bilansu nie jest juz rzedu zoldu',
                  'reszta / zold: mediana %.2f w %d dobach; reszta %s, zold naliczony %s; reszta to %s%% ruchu'
                  % (med, len(st), _zakres(_wart(bil, 'reszta')), _zakres(_wart(bil, 'zold')),
                     _zakres(_wart(bil, 'reszta_proc'))), bil[-1][1].get('_nr'))
        bild, rozne, razem = dict(bil), 0, 0
        for bi, d in rody:
            b = bild.get(bi)
            if b is None or b.get('rody_plus') is None or d.get('na_plus') is None:
                continue
            razem += 1
            if d['na_plus'] != b['rody_plus'] or abs(d.get('na_minus') or 0) != abs(b.get('rody_minus') or 0) \
                    or (d.get('zold') is not None and b.get('zold') is not None and d['zold'] != b['zold']):
                rozne += 1
        if razem:
            K.gdy(rozne == 0, 'bilans i linia rodow podaja te same rozliczenia i ten sam zold',
                  'zgodne w %d z %d dob' % (razem - rozne, razem), rody[-1][1].get('_nr'))
    else:
        K.brak('reszta [R] bilansu nie jest juz rzedu zoldu', 'nie ma linii "Pieniadz swiata (bilans):"')
    prz = ses.ser('Przeplywy osad')
    if prz:
        potk = sum(_wart(prz, 'potkniecia'))
        K.gdy(potk == 0 and all(d.get('uklad') == UKLAD_102B for _, d in prz),
              '"Przeplywy osad" w ukladzie 102b, bez potkniec licznikow',
              'uklad 102b w %d z %d dob, potkniecia licznikow %d' % (len([1 for _, d in prz if d.get('uklad') == UKLAD_102B]),
                                                                   len(prz), potk), prz[-1][1].get('_nr'))
    st = ses.fakty.get('stale_skarbca')
    if st:
        K.gdy(st[0] == st[1], 'bez zmian: skarbiec - podmienione stale 4 z 4',
              'podmienionych stalych %d, oczekiwane %d' % (st[0], st[1]), st[2])


def _k103(K, ses):
    _start(K, ses, '103', 'start: latki karawan wpiete, regula CZYNNA', ['latki wpiete', 'CZYNNA'], ('BRAK', 'NIE wpiet', 'WYLACZON'))
    kar, stan = ses.ser('Karawany'), ses.ser('Karawany (stan)')
    if not kar or not stan:
        K.brak('co dobe linie "Karawany:" i "Karawany (stan):"', 'nie ma ich w logu')
        return
    po = _pierw_ost(stan, 'ruda_bez')
    if po:
        ok = po[1] <= KARAWANY_BEZ_TOWARU_CEL or (len(stan) < KARAWANY_MIN_DOB and po[1] <= po[0]) \
            or (po[0] and 100.0 * (po[0] - po[1]) / po[0] >= KARAWANY_MIN_SPADEK_PROC)
        K.gdy(ok, 'ruda: miast bez towaru ubywa (cel: kilkanascie w 10-20 dob)',
              '%s -> %s miast bez rudy (doby %d-%d); brakuje %s -> %s ladunkow, w jukach karawan %s'
              % (po[0], po[1], po[2], po[3], skr(stan[0][1].get('ruda_brak')), skr(stan[-1][1].get('ruda_brak')),
                 skr(stan[-1][1].get('ruda_juki'))), stan[-1][1].get('_nr'))
    ceny = _wart(stan, 'ruda_cena')
    if ceny:
        bez = [d.get('bez_ceny') for _, d in stan if d.get('bez_ceny')]
        K.gdy(not bez and max(ceny) > 0, 'cena zbytu rudy jest (7-10 d przy pustych miastach, potem w dol)',
              'cena zbytu rudy %s -> %s d (kupuje %s -> %s miast)%s'
              % (skr(ceny[0]), skr(ceny[-1]), skr(stan[0][1].get('ruda_kupuje')), skr(stan[-1][1].get('ruda_kupuje')),
                 ('; BRAK CENY przy niezerowym braku: %s' % bez[0]) if bez else ''), stan[-1][1].get('_nr'))
    else:
        K.brak('cena zbytu rudy w "Karawany (stan):"', 'linie nie maja pozycji "cena zbytu" (starsza postac ogniwa 103)')
    kup, zak = sum(_wart(kar, 'kup_szt')), sum(_wart(kar, 'z_zakupem'))
    K.gdy(zak > 0, 'karawany kupuja surowce z nadwyzek miast',
          'wyjazdow z zakupem %s z %s w %d dobach; kupione %s szt. (ruda %s, drewno %s) za %s d'
          % (skr(zak), skr(sum(_wart(kar, 'wyjazdy'))), len(kar), skr(kup), skr(sum(_wart(kar, 'kup_ruda'))),
             skr(sum(_wart(kar, 'kup_drewno'))), skr(sum(_wart(kar, 'karawany_zaplacily')))), kar[-1][1].get('_nr'))
    sprz = sum(_wart(kar, 'sprz_szt'))
    K.gdy(sprz > 0, 'karawany sprzedaja surowce miastom z brakiem',
          'wjazdow ze sprzedaza %s; sprzedane %s szt. (ruda %s, drewno %s), miasta zaplacily %s d'
          % (skr(sum(_wart(kar, 'ze_sprzedaza'))), skr(sprz), skr(sum(_wart(kar, 'sprz_ruda'))),
             skr(sum(_wart(kar, 'sprz_drewno'))), skr(sum(_wart(kar, 'miasta_zaplacily')))), kar[-1][1].get('_nr'))
    if _wart(kar, 'first_szt'):
        od = Counter()
        for _, d in kar:
            if d.get('first_naj') and d.get('first_naj') != 'nic':
                od[d['first_naj']] += 1
        K.dodaj(OK, 'pomiar: kolejnosc kupna wedle zysku (ruda rzadko pierwsza)',
                'wyjazdow, ktore zaczely od rudy: %s z %s; najczesciej pierwsze: %s; oczekiwany zysk razem %s d'
                % (skr(sum(_wart(kar, 'first_ruda'))), skr(sum(_wart(kar, 'first_szt'))),
                   ', '.join('%s (%d dob)' % x for x in od.most_common(3)) or '-', skr(sum(_wart(kar, 'zysk_razem')))),
                kar[-1][1].get('_nr'))
    else:
        K.brak('kolejnosc kupna wedle zysku', 'linia "Karawany:" bez pozycji "wyjazdow, ktore zaczely zakupy od" (starsza postac)')
    bz, bm = sum(_wart(kar, 'odp_bez_zysku')), sum(_wart(kar, 'odp_brak_miejsca'))
    K.gdy(not (bm > 20 and bm > KARAWANY_BRAK_MIEJSCA_KROTNOSC * max(1, zak)),
          'zakup odpuszczony: "brak miejsca w jukach" nie dominuje',
          'bez zysku wobec ceny zbytu %s, brak miejsca w jukach %s, cena nie nizsza od sredniej %s, w drodze dosc %s'
          % (skr(bz), skr(bm), skr(sum(_wart(kar, 'odp_cena'))), skr(sum(_wart(kar, 'odp_w_drodze_dosc')))), kar[-1][1].get('_nr'))
    naj = (0, None)
    for nazwa in SUROWCE_KARAWAN.values():
        ile, _ = najdluzsza_seria([bool(d.get(nazwa + '_stoi')) for _, d in stan])
        if ile > naj[0]:
            naj = (ile, nazwa)
    K.gdy(naj[0] <= KUPNO_STOI_MAKS_DOB, '"KUPNO STOI" najwyzej %d doby z rzedu' % KUPNO_STOI_MAKS_DOB,
          ('najdluzej %d dob (%s)' % naj) if naj[0] else 'ani razu', stan[-1][1].get('_nr'))
    ru = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
    wz = _wart(ru, 'warsztaty')
    if wz:
        K.dodaj(OK, 'pomiar: ruda zuzyta przez warsztaty zbrojne (cel 50-75)',
                'warsztaty zbrojne: mediana %s ladunkow na dobe; "Warsztaty: brak surowca [ruda]": %s'
                % (_zakres(wz), _zakres(_wart(ses.ser('Warsztaty'), 'bs_ruda'))), ru[-1][1].get('_nr'))


def _k104(K, ses):
    f = ses.fakty
    linie = f.get('startstock') or []
    if not linie:
        if f.get('nowa_kampania'):
            K.dodaj(UW, 'start nowej kampanii: 4 linie "StartStock:"', 'nowa kampania, a linii "StartStock:" nie ma - ogniwo 104 nie '
                    'jest wgrane albo zapas nie zostal przeliczony')
        else:
            K.brak('start nowej kampanii: 4 linie "StartStock:"', 'wczytany zapis - linie sa tylko przy starcie NOWEJ kampanii')
        return
    bez = [w for w in linie if 'BEZ przeliczenia' in w.tresc or 'nie da sie odczytac' in w.tresc]
    K.gdy(len(linie) == 4 and not bez, 'start nowej kampanii: 4 linie "StartStock:"',
          '%d linii "StartStock:" przy starcie kampanii%s' % (len(linie), ('; zapas NIE przeliczony: ' +
                                                                         obetnij(bez[0].tresc[12:], 80)) if bez else ''),
          linie[0].nr)
    potk = None
    for w in linie:
        m = re.search(r'StartStock: (\w+) - (\d+) szt\. po .*? -> (\d+) ladunkow po .*?cel (-?\d+)', w.tresc)
        if m:
            n, szt, po, cel = m.group(1), int(m.group(2)), int(m.group(3)), int(m.group(4))
            K.gdy(po == cel, 'zapas startowy: %s przeliczony na ladunki, "po" = "cel"' % n,
                  '%d szt. -> %d ladunkow, cel %d (oczekiwane: %s)' % (szt, po, cel, 'ruda ok. 7000 -> ok. 700' if n == 'ruda'
                                                                       else 'drewno ok. 25-28 tys. -> ok. 2500-2800'), w.nr)
        m = re.search(r'gotowe - .*?; potkniecia (\d+)', w.tresc)
        if m:
            potk = (int(m.group(1)), w.nr)
    if potk is not None:
        K.gdy(potk[0] == 0, 'przeliczenie bez potkniec i bez "ERROR in StartStock"', 'potkniecia %d; linii ERROR ze StartStock: %d'
              % (potk[0], len([b for b in ses.bledy if 'StartStock' in b[0]])), potk[1])
    ru, dr = ses.ser('Ruda'), ses.ser('Drewno')
    if ru and ru[0][0] == 0 and ru[0][1].get('razem') is not None:
        K.gdy(ru[0][1]['razem'] < RUDA_RAZEM_PO_104, '"Ruda: razem" pierwszej doby ponizej %d ladunkow' % RUDA_RAZEM_PO_104,
              'razem %s ladunkow rudy w pierwszej dobie (przed przeliczeniem bylo ok. 7000)' % skr(ru[0][1]['razem']),
              ru[0][1].get('_nr'))
    else:
        K.brak('"Ruda: razem" pierwszej doby ponizej %d ladunkow' % RUDA_RAZEM_PO_104, 'nie ma linii "Ruda:" pierwszej doby')
    if ru and dr:
        K.dodaj(OK, 'pomiar: wsie dopisuja od pierwszej doby (ok. 26 / 45 wsi)',
                'pierwsza doba: ruda %s ladunkow w %s wsiach, drewno %s w %s wsiach'
                % (skr(ru[0][1].get('dopisaly')), skr(ru[0][1].get('wsi_kopie')), skr(dr[0][1].get('dopisaly')),
                   skr(dr[0][1].get('wsi_kopie'))), ru[0][1].get('_nr'))
    sk = ses.ser('Dowoz (skutki)')
    if sk:
        d = sk[0][1]
        K.gdy((d.get('zatk_zamkowe') or 0) < 25 and (d.get('zatk_miejskie') or 0) < 32,
              'zatkane magazyny 1. doby ponizej 25 (zamkowe) i 32 (miejskie)',
              'zatkane: wsie zamkowe %s, wsie miejskie %s' % (skr(d.get('zatk_zamkowe')), skr(d.get('zatk_miejskie'))), d.get('_nr'))
    kar = ses.ser('Karawany')
    if kar:
        pierwsza = [bi + 1 for bi, d in kar if (d.get('z_zakupem') or 0) > 0]
        K.gdy(bool(pierwsza) and pierwsza[0] <= KARAWANY_ROZRUCH_DOB or len(kar) < KARAWANY_ROZRUCH_DOB,
              'karawany: pierwsze zakupy w 7-10 dob po przeliczeniu zapasu',
              ('pierwszy wyjazd z zakupem w dobie %d' % pierwsza[0]) if pierwsza else 'zadnego zakupu w %d dobach' % len(kar),
              kar[-1][1].get('_nr'))


def _k105(K, ses):
    f = ses.fakty
    _start(K, ses, '105', 'start: latka mineralow wpieta (nie "NIE wpieta")', ['latka wpieta'])
    mn = f.get('mnoznik_rudy')
    if mn is not None:
        K.gdy(abs(mn - MNOZNIK_RUDY_OCZEKIWANY) < 0.01, 'wydobycie bez zmian: "wydobycie rudy x3.0"',
              'x%.1f (drewna x%.1f)%s' % (mn, f.get('mnoznik_drewna', 0.0), '' if abs(mn - MNOZNIK_RUDY_OCZEKIWANY) < 0.01
                                          else ' - inna wartosc Mine Output Multiplier zapisana w MCM'), f.get('mnoznik_nr'))
    else:
        K.brak('wydobycie bez zmian: "wydobycie rudy x3.0"', 'nie ma linii "MaterialLaw: ... wydobycie rudy x..."')
    mi = ses.ser('Mineraly (dubel BK)')
    if not mi:
        K.brak('co dobe linia "Mineraly (dubel BK):"', 'nie ma jej w logu')
    else:
        stany = Counter(d.get('stan') for _, d in mi)
        zdj = [(bi, d) for bi, d in mi if d.get('stan') == 'zdjete']
        K.gdy(bool(zdj) and not stany.get('NIE wpieta') and not stany.get('WYLACZONA'),
              'co dobe powtorzenia zdjete z list produkcji BK',
              'stan latki w %d dobach: %s' % (len(mi), ', '.join('%s %d' % (k, v) for k, v in stany.most_common())),
              mi[-1][1].get('_nr'))
        if zdj:
            naj = dict((k, max(_wart(zdj, k) or [0])) for k in MINERALY_ZNANE)
            K.gdy(all(naj[k] <= lim for k, lim in MINERALY_LIMIT_WSI.items()) and naj['iron'] > 0,
                  'lista: clay do 16, iron do 26, salt do 12, silver do 30 wsi',
                  'najwiecej wsi: iron %d, salt %d, clay %d, silver %d, marble %d, gold_ore %d'
                  % (naj['iron'], naj['salt'], naj['clay'], naj['silver'], naj['marble'], naj['gold_ore']), zdj[-1][1].get('_nr'))
            wm = _wart(zdj, 'wsi_mnoznik')
            if wm:
                K.gdy(min(wm) > 0, 'zdjety wpis oddaje mnoznik modelu (wsi z mnoznikiem ok. 84)',
                      'wsi z mnoznikiem: mediana %s' % _zakres(wm), zdj[-1][1].get('_nr'))
            else:
                K.brak('zdjety wpis oddaje mnoznik modelu (wsi z mnoznikiem ok. 84)', 'linia bez tej pozycji (starsza postac ogniwa 105)')
        obce = [d.get('obce') for _, d in mi if d.get('obce')]
        potk = max(_wart(mi, 'potkniecia') or [0])
        K.gdy(not obce and potk == 0, 'bez obcych przedmiotow na liscie i bez potkniec',
              'obce przedmioty: %s; potkniecia od wczytania kampanii: %d' % (obce[0] if obce else 'brak', potk),
              mi[-1][1].get('_nr'))
    ru = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0]
    nw = _wart(ru, 'na_wies_do_modelu')
    if nw:
        K.gdy(mediana(nw) <= STOSUNEK_NA_WIES_PO_105, 'Ruda: kopiaca wies dopisuje tyle, ile liczy "model"',
              'dopisane na wies / model na wies: mediana %s; "model" %s w %s wsiach (dotad ok. 97, po zmianie ok. 192)'
              % (_zakres(nw), _zakres(_wart(ru, 'model')), skr(mediana(_wart(ru, 'model_wsi')))), ru[-1][1].get('_nr'))
        K.dodaj(OK, 'pomiar: liczba kopiacych wsi (dotad srednio 14.9 z 26)',
                'kopiacych wsi na dobe: mediana %s; wsie dopisaly: mediana %s ladunkow'
                % (_zakres(_wart(ru, 'wsi_kopie')), _zakres(_wart(ru, 'dopisaly'))), ru[-1][1].get('_nr'))
    else:
        K.brak('Ruda: kopiaca wies dopisuje tyle, ile liczy "model"', 'nie ma linii "Ruda:" od drugiej doby')


def _k106(K, ses):
    f = ses.fakty
    chce = ['awanse band', 'zywnosc band', 'NavalDLC zywnosc piratow', 'zloto dzienne', 'zloto startowe',
            'NavalDLC zloto startowe', 'zloto kryjowek']
    w = f.get('outlaw')
    if w is not None:
        brak = [x for x in chce if x not in w.tresc.split('; BRAK:')[0]]
        K.gdy(not brak and '; BRAK:' not in w.tresc, 'start: OutlawLaw wpiete (zywnosc band, zloto kryjowek)',
              ('brak na liscie wpietych: ' + ', '.join(brak)) if brak else wycinek(w.tresc, 110), w.nr)
    else:
        K.brak('start: OutlawLaw wpiete (zywnosc band, zloto kryjowek)', 'nie ma linii "OutlawLaw: ... wpiete: ..."')
    pa = ses.ser('Paser')
    if not pa:
        K.brak('co dobe linia "Paser:"', 'nie ma jej w logu')
        return
    lad, zp = sum(_wart(pa, 'z_ladunkiem')), sum(_wart(pa, 'z_paserem'))
    bez = [(bi, d) for bi, d in pa if (d.get('bez_pasera') or 0) > 0]
    K.gdy(not bez and bool(_wart(pa, 'z_paserem')), 'kazda banda z ladunkiem ma pasera (100%)',
          'z paserem %s z %s band z ladunkiem w %d dobach%s; poza promieniem %s'
          % (skr(zp), skr(lad), len(pa), ('; dob z bandami bez pasera: %d' % len(bez)) if bez else '',
             skr(sum(_wart(pa, 'poza_promieniem')))), (bez[0][1] if bez else pa[-1][1]).get('_nr'))
    zost = [(bi, d) for bi, d in pa if (d.get('zostalo') or 0) > 0]
    ile, pocz = najdluzsza_seria([(d.get('zostalo') or 0) > 0 for _, d in pa])
    K.gdy(ile < PASER_ZOSTALO_DOB, '"z ladunkiem zostalo 0 band"',
          'zostalo ponad 0 w %d z %d dob, najdluzej %d dob z rzedu; powody razem: kasy miast przy rezerwie %s, brak otwartego '
          'miasta %s' % (len(zost), len(pa), ile, skr(sum(_wart(pa, 'zost_rezerwa'))), skr(sum(_wart(pa, 'zost_brak_miasta')))),
          (zost[0][1] if zost else pa[-1][1]).get('_nr'))
    ponad = [1 for _, d in pa if d.get('sprzedalo') is not None and d.get('z_ladunkiem') is not None
             and d['sprzedalo'] > d['z_ladunkiem']]
    K.gdy(not ponad, '"sprzedalo" nigdy wiecej niz "band z ladunkiem"',
          'sprzedalo %s band, %s szt. (ruda %s, drewno %s, zywnosc %s, zwierzeta %s) za %s zl; dzialka pasera %s zl'
          % (skr(sum(_wart(pa, 'sprzedalo'))), skr(sum(_wart(pa, 'szt'))), skr(sum(_wart(pa, 'ruda'))),
             skr(sum(_wart(pa, 'drewno'))), skr(sum(_wart(pa, 'zywnosc'))), skr(sum(_wart(pa, 'zwierzeta'))),
             skr(sum(_wart(pa, 'za_zl'))), skr(sum(_wart(pa, 'dzialka')))), pa[-1][1].get('_nr'))
    km, kz = dict(ses.ser('Przeplywy osad (kasy miast)')), dict(ses.ser('Przeplywy osad (kasy zamkow)'))
    razem = skup_zle = zycie_zle = 0
    for bi, d in pa:
        k = km.get(bi)
        if k is None:
            continue
        z = kz.get(bi) or {}
        razem += 1
        if d.get('za_zl') is not None and abs((k.get('paser_skup') or 0) + (z.get('paser_skup') or 0)) != d['za_zl']:
            skup_zle += 1
        if d.get('zycie_razem') is not None and (k.get('zycie_band') or 0) + (z.get('zycie_band') or 0) != d['zycie_razem']:
            zycie_zle += 1
    if razem:
        K.gdy(skup_zle == 0 and zycie_zle == 0, 'ksiega kas miast liczy ten sam skup i zycie band',
              'porownane %d dob: skup zgodny w %d, zycie w miastach zgodne w %d; zycie razem %s zl'
              % (razem, razem - skup_zle, razem - zycie_zle, skr(sum(_wart(pa, 'zycie_razem')))), pa[-1][1].get('_nr'))
    else:
        K.brak('ksiega kas miast liczy ten sam skup i zycie band', 'nie ma linii "Przeplywy osad (kasy miast):" w dobach z linia "Paser:"')
    aw = sum(_wart(ses.ser('Wyrzutki'), 'aw_paser'))
    K.gdy(aw > 0 or len(pa) < PASER_MIN_DOB_BEZ_AWANSOW, 'bandy kupuja sprzet u pasera (awanse od pasera ponad 0)',
          'awansow od pasera %s w %d dobach za %s zl (w tym z kas kryjowek %s zl)'
          % (skr(aw), len(pa), skr(sum(_wart(pa, 'kupno_zl'))), skr(sum(_wart(pa, 'kupno_z_kryjowek')))), pa[-1][1].get('_nr'))
    otw = [1 for _, d in pa if d.get('kurek_otwarty') or d.get('kurek_zywnosci') or d.get('obieg_wylaczony')]
    K.gdy(not otw, 'koniec zlota i zywnosci band z niczego (kurki zamkniete)',
          'zablokowane zloto kryjowek %s zl w %s wejsciach; zywnosc zablokowana %s nowym bandom%s'
          % (skr(sum(_wart(pa, 'zabl_razem'))), skr(sum(_wart(pa, 'wejsc'))), skr(sum(_wart(pa, 'zywn_zabl'))),
             ('; DOB Z OTWARTYM KURKIEM albo wylaczonym obiegiem: %d' % len(otw)) if otw else ''), pa[-1][1].get('_nr'))
    ost = pa[-1][1]
    K.gdy((ost.get('juki_zywnosc') or 0) == 0, 'juki band: ladunek na sprzedaz, "zywnosc poza skupem 0"',
          'ostatnia doba: ladunek na sprzedaz %s szt. (%s zl), zywnosc poza skupem %s, zbroje na awanse %s, konie %s'
          % (skr(ost.get('juki_ladunek')), skr(ost.get('juki_ladunek_zl')), skr(ost.get('juki_zywnosc')),
             skr(ost.get('juki_zbroje')), skr(ost.get('juki_konie'))), ost.get('_nr'))
    po = _pierw_ost(pa, 'kasy_kryjowek')
    K.dodaj(OK, 'pomiar: kasy kryjowek i skup wedle odleglosci od miasta',
            'kasy kryjowek %s -> %s zl; doplyw %s, odplyw %s; skup szt.: do 50 - %s, 50-100 - %s, dalej - %s'
            % (skr(po[0]) if po else '-', skr(po[1]) if po else '-', skr(sum(_wart(pa, 'kryj_doplyw'))),
               skr(sum(_wart(pa, 'kryj_odplyw'))), skr(sum(_wart(pa, 'strefa1_szt'))), skr(sum(_wart(pa, 'strefa2_szt'))),
               skr(sum(_wart(pa, 'strefa3_szt')))), ost.get('_nr'))


def _k107(K, ses):
    f = ses.fakty
    _start(K, ses, '107', 'start: "SoldierPay: zold do obiegu ... rozliczenie rodu wpiete"', ['rozliczenie rodu wpiete'])
    w = f.get('soldierpay_latki')
    if w is not None:
        K.gdy('zold partii wpiete' in w.tresc and 'BRAK' not in w.tresc,
              'pierwsza doba: latki modelu finansow wpiete', wycinek(w.tresc[12:], 110), w.nr)
    elif ses.bloki:
        K.dodaj(UW, 'pierwsza doba: latki modelu finansow wpiete',
                'nie ma linii "SoldierPay: latki modelu finansow ..." - latki na model nie zalozone')
    zo = ses.ser('Zold')
    if not zo:
        K.brak('co dobe linia "Zold:"', 'nie ma jej w logu')
        return
    wl = [1 for _, d in zo if d.get('tarcza_wl')]
    t = f.get('soldierpay_tarcza')
    K.gdy(len(wl) == len(zo) and (t is None or 'BRAK' not in t.tresc), 'tarcza zoldu w kasach miast wlaczona (decyzja 06.10)',
          'wlaczona w %d z %d dob; znacznik zoldu w kasach %s, regulator nie skasowal dzis: mediana %s; latka regulatora: %s'
          % (len(wl), len(zo), skr(zo[-1][1].get('znacznik')), _zakres(_wart(zo, 'nie_skasowal')),
             ('BRAK - tarcza nie dziala' if 'BRAK' in t.tresc else 'wpieta') if t is not None else 'linii jeszcze nie ma'),
          (t.nr if t is not None else zo[-1][1].get('_nr')))
    roz = [abs(d['rozjazd']) for _, d in zo if d.get('rozjazd') is not None]
    zeszlo = _wart(zo, 'zeszlo_razem')
    if roz and zeszlo:
        K.gdy(max(roz) <= max(10, ZOLD_ROZJAZD_PROC / 100.0 * mediana(zeszlo)),
              'zold nie znika: z kies zeszlo = przekazane + nieprzekazane',
              'z kies zeszlo na dobe: mediana %s; do sakiewek ludzi %s, do kas miast %s, do kas zamkow %s; najwiekszy rozjazd %s'
              % (_zakres(zeszlo), skr(mediana(_wart(zo, 'do_sakiewek'))), skr(mediana(_wart(zo, 'do_miast'))),
                 skr(mediana(_wart(zo, 'do_zamkow'))), skr(max(roz))), zo[-1][1].get('_nr'))
    prz = dict(ses.ser('Przeplywy osad'))
    pary = []
    for bi, d in zo:
        k = prz.get(bi)
        if k is not None and d.get('naliczony_razem') is not None and k.get('zold_rody') is not None:
            pary.append((d['naliczony_razem'], (k.get('zold_rody') or 0) + (k.get('zold_garnizony') or 0)))
    if pary:
        naj = max(abs(a - b) / float(b) for a, b in pary if b) if any(b for _, b in pary) else 0.0
        K.gdy(100.0 * naj <= ZOLD_KSIEGA_ROZJAZD_PROC, '"Zold: naliczony" bliski licznikowi zoldu ksiegi pieniadza',
              'porownane %d dob, najwieksza roznica %.1f%% (ostatnio %s wobec %s)' % (len(pary), 100.0 * naj, skr(pary[-1][0]),
                                                                                    skr(pary[-1][1])), zo[-1][1].get('_nr'))
    else:
        K.brak('"Zold: naliczony" bliski licznikowi zoldu ksiegi pieniadza', 'nie ma linii "Przeplywy osad:" w dobach z linia "Zold:"')
    slepe = sum(_wart(zo, 'slepe'))
    K.gdy(slepe == 0, '"rody z pusta kiesa i nieznanym saldem" = 0', 'razem %d w %d dobach' % (slepe, len(zo)),
          zo[-1][1].get('_nr'))
    pd, ps, pn = _pierw_ost(zo, 'dlug_rodow'), _pierw_ost(zo, 'sak_sieroty'), _pierw_ost(zo, 'sak_najwieksza')
    rosnie = (ps is not None and len(zo) >= 3 and ps[1] > ps[0] > 0) or (pd is not None and len(zo) >= 3 and pd[1] > pd[0]
                                                                          and pd[1] >= 5)
    K.gdy(not rosnie, 'nie rosna: rody z dlugiem, sakiewki po zniklych partiach',
          'rodow z dlugiem %s -> %s; sakiewki po zniklych partiach %s -> %s zl; najwieksza sakiewka %s -> %s zl'
          % ((skr(pd[0]), skr(pd[1])) + (skr(ps[0]), skr(ps[1])) + (skr(pn[0]), skr(pn[1])))
          if pd and ps and pn else 'brak pozycji w linii', zo[-1][1].get('_nr'))
    bil = dict(ses.ser('Pieniadz swiata (bilans)'))
    pary = []
    for bi, d in zo:
        b = bil.get(bi)
        cz = [d.get('do_sakiewek'), d.get('do_miast'), d.get('do_zamkow')]
        if b is not None and b.get('oddany') is not None and all(v is not None for v in cz):
            pary.append((sum(cz), b['oddany']))
    if pary:
        zle = [1 for a, b in pary if abs(a - b) > max(10, ZOLD_KSIEGA_ROZJAZD_PROC / 100.0 * max(a, b))]
        K.gdy(not zle, 'bilans swiata: "zold oddany do obiegu przez SoldierPay" = "Zold:"',
              'zgodne w %d z %d dob (ostatnio %s wobec %s)' % (len(pary) - len(zle), len(pary), skr(pary[-1][0]),
                                                             skr(pary[-1][1])), zo[-1][1].get('_nr'))
    elif ses.fakty.get('uklad_ksiegi') == UKLAD_102B:
        K.brak('bilans swiata: "zold oddany do obiegu przez SoldierPay"', 'bilans bez tej pozycji w dobach z linia "Zold:"')
    zw, sk = ses.ser('Korona (zwrot zoldu)'), ses.ser('Skarbce')
    wojna = any((d.get('wojna') or 0) > 0 for _, d in sk)
    if zw:
        K.gdy(sum(_wart(zw, 'potkniecia')) == 0, 'skarbiec krolestwa w wojnie zwraca rodom polowe zoldu',
              'zwrot w %d dobach, razem %s zl z naleznych %s; pusty skarbiec: najwiecej %s krolestw'
              % (len(zw), skr(sum(_wart(zw, 'zwrot'))), skr(sum(_wart(zw, 'nalezne'))), skr(max(_wart(zw, 'pusty') or [0]))),
              zw[-1][1].get('_nr'))
    elif sk:
        K.gdy(not wojna or len(ses.pelne()) < 2, 'skarbiec krolestwa w wojnie zwraca rodom polowe zoldu',
              'nie ma linii "Korona: ... zwrot zoldu"; krolestw w wojnie: %s' % ('sa' if wojna else 'brak'))
    sa = ses.ser('Sakiewka ludzi')
    zy = _wart(sa, 'na_zycie')
    if zy:
        K.gdy(mediana(zy) >= 100000, 'ludzie wydaja zold w miastach (setki tysiecy dziennie)',
              'na zycie w miastach: mediana %s zl na dobe; zold wplacony do sakiewek: mediana %s'
              % (_zakres(zy), skr(mediana(_wart(sa, 'zold')))), sa[-1][1].get('_nr'))


def _k108(K, ses):
    f = ses.fakty
    w = _start(K, ses, '108', 'start: "PeopleUnit: jednostka ludzi ... CZYNNA", bez "BRAK"',
               ['CZYNNA', 'tabor wsi - uzupelnienie', 'tabor wsi - nowy', 'pobor wymuszony gracza', 'incydenty gracza - licznik'])
    if w is not None:
        m = re.search(r'zadania gracza (\d+) z (\d+)', w.tresc)
        K.gdy(bool(m) and m.group(1) == m.group(2) and 'wyrzutki liczone od ludnosci wsi' in w.tresc,
              'start: zadania gracza 13 z 13, wyrzutki liczone od ludnosci wsi',
              ('zadania gracza %s z %s' % m.groups() if m else 'brak pozycji "zadania gracza"') +
              (', wyrzutki liczone od ludnosci wsi' if 'wyrzutki liczone od ludnosci wsi' in w.tresc else
               ', wyrzutki liczone od hearth wsi (Outlaws Counted By People wylaczone)'), w.nr)
    if f.get('nowa_kampania'):
        kal, pula = f.get('kalibracja'), f.get('pula_nr')
        if kal is not None and pula is not None:
            K.gdy(kal.nr < pula, 'start kampanii: "kalibracja ludnosci" PRZED pula wyrzutkow',
                  'kalibracja w linii %d, pula poczatkowa w linii %d' % (kal.nr, pula), kal.nr)
        if 'zdjeto_hearth' in f:
            K.gdy('1 czlowiek = 1/k hearth' in (f.get('zdjeto_jak') or ''),
                  'pula poczatkowa wyrzutkow: zdjeto 1/k hearth za czlowieka',
                  'pula %s ludzi, zdjeto %s hearth: %s' % (skr(f.get('pula_poczatkowa')), skr(f.get('zdjeto_hearth')),
                                                          obetnij(f.get('zdjeto_jak') or '', 70)), pula)
        elif pula is not None:
            K.dodaj(UW, 'pula poczatkowa wyrzutkow: zdjeto 1/k hearth za czlowieka',
                    'linia "Wyrzutki: pula poczatkowa" bez dopisku "zdjeto N hearth" (pula %s)' % skr(f.get('pula_poczatkowa')),
                    pula)
        sw = f.get('srednia_swiata')
        if sw is not None:
            m = re.search(r'srednia swiata ' + RX_F + ' ludzi wsi na punkt hearth', sw.tresc)
            K.gdy(bool(m) and 150 <= float(m.group(1).replace(',', '.')) <= 240,
                  'srednia swiata ludzi wsi na punkt hearth (rachunek: 191.2)', (m.group(1) if m else '?') + ' ludzi na hearth',
                  sw.nr)
    lu = [(bi, d) for bi, d in ses.ser('Ludzie') if d.get('jednostka')]
    if not lu:
        K.brak('co dobe odcinek "hearth za ludzi dzis" w linii "Ludzie:"', 'linie "Ludzie:" go nie maja (albo nie ma linii)')
        return
    czynne = [1 for _, d in lu if d.get('jednostka') == 'czynna']
    K.gdy(len(czynne) == len(lu), 'co dobe odcinek "hearth za ludzi dzis", jednostka czynna',
          'jest w %d dobach, jednostka czynna w %d' % (len(lu), len(czynne)), lu[0][1].get('_nr'))
    tl = _wart(lu, 'hz_tabory_ludzi')
    d0 = lu[0][1]
    K.gdy(sum(tl) > 0 or not any((d.get('tabory_wsi') or 0) > 0 for _, d in lu),
          'tabory wsi i lodzie: latka wolana (nie "za 0 ludzi")',
          'pierwsza doba: -%s hearth za %s ludzi (uzupelnien %s, nowych %s); razem %s ludzi w %d dobach; w taborach wsi %s ludzi'
          % (skr(d0.get('hz_tabory')), skr(d0.get('hz_tabory_ludzi')), skr(d0.get('hz_uzupelnien')), skr(d0.get('hz_nowych')),
             skr(sum(tl)), len(lu), skr(lu[-1][1].get('tabory_wsi'))), d0.get('_nr'))
    nasze, gra = sum(_wart(lu, 'hz_tabory')), sum(_wart(lu, 'hz_gra'))
    if nasze > 0:
        K.gdy(gra / nasze >= 10.0, 'wies oddaje za tabor ok. 100 razy mniej hearth niz w grze',
              'nasze -%s hearth wobec %s, ktore zdjelaby gra (%.0f razy mniej)' % (skr(round(nasze, 3)), skr(round(gra, 1)),
                                                                                  gra / nasze), lu[-1][1].get('_nr'))
    zle = [(k, sum(_wart(lu, k))) for k in ('hz_niezgodne', 'hz_bez_k', 'hz_potk')]
    K.gdy(all(v == 0 for _, v in zle), 'bez dopiskow "niezgodne z formula gry", "potkniecia"',
          'niezgodne %s, bez przelicznika kultury %s, potkniecia %s' % tuple(skr(v) for _, v in zle), lu[-1][1].get('_nr'))
    K.dodaj(OK, 'pomiar: hearth za wyrzutkow, pobor, zadania, incydenty',
            'w las -%s za %s ludzi, z lasu +%s za %s ludzi; pobor -%s, zadania %s, incydenty %s (suma %d dob)'
            % (skr(round(sum(_wart(lu, 'hz_wyrzutki')), 3)), skr(round(sum(_wart(lu, 'hz_wyrzutki_ludzi')), 1)),
               skr(round(sum(_wart(lu, 'hz_powrot')), 3)), skr(round(sum(_wart(lu, 'hz_powrot_ludzi')), 1)),
               skr(round(sum(_wart(lu, 'hz_pobor')), 3)), skr(round(sum(_wart(lu, 'hz_zadania')), 3), True),
               skr(round(sum(_wart(lu, 'hz_incydenty')), 1), True), len(lu)), lu[-1][1].get('_nr'))
    wy = ses.ser('Wyrzutki')
    if wy:
        d = wy[0][1]
        K.dodaj(OK, 'pomiar: bandy pierwszej doby (test 15:22: nowe 391, pula 299)',
                'bandy nowe %s (%s ludzi), odmowione %s, puste usuniete %s; pula po pierwszej dobie %s; band %s -> %s'
                % (skr(d.get('bandy_nowe')), skr(d.get('nowe_ludzi')), skr(d.get('odmowione')), skr(d.get('puste')),
                   skr(d.get('pula')), skr(d.get('band')), skr(wy[-1][1].get('band'))), d.get('_nr'))
    ln = ses.ser('Ludnosc')
    if ln and ln[0][1].get('mln') is not None:
        K.gdy(abs(ln[0][1]['mln'] - 52.6) <= 0.25 or not f.get('nowa_kampania'), '"Ludnosc:" pierwszej doby nadal 52.6 mln',
              '%.1f mln w pierwszej dobie sesji%s' % (ln[0][1]['mln'], '' if f.get('nowa_kampania') else ' (wczytany zapis)'),
              ln[0][1].get('_nr'))


def _k109(K, ses):
    f = ses.fakty
    w = _start(K, ses, '109', 'start: przyrost naturalny wsi CZYNNY, latka wpieta',
               ['CZYNNY', 'wpieta'], ('BRAK', 'NIE wpiet', 'wylaczony w ustawieniach'))
    if w is not None:
        m = re.search(r'g = (.*?) \[% rocznie\], korytarz (\S+?)[;,]', w.tresc)
        wzor = 'g = 0.5 + 0.6 x dobrobyt - 0.6 x niebezpieczenstwo (glod wylaczony)'
        K.gdy(bool(m) and ('g = ' + m.group(1)) == wzor and m.group(2) == '-1.3..+0.8',
              'wzor przyrostu z ustawien domyslnych, korytarz -1.3..+0.8',
              ('g = %s, korytarz %s' % m.groups()) if m else 'wzoru nie ma w linii startowej (oczekiwany: %s)' % wzor,
              w.nr)
    md = f.get('model_dobrobytu')
    if md is not None:
        m = re.search(r'postfiksy w kolejnosci biegu: (.*?); prefiksy', md.tresc)
        lancuch = [x.strip() for x in m.group(1).split(' -> ')] if m else []
        ost = lancuch[-1] if lancuch else '?'
        K.gdy('PopulationLaw.GrowthPostfix' in ost and 'dopisuje PO naszej latce' not in md.tresc,
              'pierwsza doba: GrowthPostfix biegnie ostatni',
              '%d postfiksow, ostatni: %s%s' % (len(lancuch), obetnij(ost, 60), '; model dopisuje PO naszej latce'
                                                if 'dopisuje PO naszej latce' in md.tresc else ''), md.nr)
    elif ses.bloki:
        K.brak('pierwsza doba: GrowthPostfix biegnie ostatni', 'nie ma linii "PopulationLaw: czynny model dobrobytu gry ..."')
    pr = ses.ser('Ludzie (przyrost)')
    if not pr:
        K.brak('co dobe linia "Ludzie: przyrost naturalny +N ludzi"', 'nie ma jej w logu')
        return
    wyl = [1 for _, d in pr if d.get('wylaczony')]
    n, g = _wart(pr, 'ludzi'), _wart(pr, 'g_srednio')
    if n:
        K.gdy(not wyl and all(PRZYROST_MIN_LUDZI <= x <= PRZYROST_MAKS_LUDZI for x in n),
              'przyrost naturalny N w przedziale %d-%d (oczekiwane 250-600)' % (PRZYROST_MIN_LUDZI,
                                                                                             PRZYROST_MAKS_LUDZI),
              'N: mediana %s ludzi dziennie w %d dobach; w rok ok. %s ludzi%s' % (_zakres(n), len(n),
                                                                                skr(mediana(_wart(pr, 'w_rok')), True),
                                                                                ('; WYLACZONY w %d dobach' % len(wyl)) if wyl
                                                                                else ''), pr[-1][1].get('_nr'))
    else:
        K.dodaj(UW, 'przyrost naturalny N w przedziale %d-%d (oczekiwane 250-600)' % (PRZYROST_MIN_LUDZI, PRZYROST_MAKS_LUDZI),
                'przyrost WYLACZONY we wszystkich %d dobach - hearth wsi liczy gra: %s ludzi dziennie'
                % (len(pr), _zakres(_wart(pr, 'gra_ludzi'))), pr[-1][1].get('_nr'))
    if g:
        K.gdy(0.2 <= mediana(g) <= 0.5, '"g srednio" 0.2-0.5% rocznie', 'g srednio: mediana %s%% rocznie; wsie: rosnie %s, kurczy '
              'sie %s, bez zmiany %s (ostatnia doba)' % (_zakres(g), skr(pr[-1][1].get('rosnie')), skr(pr[-1][1].get('kurczy')),
                                                        skr(pr[-1][1].get('bez_zmiany'))), pr[-1][1].get('_nr'))
    # N x dni roku / ludnosc wsi ma dawac g (ludnosc wsi z linii "Ludzie:")
    lu = dict(ses.ser('Ludzie'))
    roz = []
    for bi, d in pr:
        wsie = (lu.get(bi) or {}).get('wsie_mln')
        if d.get('w_rok') is not None and d.get('g_srednio') is not None and wsie:
            roz.append(abs(100.0 * d['w_rok'] / (wsie * 1e6) - d['g_srednio']))
    if roz:
        K.gdy(max(roz) <= 0.05, 'rachunek sie zgadza: N x dni roku / ludnosc wsi = g srednio',
              'najwieksza roznica %.3f pkt %% w %d dobach' % (max(roz), len(roz)), pr[-1][1].get('_nr'))
    uch = _wart(pr, 'uch_ludzi')
    K.dodaj(OK, 'pomiar: powrot uchodzcow (osobno, nie wchodzi do N)',
            ('osobno powrot uchodzcow: mediana %s ludzi dziennie w %d dobach, najwiecej wsi przy dnie %s'
             % (_zakres(uch), len(uch), skr(max(_wart(pr, 'uch_wsi') or [0])))) if uch else 'zadna wies nie spadla ponizej 40 hearth',
            pr[-1][1].get('_nr'))
    rz = _wart(pr, 'reszta')
    if rz:
        naj = max(rz, key=abs)
        K.gdy(abs(naj) <= PRZYROST_RESZTA_HEARTH, 'RESZTA hearth wsi bliska zera (BetterEconomy: +65..135)',
              'RESZTA: mediana %s hearth na dobe, najwieksza %s; zmiana hearth wsi od wczoraj: mediana %s'
              % (_zakres(rz), skr(naj, True), skr(mediana(_wart(pr, 'od_wczoraj')), True)), pr[-1][1].get('_nr'))
    else:
        K.brak('RESZTA hearth wsi bliska zera (BetterEconomy: +65..135)', 'rozliczenie zmiany hearth zaczyna sie od drugiej doby po wczytaniu')
    progi = [(d.get('prog_0'), d.get('prog_200'), d.get('prog_600')) for _, d in pr if d.get('prog_600') is not None]
    if progi:
        K.gdy(progi[0] == progi[-1], 'wsie wedle progow produkcji stale (nowa kampania 12/522/37)',
              'ponizej 200 / 200-599 / od 600 hearth: %s / %s / %s -> %s / %s / %s' % (progi[0] + progi[-1]), pr[-1][1].get('_nr'))
    potk = sum(_wart(pr, 'potk_rachunek')) + sum(_wart(pr, 'potk_linia')) + sum(_wart(pr, 'bez_k'))
    lus = ses.ser('Ludzie')
    pc = [100.0 * abs(d['ludnosc_zm']) / (d['ludnosc_mln'] * 1e6) for _, d in lus if d.get('ludnosc_zm') is not None
          and d.get('ludnosc_mln')]
    ln = [d['mln'] for _, d in ses.ser('Ludnosc') if d.get('mln')]
    if pc:
        K.gdy(max(pc) <= LUDNOSC_MAKS_ZMIANA_PROC, 'ludnosc swiata stoi (zmiana najwyzej %.2f%% dziennie)'
              % LUDNOSC_MAKS_ZMIANA_PROC, 'najwieksza zmiana dobowa %.3f%%, mediana %.3f%%; "Ludnosc:" %s -> %s mln'
              % (max(pc), mediana(pc), skr(ln[0]) if ln else '-', skr(ln[-1]) if ln else '-'), lus[-1][1].get('_nr'))
    elif len(ln) >= 2:
        dob = max(1, len(ln) - 1)
        zm = 100.0 * abs(ln[-1] - ln[0]) / ln[0] / dob
        K.gdy(zm <= LUDNOSC_MAKS_ZMIANA_PROC, 'ludnosc swiata stoi (zmiana najwyzej %.2f%% dziennie)' % LUDNOSC_MAKS_ZMIANA_PROC,
              '"Ludnosc:" %.1f -> %.1f mln w %d dob = %.3f%% dziennie' % (ln[0], ln[-1], dob, zm))
    else:
        K.brak('ludnosc swiata stoi (zmiana najwyzej %.2f%% dziennie)' % LUDNOSC_MAKS_ZMIANA_PROC, 'za malo linii "Ludzie:" / "Ludnosc:"')
    zam_ = [d for _, d in pr if d.get('zamrozone') == 1 and d.get('miasta_mln') is not None]
    if zam_:
        st = [d['miasta_mln'] for d in zam_]
        K.gdy(len(zam_) == len(pr) and max(st) - min(st) <= 0.0015, 'ludnosc miast zamrozona (stan nie idzie za dobrobytem)',
              'miasta %.3f mln w %s miastach, stale w %d z %d dob; z dzisiejszego dobrobytu byloby %s mln (dryf %s ludzi)'
              % (st[-1], skr(zam_[-1].get('miasta_n')), len(zam_), len(pr), skr(zam_[-1].get('miasta_live')),
                 skr(zam_[-1].get('miasta_dryf'), True)), pr[-1][1].get('_nr'))
    else:
        K.dodaj(UW, 'ludnosc miast zamrozona (stan nie idzie za dobrobytem)',
                'linia przyrostu nie podaje stanu zamrozonego (Town People Frozen wylaczone albo ludnosc nieskalibrowana)',
                pr[-1][1].get('_nr'))
    K.gdy(potk == 0, 'bez potkniec i bez wsi bez przelicznika ludzi', 'potkniecia i wsie bez przelicznika razem: %s' % skr(potk),
          pr[-1][1].get('_nr'))


FUNKCJE_KONTROLI = OrderedDict([('100', _k100), ('101', _k101), ('102', _k102), ('102b', _k102b), ('103', _k103),
                                ('104', _k104), ('105', _k105), ('106', _k106), ('107', _k107), ('108', _k108),
                                ('109', _k109)])


def kontrole_ogniwa(ses, nr):
    """Lista kontroli jednego ogniwa: [(status, co, wynik, numer linii)]. Nigdy wyjatek."""
    K = Kontrole()
    f = FUNKCJE_KONTROLI.get(nr)
    if f is None:
        K.brak('ogniwo %s' % nr, 'nieznany numer ogniwa')
        return K.lista
    # Ogniwa nie widac w logu: nie ma czego oceniac (inaczej linie wspolne ze starszym kodem dawalyby mylace OK albo UWAGA).
    # Wyjatek: 104 nie ma linii przy starcie gry - jego kontrole same rozrozniaja nowa kampanie i wczytany zapis.
    if nr != '104' and not ma_ogniwo(ses, nr):
        o = ogniwo(ses, nr)
        K.brak('ogniwo %s w logu' % nr, 'nie widac go (nie wgrane?) - poznac po: %s' % (o['poznac'] if o else '?'))
        return K.lista
    try:
        f(K, ses)
    except Exception as e:
        K.dodaj(BD, 'kontrole ogniwa %s przerwane' % nr, 'blad wewnetrzny (%s: %s) - reszta skrotu bez zmian' % (type(e).__name__, e))
    return K.lista


def ogniwa_z_nazwy(ses, nazwa):
    """Numery ogniw dla opcji --test: numer, lista, zakres, grupa testowa, slowo z opisu, 'wykryte', 'w-grze'."""
    q = (nazwa or '').strip().lower()
    if not q or q in ('w-grze', 'wgrze', 'gra'):
        return [ses.ogniwo_w_grze] if ses.ogniwo_w_grze else []
    if q in ('wykryte', 'lancuch'):
        return [o['nr'] for o in ses.ogniwa if ma_ogniwo(ses, o['nr'])]
    wyn = []
    for czesc in re.split(r'[,+ ]+', q):
        if not czesc:
            continue
        m = re.match(r'^(\d{3}b?)(?:-|\.\.)(\d{3}b?)$', czesc)
        if m and m.group(1) in NR_OGNIW and m.group(2) in NR_OGNIW:
            a, b = sorted((ix_ogniwa(m.group(1)), ix_ogniwa(m.group(2))))
            wyn.extend(NR_OGNIW[a:b + 1])
        elif czesc in NR_OGNIW:
            wyn.append(czesc)
        elif czesc in GRUPY_TESTOWE:
            wyn.extend(GRUPY_TESTOWE[czesc])
        else:
            traf = [o[0] for o in OGNIWA if czesc in o[1].lower() or czesc in o[7].lower()]
            wyn.extend(traf)
    return [n for i, n in enumerate(wyn) if n not in wyn[:i]]


def zawin(tekst, szer, maks=3, wciecie='        '):
    """Lamie dluga linie na najwyzej `maks` linii o szerokosci `szer` (ciag dalszy z wcieciem); nadmiar jest obciety."""
    if len(tekst) <= szer or maks <= 1:
        return [obetnij(tekst, szer)]
    wyn, reszta = [], tekst
    while reszta and len(wyn) < maks:
        miejsce = szer if not wyn else szer - len(wciecie)
        if len(reszta) <= miejsce:
            kawalek, reszta = reszta, ''
        else:
            ciecie = reszta.rfind(' ', 0, miejsce + 1)
            if ciecie < miejsce // 2:
                ciecie = miejsce
            kawalek, reszta = reszta[:ciecie], reszta[ciecie:].lstrip()
        wyn.append(kawalek if not wyn else wciecie + kawalek)
    if reszta:
        wyn[-1] = obetnij(wyn[-1] + ' ' + reszta, szer)
    return wyn


def linie_kontroli(ses, nr, szer, limit=None, naglowek=True, pelne=False):
    """Sekcja kontroli jednego ogniwa. pelne = kazda kontrola w calosci (zawijana do 3 linii); inaczej kontrola OK to jedna
    linia, a UWAGA i BRAK DANYCH najwyzej dwie (to je trzeba umiec przepisac w calosci)."""
    o = ogniwo(ses, nr)
    lista = kontrole_ogniwa(ses, nr)
    licz = Counter(k[0] for k in lista)
    L = []
    if naglowek:
        jest = ma_ogniwo(ses, nr)
        L.append(obetnij('--- CO SPRAWDZIC DLA OGNIWA %s %s (zrodlo: %s)%s: OK %d, UWAGA %d, BRAK DANYCH %d ---'
                         % (nr, o['opis'] if o else '', o['paczka'] if o else '?', '' if jest else ' - OGNIWA NIE WIDAC W LOGU',
                            licz[OK], licz[UW], licz[BD]), szer))
    pokaz = lista
    if limit and len(lista) > limit:
        # przy limicie najpierw to, co wymaga uwagi, potem reszta w kolejnosci z paczki
        wazne = [k for k in lista if k[0] != OK]
        reszta = [k for k in lista if k[0] == OK]
        pokaz = (wazne + reszta)[:limit]
        pokaz = [k for k in lista if k in pokaz]
    for status, co, wynik, nr_linii in pokaz:
        gdzie = ('linia %d' % nr_linii) if nr_linii else 'caly log'
        L.extend(zawin('  [%s] %s: %s -> %s' % (status, gdzie, co, wynik), szer, 3 if pelne else (1 if status == OK else 2)))
    if len(pokaz) < len(lista):
        L.append('  ... i %d dalszych kontroli tego ogniwa (--test %s)' % (len(lista) - len(pokaz), nr))
    return L


def linia_pozostalych_ogniw(ses, szer):
    """Jedna linia o ogniwach widocznych w logu poza najwyzszym: ktore maja kontrole z UWAGA albo BRAK DANYCH."""
    inne = [o['nr'] for o in ses.ogniwa if ma_ogniwo(ses, o['nr']) and o['nr'] != ses.ogniwo_w_grze]
    if not inne:
        return []
    czyste, z_uwagami = [], []
    for nr in inne:
        licz = Counter(k[0] for k in kontrole_ogniwa(ses, nr))
        if licz[UW] or licz[BD]:
            z_uwagami.append((nr, licz))
        else:
            czyste.append(nr)
    if not z_uwagami:
        return [obetnij('  pozostale ogniwa w logu (%s): wszystkie kontrole OK | szczegoly: --test wykryte' % ', '.join(czyste),
                        szer)]
    opis = ', '.join('%s%s%s' % (nr, (' UWAGA %d' % licz[UW]) if licz[UW] else '', (' BRAK DANYCH %d' % licz[BD]) if licz[BD]
                                 else '') for nr, licz in z_uwagami)
    return [obetnij('  pozostale ogniwa w logu: z uwagami %s | bez uwag: %s | szczegoly: --test %s'
                    % (opis, ', '.join(czyste) or 'zadne', ','.join(nr for nr, _ in z_uwagami)), szer)]


def drukuj_test(ses, nazwa, szer=SZEROKOSC):
    """Wyjscie opcji --test: kontrole wybranych ogniw w calosci."""
    numery = ogniwa_z_nazwy(ses, nazwa)
    L = []
    if not numery:
        if (nazwa or '').strip().lower() in ('', 'w-grze', 'wgrze', 'gra', 'wykryte', 'lancuch'):
            L.append('--test "%s": w tym logu nie widac zadnego ogniwa lancucha (sesja sprzed wpisu 100 albo sam start gry).'
                     % nazwa)
        else:
            L.append('--test "%s": nie ma takiego ogniwa ani grupy.' % obetnij(nazwa, 60))
        L.append(obetnij('  ogniwa: %s | grupy: %s, wszystkie | takze: wykryte, w-grze'
                         % (', '.join(NR_OGNIW), ', '.join('%s = %s' % (k, '+'.join(v)) for k, v in GRUPY_TESTOWE.items()
                                                           if k != 'wszystkie')), szer))
        return L
    w_grze = ses.ogniwo_w_grze
    L.append(obetnij('=== TEST: ogniwa %s | OGNIWO W GRZE (wg linii logu): %s | doby: %d ==='
                     % (', '.join(numery), w_grze or 'sprzed wpisu 100', len(ses.pelne()) if ses.bloki else 0), szer))
    razem = Counter()
    for nr in numery:
        blok = linie_kontroli(ses, nr, szer, pelne=True)
        L.extend(blok)
        razem.update(k[0] for k in kontrole_ogniwa(ses, nr))
    al = [a for a in ses.alarmy if a[0] == 'ALARM']
    L.append(obetnij('=== RAZEM: OK %d, UWAGA %d, BRAK DANYCH %d | w calym logu: ERROR %d, Exception %d, alarmy %d, uwagi %d '
                     '(pelny skrot: bez --test) ===' % (razem[OK], razem[UW], razem[BD], ses.n_error, ses.n_exception, len(al),
                                                       len(ses.alarmy) - len(al)), szer))
    return L


# =====================================================================================================================
# WYDRUK
# =====================================================================================================================
def probki(indeksy, ile):
    """Pierwszy, rownomiernie rozlozone, ostatni."""
    n = len(indeksy)
    if n <= ile:
        return list(indeksy)
    if ile <= 1:
        return [indeksy[-1]]
    wyn = []
    for i in range(ile):
        k = int(round(i * (n - 1) / float(ile - 1)))
        if indeksy[k] not in wyn:
            wyn.append(indeksy[k])
    return wyn


def linie_naglowka(ses, szer):
    L = []
    nazwa = os.path.basename(ses.sciezka)
    L.append('=== SPRAWDZ LOGI: %s ===' % nazwa)
    ost = ses.wpisy[-1].czas if ses.wpisy else '-'
    pelna = os.path.normpath(ses.sciezka)
    L.append('plik: %s' % (pelna if len(pelna) <= szer - 6 else '...' + pelna[-(szer - 9):]))
    sesja = (ses.naglowek or '').strip('= ').strip()
    L.append('rozmiar %d B, %d linii, kodowanie %s | sesja: %s, ostatni wpis %s'
             % (ses.rozmiar, ses.linii, ses.kodowanie, sesja or 'brak naglowka', ost))
    f = ses.fakty
    if ses.bloki:
        b0, b1 = ses.bloki[0], ses.bloki[-1]
        zakres = 'doby 1-%d = dni gry %d-%d (%d dob)' % (len(ses.bloki), b0.D, b1.D, len(ses.bloki))
        if b1.niepelny:
            zakres += ', ostatnia NIEPELNA (%d tematow) - poza szeregiem' % b1.tematow
        if len(ses.wyb) != len(ses.bloki):
            zakres += ' | --dni: wybrano %d dob (%s)' % (len(ses.wyb), ('%d-%d' % (ses.wyb[0] + 1, ses.wyb[-1] + 1))
                                                       if ses.wyb else 'zadna')
    else:
        zakres = 'BRAK DOB (zadnej linii dziennej - sam start gry albo menu)'
    kamp = 'kampanie/wczytania w sesji: %d' % len(ses.segmenty)
    if len(ses.segmenty) > 1:
        kamp += ' (' + ', '.join('nr %d od linii %d: %d dob' % (i + 1, s[0], len(s[2]))
                                 for i, s in enumerate(ses.segmenty)) + '; analizowana nr %d)' % (ses.wybrany + 1)
    L.append('%s | %s' % (kamp, zakres))
    czesci = []
    czesci.append('rok %s dni' % f['rok_dni'] if 'rok_dni' in f else 'rok: brak linii Kalendarz')
    czesci.append('nowa kampania' if f.get('nowa_kampania') else ('wczytany zapis' if f.get('wczytana') else
                                                                  'nowa kampania / zapis: nie wiadomo'))
    if 'mnoznik_rudy' in f:
        czesci.append('wydobycie rudy x%.1f, drewna x%.1f' % (f['mnoznik_rudy'], f.get('mnoznik_drewna', 0.0)))
    if 'pula_poczatkowa' in f:
        czesci.append('pula wyrzutkow na starcie %d%s' % (f['pula_poczatkowa'], (' (zdjeto %s hearth)' % skr(f['zdjeto_hearth']))
                                                         if 'zdjeto_hearth' in f else ''))
    L.append('start: ' + ' | '.join(czesci))
    ss = f.get('startstock') or []
    if ss:
        op = []
        for w in ss:
            m = re.search(r'StartStock: (\w+) - (\d+) szt\. po .*? -> (\d+) ladunkow po .*?cel (-?\d+)', w.tresc)
            if m:
                op.append('%s %s szt. -> %s ladunkow (cel %s)' % m.groups())
            m = re.search(r'gotowe - .*?; potkniecia (\d+)', w.tresc)
            if m:
                op.append('potkniecia %s' % m.group(1))
        L.append('StartStock (linie %d-%d): %s' % (ss[0].nr, ss[-1].nr, '; '.join(op) if op else
                                                 'bez przeliczenia - ' + obetnij(ss[0].tresc[12:], 100)))
    # linie startowe modulow
    licz = Counter(status_linii(w.tresc) for w in ses.start if w.pref)
    L.append('linie startowe modulow: %d (wpiete %d, CZYNNE %d, BRAK %d, NIE wpieta %d, WYLACZONE %d, pozostale %d)'
             % (sum(licz.values()), licz['wpiete'], licz['CZYNNE'], licz['BRAK'], licz['NIE wpieta'], licz['WYLACZONE'],
                licz['inne']))
    zle = [w for w in ses.start if w.pref and status_linii(w.tresc) in ('BRAK', 'NIE wpieta', 'WYLACZONE')]
    for w in zle[:5]:
        L.append('  [%s] linia %d: %s' % (status_linii(w.tresc), w.nr, wycinek(w.tresc, szer - 30)))
    if len(zle) > 5:
        L.append('  ... i %d dalszych (linie %s)' % (len(zle) - 5, ', '.join(str(w.nr) for w in zle[5:15])))
    brak_startu = []
    for nazwa_o, wz in OCZEKIWANE_STARTU:
        rx = re.compile(wz)
        if not any(rx.search(w.tresc) for w in ses.start):
            brak_startu.append(nazwa_o)
    L.append('oczekiwane linie startu (STAN-PRAC): %d z %d%s'
             % (len(OCZEKIWANE_STARTU) - len(brak_startu), len(OCZEKIWANE_STARTU),
                (' - BRAK: ' + ', '.join(brak_startu)) if brak_startu else ''))
    # uklad linii ksiegi pieniadza (wpis 102, ktory jest w grze, albo poprawka 102b)
    uk = f.get('uklad_ksiegi')
    if uk:
        ul = f.get('uklad_linii') or {}
        if uk == UKLAD_102B:
            op = 'uklad 102b - bilans z "rozliczenia rodow na plus / na minus" i "W tym zold naliczony", linia "Pieniadz swiata (rody):"'
        else:
            op = 'uklad wpisu 102 - zold jest pozycja ujsc bilansu i siedzi tez w saldach rodow (reszta zawyzona o zold; naprawia 102b)'
        if f.get('uklad_mieszany'):
            op += ' | UWAGA: linie w obu ukladach (102: %d, 102b: %d)' % (ul.get(UKLAD_102, 0), ul.get(UKLAD_102B, 0))
        elif not ul:
            op += ' (wg linii startowej - linii dziennych ksiegi jeszcze nie ma)'
        L.append('ksiega pieniadza: ' + op)
    # lancuch
    w_grze = ses.ogniwo_w_grze
    nowa = bool(f.get('nowa_kampania'))
    # ogniwo 104 nie ma linii przy starcie gry - jego linie "StartStock:" pojawiaja sie tylko przy starcie kampanii
    brakujace = [o for o in ses.ogniwa if not o['jest'] and (o['zawsze'] or nowa) and o['ix'] >= ix_ogniwa('102')]
    L.append('OGNIWO W GRZE (wg linii logu): %s%s' % (
        ('%s %s' % (w_grze, [o['opis'] for o in ses.ogniwa if o['nr'] == w_grze][0])) if w_grze else 'sprzed wpisu 100',
        (' | NIE MA linii startowych ogniw: ' + ', '.join(o['nr'] for o in brakujace)) if brakujace else
        ' | komplet linii startowych lancucha 102-109'))
    for o in ses.ogniwa:
        if o['jest']:
            op = 'JEST  linia %d%s %s' % (o['wpis'].nr, (' [%s]' % o['status']) if o['status'] != 'inne' else '',
                                          wycinek(o['wpis'].tresc, 62))
        elif not o['zawsze']:
            wyzsze = any(x['jest'] for x in ses.ogniwa if x['ix'] > o['ix'] and x['zawsze'])
            if nowa:
                op = 'BRAK  linii "StartStock:" przy starcie NOWEJ kampanii -> ogniwo nie wgrane' + (
                    ' (a wyzsze ogniwo jest - patrz alarmy)' if wyzsze else '')
            else:
                op = 'brak linii "StartStock:" - ' + ('wyzsze ogniwo wgrane, wiec 104 jest (linie tylko przy starcie nowej '
                                                      'kampanii)' if wyzsze else 'nie do rozstrzygniecia (wczytany zapis; '
                                                                                  'linie tylko przy starcie nowej kampanii)')
        else:
            op = 'BRAK  poznac po: ' + o['poznac']
        dz = ', '.join('"%s:" %d dob' % (n, c) for n, c in o['dob'].items())
        if o['znak']:
            dz = (dz + ', ' if dz else '') + '"%s:" z odcinkiem ogniwa %d dob' % (o['znak'][0], o['znak_dob'] or 0)
        if not o['jest'] and o['zawsze'] and not any(o['linii'].values()) and not o['znak_dob']:
            dz = ''                         # brak ogniwa: miejsce zajmuje opis, po czym je poznac
        L.append('  %-4s %-17s %s%s' % (o['nr'], o['opis'], op, (' | ' + dz) if dz else ''))
    return [obetnij(x, szer) for x in L]


def linie_bledow(ses, szer, pelny):
    L = ['--- BLEDY: ERROR %d, Exception %d, potkniecia niezerowe %d ---' % (ses.n_error, ses.n_exception, ses.n_potkniec)]
    lim = len(ses.bledy) if pelny else MAKS_GRUP_BLEDOW
    for klucz, ile, nr, przyklad in ses.bledy[:lim]:
        L.append(obetnij('  %3dx %s  (pierwsze: linia %d)%s' % (ile, klucz, nr, (' ' + przyklad) if przyklad else ''), szer))
    if len(ses.bledy) > lim:
        L.append('  ... i %d dalszych grup (--pelny)' % (len(ses.bledy) - lim))
    return L


def linie_alarmow(ses, szer, pelny):
    ile_a = sum(1 for a in ses.alarmy if a[0] == 'ALARM')
    L = ['--- ALARMY: %d, uwagi: %d (progi: stale na gorze skryptu) ---' % (ile_a, len(ses.alarmy) - ile_a)]
    lim = len(ses.alarmy) if pelny else MAKS_ALARMOW
    for poziom, nr, tekst in ses.alarmy[:lim]:
        gdzie = ('linia %d' % nr) if nr else 'caly log'
        L.append(obetnij('  [%s] %s: %s' % (poziom, gdzie, tekst), szer))
    if len(ses.alarmy) > lim:
        L.append('  ... i %d dalszych (--pelny)' % (len(ses.alarmy) - lim))
    if not ses.alarmy:
        L.append('  brak')
    return L


def wiersze_tematu(ses, t, bloki_probki):
    """[(etykieta tematu, etykieta miary, [komorki])] dla skrotu."""
    s = ses.szer.get(t.nazwa) or {}
    wyn = []
    for etykieta, szablon in t.skrot:
        kom = [wypelnij(szablon, s.get(bi)) for bi in bloki_probki]
        if all(k == '-' for k in kom):
            wszystkie = [wypelnij(szablon, s.get(bi)) for bi in ses.wyb]
            if all(k == '-' for k in wszystkie):
                continue
        wyn.append((NAZWY_KROTKIE.get(t.nazwa, t.nazwa), etykieta, kom))
    return wyn


def tabela_grupy(ses, tytul, tematy, szer, ile_probek, surowe_tematy):
    """Linie tabeli jednej grupy; liczba probek zmniejszana, az tabela zmiesci sie w szerokosci."""
    pelne = ses.pelne()
    if not pelne:
        return []
    ile = min(ile_probek, len(pelne))
    while True:
        pr = probki(pelne, ile)
        wiersze = []
        for t in tematy:
            if t.nazwa in surowe_tematy:
                continue
            wiersze.extend(wiersze_tematu(ses, t, pr))
        if not wiersze:
            break
        w1 = min(22, max(len(w[0]) for w in wiersze))
        w2 = min(38, max(len(w[1]) for w in wiersze))
        szerokosci = []
        for k in range(len(pr)):
            szerokosci.append(max([len(str(pr[k] + 1)) + 1] + [len(w[2][k]) for w in wiersze]) + 2)
        if w1 + 1 + w2 + sum(szerokosci) <= szer or ile <= 2:
            break
        ile -= 1
    L = []
    if wiersze:
        glowa = ('--- %s ---' % tytul).ljust(w1 + 1 + w2 - 5) + 'doba:'
        glowa += ''.join(str(pr[k] + 1).rjust(szerokosci[k]) for k in range(len(pr)))
        L.append(obetnij(glowa, szer))
        poprzedni = None
        for nazwa, etykieta, kom in wiersze:
            lewa = (obetnij(nazwa, w1) if nazwa != poprzedni else '').ljust(w1) + ' ' + obetnij(etykieta, w2).ljust(w2)
            L.append(obetnij(lewa + ''.join(kom[k].rjust(szerokosci[k]) for k in range(len(pr))), szer))
            poprzedni = nazwa
    # tematy w trybie surowym (format nierozpoznany): pierwsza, srodkowa, ostatnia linia
    for t in tematy:
        if t.nazwa not in surowe_tematy:
            continue
        lin = [w for w in ses.linie_tematu.get(t.nazwa, []) if w.blok is None or w.blok in ses.wyb]
        if not lin:
            continue
        if not L:
            L.append('--- %s ---' % tytul)
        wyb = [lin[0]] if len(lin) == 1 else ([lin[0], lin[-1]] if len(lin) == 2 else [lin[0], lin[len(lin) // 2], lin[-1]])
        L.append('%s  [TRYB SUROWY - format nierozpoznany, %d linii]' % (t.nazwa, len(lin)))
        for w in wyb:
            L.append(obetnij('  doba %s linia %d: %s' % ((w.blok + 1) if w.blok is not None else '-', w.nr, w.tresc), szer))
    return L


def tematy_surowe(ses):
    """Tematy, ktore spadaja do trybu surowego: zadna linia dzienna nie przeszla parsera."""
    wyn = set()
    for t in TEMATY:
        if not t.dzienny or (not t.kol and t.extra is None):
            continue
        linie = ses.linie_tematu.get(t.nazwa) or []
        if linie and not (ses.szer.get(t.nazwa) or {}):
            wyn.add(t.nazwa)
    return wyn


def linie_nierozpoznanych(ses, szer):
    L = []
    surowe = tematy_surowe(ses)
    for t in TEMATY:
        zle = ses.zle.get(t.nazwa) or []
        if not zle:
            continue
        linie = ses.linie_tematu.get(t.nazwa) or []
        braki = Counter()
        for w, b in zle:
            braki.update(b)
        L.append(obetnij('  %s: %d z %d linii nie pasuje do wzorca (brak pol: %s; pierwsza: linia %d)%s'
                         % (t.nazwa, len(zle), len(linie), ', '.join(k for k, _ in braki.most_common(6)), zle[0][0].nr,
                            ' -> TRYB SUROWY' if t.nazwa in surowe else ' -> te doby pominiete w szeregu'), szer))
    for pref, wpisy in ses.obce.items():
        if pref.endswith('(inny rodzaj linii)'):
            L.append(obetnij('  %s: %d linii o nieznanym poczatku (pierwsza: linia %d: %s)'
                             % (pref, len(wpisy), wpisy[0].nr, wpisy[0].tresc), szer))
    for nazwa, s in ses.szer.items():
        powt = [bi for bi, d in s.items() if isinstance(d, dict) and d.get('_powtorzen')]
        if powt:
            L.append(obetnij('  %s: w %d dobach wiecej niz jedna linia (brana ostatnia), np. doba %d'
                             % (nazwa, len(powt), min(powt) + 1), szer))
    for u in ses.uwagi:
        L.append(obetnij('  ' + u, szer))
    return ['--- FORMATY NIEROZPOZNANE: %d ---' % len(L)] + (L if L else ['  brak'])


def linie_obcych(ses, szer, surowe=False):
    """Prefiksy bez parsera, ktore powtarzaja sie w dobach: jedna linia na prefiks."""
    L = []
    for pref, wpisy in ses.obce.items():
        if pref.endswith('(inny rodzaj linii)'):
            continue
        wp = [w for w in wpisy if w.blok in ses.wyb]
        dob = len(set(w.blok for w in wp))
        if dob < 2 and not any(w.dzien is not None for w in wp):
            continue
        if not wp:
            continue
        if surowe and len(wp) > 1:
            L.append(obetnij('%s [x%d, %d dob] doba %d linia %d: %s' % (pref, len(wp), dob, wp[0].blok + 1, wp[0].nr,
                                                                       wp[0].reszta or ''), szer))
        L.append(obetnij('%s [x%d, %d dob] doba %d linia %d: %s' % (pref, len(wp), dob, wp[-1].blok + 1, wp[-1].nr,
                                                                   wp[-1].reszta or ''), szer))
    return L


def drukuj_skrot(ses, szer=SZEROKOSC, pelny=False, surowe=False, limit=LIMIT_LINII):
    L = []
    L.extend(linie_naglowka(ses, szer))
    L.extend(linie_bledow(ses, szer, pelny))
    L.extend(linie_alarmow(ses, szer, pelny))
    # kontrole najwyzszego ogniwa wykrytego w logu ("po czym poznac w logu" z jego paczki)
    if ses.ogniwo_w_grze:
        try:
            L.extend(linie_kontroli(ses, ses.ogniwo_w_grze, szer, None if pelny else MAKS_KONTROLI, pelne=pelny))
            L.extend(linia_pozostalych_ogniw(ses, szer))
        except Exception as e:
            ses.uwagi.append('kontrole ogniwa %s: blad wewnetrzny (%s: %s)' % (ses.ogniwo_w_grze, type(e).__name__, e))
    nier = linie_nierozpoznanych(ses, szer)
    sur = tematy_surowe(ses)
    if surowe:
        sur = set(t.nazwa for t in TEMATY)
    obce = linie_obcych(ses, szer, surowe)
    cialo, dodatkowe = [], []
    for g, tytul in GRUPY.items():
        tematy = [t for t in TEMATY if t.grupa == g and t.dzienny]
        if surowe:
            blok = _surowe_grupy(ses, tytul, tematy, szer)
        else:
            blok = tabela_grupy(ses, tytul, tematy, szer, PROBKI, sur)
        if g == 'dodatkowe':
            dodatkowe = blok
        else:
            cialo.extend(blok)
    zd = []
    for t in TEMATY:
        if t.dzienny:
            continue
        ile = len([w for w in ses.linie_tematu.get(t.nazwa, []) if w.blok in ses.wyb])
        if ile:
            zd.append('%s x%d' % (t.nazwa, ile))
    if zd:
        dodatkowe = dodatkowe + [obetnij('linie niedzienne w dobach (zdarzenia; --temat NAZWA): ' + ', '.join(zd), szer)]
    if obce:
        dodatkowe = dodatkowe + ['--- POZOSTALE PREFIKSY DZIENNE (bez parsera, linia obcieta) ---'] + obce
    stale = len(L) + len(cialo) + len(nier)
    if not pelny and limit and stale + len(dodatkowe) > limit:
        miejsce = max(3, limit - stale - 1)
        pominiete = len(dodatkowe) - miejsce
        dodatkowe = dodatkowe[:miejsce] + ['  ... %d dalszych wierszy tematow dodatkowych (--pelny albo --temat NAZWA)'
                                           % pominiete]
    L.extend(cialo)
    L.extend(dodatkowe)
    L.extend(nier)
    return L


def _surowe_grupy(ses, tytul, tematy, szer):
    L = []
    for t in tematy:
        lin = [w for w in ses.linie_tematu.get(t.nazwa, []) if w.blok in ses.wyb]
        if not lin:
            continue
        if not L:
            L.append('--- %s (linie surowe: pierwsza i ostatnia) ---' % tytul)
        for w in ([lin[0]] if len(lin) == 1 else [lin[0], lin[-1]]):
            L.append(obetnij('%s [x%d] doba %d linia %d: %s' % (t.nazwa, len(lin), w.blok + 1, w.nr, w.reszta or ''), szer))
    return L


# ------------------------------------------------------------------------------------------------ --temat
def znajdz_tematy(ses, nazwa):
    """Nazwy tematow (z parserem) i prefiksow obcych pasujace do zapytania."""
    q = nazwa.strip().lower().rstrip(':')
    nazwy = list(TEMATY_WG_NAZWY.keys())
    obce = [p for p in ses.obce.keys()]
    inne = sorted(set(w.pref for w in ses.wpisy if w.pref) - set(t.prefiks for t in TEMATY) - set(obce))
    for pula in (nazwy, obce, inne):
        dokl = [n for n in pula if n.lower() == q]
        if dokl:
            return dokl
    wsz = nazwy + obce + inne
    pocz = [n for n in wsz if n.lower().startswith(q)]
    if pocz:
        return pocz
    return [n for n in wsz if q in n.lower()]


def tabela_pelna(naglowki, wiersze, szer, stale=3):
    """Tabela wiersz = doba; kolumny lamane na kawalki mieszczace sie w szerokosci (pierwsze 'stale' powtarzane)."""
    L = []
    if not wiersze:
        return L
    szerok = [max(len(str(naglowki[i])), max(len(str(w[i])) for w in wiersze)) for i in range(len(naglowki))]
    szerok = [min(s, 40) for s in szerok]
    baza = sum(szerok[:stale]) + stale
    i = stale
    while i < len(naglowki) or (i == stale and len(naglowki) == stale):
        j, zajete = i, baza
        while j < len(naglowki) and (zajete + szerok[j] + 1 <= szer or j == i):
            zajete += szerok[j] + 1
            j += 1
        kol = list(range(stale)) + list(range(i, j))
        L.append(obetnij(' '.join(obetnij(str(naglowki[k]), szerok[k]).rjust(szerok[k]) for k in kol), szer))
        for w in wiersze:
            L.append(obetnij(' '.join(obetnij(str(w[k]), szerok[k]).rjust(szerok[k]) for k in kol), szer))
        if j >= len(naglowki):
            break
        L.append('')
        i = j
    return L


KOLUMNY_ZE_ZNAKIEM = ('zmiana', 'bez_wyjasnienia', 'reszta', 'bil_zmiana', 'bil_reszta', 'rozjazd')


def _komorka_pelna(klucz, v):
    """Komorka tabeli --temat: pelna liczba, zmiany ze znakiem."""
    if v is None:
        return '-'
    if isinstance(v, bool):
        return 'TAK' if v else 'nie'
    if isinstance(v, float):
        return '%g' % v
    if isinstance(v, int) and (klucz.endswith('_zm') or klucz in KOLUMNY_ZE_ZNAKIEM):
        return '%+d' % v
    return v


def drukuj_temat(ses, nazwa, szer=SZEROKOSC, surowe=False):
    L = []
    trafione = znajdz_tematy(ses, nazwa)
    if not trafione:
        L.append('temat "%s": nic nie pasuje. Tematy z parserem: %s' % (nazwa, ', '.join(TEMATY_WG_NAZWY.keys())))
        L.append('prefiksy bez parsera w tym logu: %s' % (', '.join(sorted(ses.obce.keys())) or 'brak'))
        return L
    for n in trafione:
        t = TEMATY_WG_NAZWY.get(n)
        if t is None:
            lin = [w for w in ses.wpisy if w.pref == n or (n in ses.obce and w in ses.obce[n])]
            L.append('=== PREFIKS BEZ PARSERA: %s (%d linii) ===' % (n, len(lin)))
            for w in lin:
                if w.blok is not None and w.blok not in ses.wyb:
                    continue
                L.append('doba %s linia %d: %s' % ((w.blok + 1) if w.blok is not None else '-', w.nr, w.tresc))
            continue
        lin = [w for w in ses.linie_tematu.get(n, []) if w.blok is None or w.blok in ses.wyb]
        if not lin:                 # linie startu gry stoja przed znacznikiem kampanii - nie ma ich wsrod linii tematu
            lin = [w for w in ses.start if w.pref == t.prefiks and t.pasuje(w.reszta or '')]
        s = ses.szer.get(n) or {}
        L.append('=== TEMAT: %s (prefiks "%s:", %d linii, %d dob z danymi%s) ==='
                 % (n, t.prefiks, len(lin), len([bi for bi in ses.wyb if bi in s]),
                    (', linie %d-%d' % (lin[0].nr, lin[-1].nr)) if lin else ''))
        if not lin:
            L.append('  brak linii w tym logu')
            continue
        if surowe or not s:
            if not surowe:
                L.append('  (bez kolumn: temat jednorazowy albo format nierozpoznany - linie surowe)')
            for w in lin:
                L.append('doba %s linia %d: %s' % ((w.blok + 1) if w.blok is not None else '-', w.nr, w.tresc))
            continue
        klucze = []
        for bi in ses.wyb:
            for k in (s.get(bi) or {}):
                if not k.startswith('_') and k not in klucze:
                    klucze.append(k)
        naglowki = ['doba', 'dzien', 'linia'] + klucze
        wiersze = []
        for bi in ses.wyb:
            d = s.get(bi)
            if d is None:
                continue
            wiersze.append([bi + 1, ses.bloki[bi].D, d.get('_nr', '')] +
                           [_komorka_pelna(k, d.get(k)) for k in klucze])
        L.extend(tabela_pelna(naglowki, wiersze, szer))
        if t.wielo and t.nazwa == 'Skarbce':
            L.append('')
            L.append('krolestwa: pierwsza i ostatnia doba (skarbiec, kiesy rodow, wojsko rodow)')
            wg = ses.wiersze.get(n) or {}
            dob = [bi for bi in ses.wyb if bi in wg]
            if dob:
                a = dict((w['krolestwo'], w) for w in wg[dob[0]])
                b = dict((w['krolestwo'], w) for w in wg[dob[-1]])
                nag = ['krolestwo', 'wojna', 'skarbiec:%d' % (dob[0] + 1), 'skarbiec:%d' % (dob[-1] + 1),
                       'kiesy:%d' % (dob[0] + 1), 'kiesy:%d' % (dob[-1] + 1), 'wojsko:%d' % (dob[0] + 1),
                       'wojsko:%d' % (dob[-1] + 1), 'zwrot:%d' % (dob[-1] + 1)]
                wr = []
                for k in list(a.keys()) + [x for x in b.keys() if x not in a]:
                    x, y = a.get(k, {}), b.get(k, {})
                    wr.append([k, 'TAK' if y.get('wojna') else 'nie', x.get('skarbiec', '-'), y.get('skarbiec', '-'),
                               x.get('kiesy', '-'), y.get('kiesy', '-'), x.get('wojsko', '-'), y.get('wojsko', '-'),
                               y.get('zwrot') if y.get('zwrot') is not None else '-'])
                L.extend(tabela_pelna(nag, wr, szer, stale=1))
        zle = ses.zle.get(n) or []
        if zle:
            L.append('  UWAGA: %d linii nie pasuje do wzorca (pierwsza: linia %d, brak pol: %s)'
                     % (len(zle), zle[0][0].nr, ', '.join(zle[0][1])))
    return L


# ------------------------------------------------------------------------------------------------ --porownaj
def drukuj_porownanie(a, b, szer=SZEROKOSC):
    L = ['=== POROWNANIE SESJI: A = %s | B = %s ===' % (os.path.basename(a.sciezka), os.path.basename(b.sciezka))]
    for znak, s in (('A', a), ('B', b)):
        ile_a = sum(1 for x in s.alarmy if x[0] == 'ALARM')
        zakres = ('doby 1-%d = dni gry %d-%d' % (len(s.bloki), s.bloki[0].D, s.bloki[-1].D)) if s.bloki else 'brak dob'
        L.append(obetnij('%s: %s | %d linii | %s | ogniwo w grze: %s | ERROR %d, Exception %d, potkniecia %d | alarmy %d, '
                         'uwagi %d' % (znak, os.path.basename(s.sciezka), s.linii, zakres, s.ogniwo_w_grze or 'sprzed 100',
                                       s.n_error, s.n_exception, s.n_potkniec, ile_a, len(s.alarmy) - ile_a), szer))
    pa, pb = a.pelne(), b.pelne()
    if not pa and not pb:
        L.append('zadna z sesji nie ma dob - nie ma czego porownac')
        return L
    kol = [('A:doba %d' % (pa[0] + 1), a, pa[0]) if pa else None, ('A:doba %d' % (pa[-1] + 1), a, pa[-1]) if pa else None,
           ('B:doba %d' % (pb[0] + 1), b, pb[0]) if pb else None, ('B:doba %d' % (pb[-1] + 1), b, pb[-1]) if pb else None]
    wiersze = []
    for t in TEMATY:
        if not t.dzienny or not t.skrot:
            continue
        for etykieta, szablon in t.skrot:
            kom = []
            for k in kol:
                kom.append('-' if k is None else wypelnij(szablon, (k[1].szer.get(t.nazwa) or {}).get(k[2])))
            if all(x == '-' for x in kom):
                continue
            wiersze.append((t.nazwa, etykieta, kom))
    if not wiersze:
        L.append('brak wspolnych tematow z danymi')
        return L
    w1 = min(28, max(len(w[0]) for w in wiersze))
    w2 = min(46, max(len(w[1]) for w in wiersze))
    sz = []
    for i in range(4):
        sz.append(max([len(kol[i][0]) if kol[i] else 1] + [len(w[2][i]) for w in wiersze]) + 2)
    nadmiar = w1 + 1 + w2 + sum(sz) - szer
    if nadmiar > 0:
        w2 = max(20, w2 - nadmiar)
    L.append(obetnij(' ' * (w1 + 1 + w2) + ''.join((kol[i][0] if kol[i] else '-').rjust(sz[i]) for i in range(4)), szer))
    poprzedni = None
    for nazwa, etykieta, kom in wiersze:
        lewa = (obetnij(nazwa, w1) if nazwa != poprzedni else '').ljust(w1) + ' ' + obetnij(etykieta, w2).ljust(w2)
        L.append(obetnij(lewa + ''.join(kom[i].rjust(sz[i]) for i in range(4)), szer))
        poprzedni = nazwa
    for znak, s in (('A', a), ('B', b)):
        al = [x for x in s.alarmy if x[0] == 'ALARM']
        L.append('--- alarmy %s: %d ---' % (znak, len(al)))
        for poziom, nr, tekst in al[:10]:
            L.append(obetnij('  [%s] %s: %s' % (poziom, ('linia %d' % nr) if nr else 'caly log', tekst), szer))
        if len(al) > 10:
            L.append('  ... i %d dalszych' % (len(al) - 10))
    return L


# ------------------------------------------------------------------------------------------------ --csv
def sciezka_csv(sciezka_logu):
    kat = os.path.dirname(os.path.abspath(sciezka_logu))
    baza = os.path.splitext(os.path.basename(sciezka_logu))[0]
    sesja = baza[len('Armoury-'):] if baza.startswith('Armoury-') else baza
    for kandydat in (os.path.join(kat, 'Logs', sesja, PLIK_CSV), os.path.join(kat, sesja, PLIK_CSV),
                     os.path.join(kat, PLIK_CSV)):
        if os.path.isfile(kandydat):
            return kandydat
    return os.path.join(kat, 'Logs', sesja, PLIK_CSV)


def skrot_csv_z_tekstu(tekst, szer=SZEROKOSC, ile=8):
    """Skrot pliku ludzie-regiony.csv: regiony o najwiekszym obciazeniu w ostatniej dobie."""
    L = []
    linie = [x for x in tekst.splitlines() if x.strip()]
    if len(linie) < 2:
        return ['plik CSV pusty albo sam naglowek']
    nagl = [x.strip() for x in linie[0].split(';')]
    idx = dict((n, i) for i, n in enumerate(nagl))
    if 'dzien' not in idx or 'obciazenie_proc_mezczyzn' not in idx:
        return ['plik CSV ma inny naglowek niz oczekiwany (brak kolumn dzien / obciazenie_proc_mezczyzn): '
                + obetnij(linie[0], szer - 90)]
    wiersze, zle = [], 0
    for x in linie[1:]:
        p = x.split(';')
        if len(p) < len(nagl):
            zle += 1
            continue
        wiersze.append(p)

    def pole(p, n):
        return p[idx[n]].strip() if n in idx and idx[n] < len(p) else ''

    def lf(s):
        try:
            return float(s.replace(',', '.'))
        except ValueError:
            return None
    dni = sorted(set(int(lf(pole(p, 'dzien'))) for p in wiersze if lf(pole(p, 'dzien')) is not None))
    if not dni:
        return ['plik CSV bez wierszy z numerem dnia']
    ostatni = dni[-1]
    dzis = [p for p in wiersze if lf(pole(p, 'dzien')) == ostatni]
    z_obc = [(lf(pole(p, 'obciazenie_proc_mezczyzn')), p) for p in dzis]
    ranking = sorted([x for x in z_obc if x[0] is not None], key=lambda x: -x[0])
    ponad = sum(1 for x in ranking if x[0] > 20.0)
    L.append('dni w pliku: %d-%d (%d dob), wierszy %d%s; ostatnia doba %d: regionow %d, z ludnoscia %d, ponad prog 20%% '
             'mezczyzn: %d' % (dni[0], ostatni, len(dni), len(wiersze), (', uszkodzonych %d' % zle) if zle else '',
                              ostatni, len(dzis), len(ranking), ponad))
    kol = [('region', 'region'), ('rodzaj', 'rodzaj'), ('kultura', 'kultura'), ('krolestwo', 'krolestwo'),
           ('ludzie', 'ludzie'), ('zmiana', 'ludzie_zmiana'), ('zaloga', 'zaloga'), ('milicja', 'milicja'),
           ('wyrzutki', 'wyrzutki'), ('bandy', 'bandy_ludzie'), ('zabici', 'zabici_dzis'), ('zwerb.', 'zwerbowani_dzis'),
           ('obciaz.%', 'obciazenie_proc_mezczyzn'), ('bezp.', 'bezpieczenstwo'), ('bil.zywn.', 'bilans_zywnosci'),
           ('glod', 'glod'), ('wojna', 'wojna')]
    kol = [k for k in kol if k[1] in idx]
    nag = ['nr'] + [k[0] for k in kol]
    wr = []
    for i, (o, p) in enumerate(ranking[:ile], 1):
        wr.append([i] + [obetnij(pole(p, k[1]), 26) for k in kol])
    L.extend(tabela_pelna(nag, wr, szer, stale=2))
    return L


def drukuj_csv(sciezka_logu, szer=SZEROKOSC):
    sc = sciezka_csv(sciezka_logu)
    L = ['=== CSV: %s ===' % sc]
    if not os.path.isfile(sc):
        L.append('pliku nie ma (ksiega ludzi pisze go od wpisu 102; katalog sesji: Logs\\<sesja>\\)')
        return L
    try:
        with open(sc, 'rb') as f:
            b = f.read()
        tekst, kod = dekoduj(b)
        L.append('rozmiar %d B, kodowanie %s' % (len(b), kod))
        L.extend(skrot_csv_z_tekstu(tekst, szer))
    except Exception as e:
        L.append('nie udalo sie odczytac pliku (%s: %s)' % (type(e).__name__, e))
    return L


# =====================================================================================================================
# WEJSCIE
# =====================================================================================================================
def najnowszy_log(katalog=KATALOG_LOGOW):
    pliki = glob.glob(os.path.join(katalog, WZORZEC_LOGU))
    if not pliki:
        return None
    return max(pliki, key=lambda p: (os.path.getmtime(p), p))


def wczytaj_sesje(sciezka, wczytanie=None, dni=None):
    with open(sciezka, 'rb') as f:
        b = f.read()
    tekst, kod = dekoduj(b)
    return analizuj(tekst, sciezka, kod, len(b), wczytanie, dni)


def parsuj_dni(s):
    m = re.match(r'^\s*(\d+)\s*(?:-|\.\.|:)\s*(\d+)\s*$', s or '')
    if m:
        a, b = int(m.group(1)), int(m.group(2))
        return (min(a, b), max(a, b))
    m = re.match(r'^\s*(\d+)\s*$', s or '')
    if m:
        return (int(m.group(1)), int(m.group(1)))
    return None


def wypisz(linie):
    out = sys.stdout
    try:
        out.reconfigure(encoding='utf-8', errors='replace')
    except Exception:
        pass
    for x in linie:
        try:
            out.write(x + '\n')
        except Exception:
            out.write(x.encode('ascii', 'replace').decode('ascii') + '\n')


def main(argv=None):
    ap = argparse.ArgumentParser(description='Zwiezly skrot sesji logu Armoury (tylko stdout).', add_help=True)
    ap.add_argument('log', nargs='?', help='sciezka logu; domyslnie najnowszy Armoury-*.log w katalogu moda')
    ap.add_argument('--temat', help='pelny szereg jednego tematu (wszystkie doby)')
    ap.add_argument('--dni', help='zakres dob A-B (numery porzadkowe albo dni gry)')
    ap.add_argument('--surowe', action='store_true', help='linie bez parsowania')
    ap.add_argument('--porownaj', metavar='LOG2', help='drugi log: pierwsza i ostatnia doba kazdego tematu obok siebie')
    ap.add_argument('--csv', action='store_true', help='skrot pliku ludzie-regiony.csv z katalogu sesji')
    ap.add_argument('--wczytanie', type=int, help='numer kampanii / wczytania w logu (od 1)')
    ap.add_argument('--test', metavar='NAZWA', help='kontrole "po czym poznac w logu" ogniwa albo grupy: 102b, 106, 103,104, '
                                                    '102b-105, surowce, pieniadz, ludzie, wykryte, wszystkie')
    ap.add_argument('--pelny', action='store_true', help='bez limitu dlugosci skrotu')
    ap.add_argument('--szer', type=int, default=SZEROKOSC, help='szerokosc wyjscia (domyslnie %d)' % SZEROKOSC)
    try:
        a = ap.parse_args(argv)
    except SystemExit as e:
        return e.code if isinstance(e.code, int) else 2
    szer = max(60, a.szer or SZEROKOSC)
    sciezka = a.log or najnowszy_log()
    if not sciezka:
        wypisz(['nie znaleziono zadnego logu: %s' % os.path.join(KATALOG_LOGOW, WZORZEC_LOGU)])
        return 1
    if not os.path.isfile(sciezka):
        wypisz(['nie ma pliku: %s' % sciezka])
        return 1
    dni = parsuj_dni(a.dni) if a.dni else None
    if a.dni and dni is None:
        wypisz(['--dni: nie rozumiem "%s" (przyklady: 5-12, 108840-108850, 7)' % a.dni])
        return 2
    try:
        if a.csv:
            wypisz(drukuj_csv(sciezka, szer))
            return 0
        ses = wczytaj_sesje(sciezka, a.wczytanie, dni)
        if a.porownaj:
            if not os.path.isfile(a.porownaj):
                wypisz(['nie ma pliku: %s' % a.porownaj])
                return 1
            wypisz(drukuj_porownanie(ses, wczytaj_sesje(a.porownaj, None, dni), szer))
        elif a.test:
            wypisz(linie_naglowka(ses, szer)[:4] + drukuj_test(ses, a.test, szer))
        elif a.temat:
            wypisz(linie_naglowka(ses, szer)[:3] + drukuj_temat(ses, a.temat, szer, a.surowe))
        else:
            wypisz(drukuj_skrot(ses, szer, a.pelny, a.surowe))
        return 0
    except Exception as e:     # ostatnia siatka: skrypt ma zawsze cos powiedziec
        import traceback
        tb = traceback.extract_tb(sys.exc_info()[2])
        gdzie = ('%s:%d' % (os.path.basename(tb[-1].filename), tb[-1].lineno)) if tb else '?'
        wypisz(['BLAD WEWNETRZNY skryptu (%s: %s w %s) - plik: %s' % (type(e).__name__, e, gdzie, sciezka),
                'sprobuj: --surowe albo --temat NAZWA --surowe'])
        return 3


if __name__ == '__main__':
    sys.exit(main())
