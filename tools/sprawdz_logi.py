#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
sprawdz_logi.py - zwiezly skrot sesji logu moda Armoury (Bannerlord / Realm of Thrones).

UZYCIE
  python sprawdz_logi.py [sciezka-logu] [opcje]
      bez sciezki: najnowszy  <gra>\\Modules\\Armoury\\Armoury-*.log
  --temat NAZWA      pelny szereg jednego tematu (wszystkie doby, wszystkie kolumny); nazwa bez rozrozniania
                     wielkosci liter: najpierw dokladna ("ruda"), potem poczatek ("przeplywy" = 4 tematy), potem fragment
  --dni A-B          tylko doby A-B (numery porzadkowe 1..N; liczby >= 10000 = dni gry, np. 108840-108850)
  --surowe           linie bez parsowania (z --temat: wszystkie linie tematu w calosci; bez: pierwsza i ostatnia kazdego tematu)
  --porownaj LOG2    ten sam skrot dla dwoch sesji obok siebie: pierwsza i ostatnia doba kazdego tematu
  --csv              skrot pliku ludzie-regiony.csv z katalogu sesji (Logs\\<sesja>\\): 8 regionow o najwiekszym obciazeniu
  --wczytanie N      gdy w jednym logu jest kilka kampanii / wczytan: ktora analizowac (domyslnie ta z najwieksza liczba dob)
  --pelny            bez limitu ok. 150 linii (wszystkie alarmy i wszystkie tematy dodatkowe)
  --szer N           szerokosc wyjscia (domyslnie 160)

UKLAD SKROTU (domyslnie ok. 150 linii, do 160 znakow)
  1. naglowek: plik, rozmiar, doby i dni gry, linie startowe modulow (wpiete / CZYNNE / BRAK / NIE wpieta / WYLACZONE),
     ogniwa lancucha paczek 100-107 (ktore jest w grze, ktorych linii startowych nie ma)
  2. bledy: ERROR / Exception / potkniecia, pogrupowane, z numerem linii pierwszego wystapienia
  3. alarmy: reguly z docs\\paczki\\*.md i z raportu nocnego, kazdy z numerem linii logu (progi = stale na gorze pliku)
  4. szeregi dzienne: wiersz = miara, kolumny = pierwsza doba, kilka rownomiernie rozlozonych, ostatnia
  5. formaty nierozpoznane: tematy, ktore spadly do trybu surowego (zmieniony albo nieznany format linii)

TEMATY Z PARSEREM KOLUMN (prefiks linii logu)
  surowce:   Ruda, Drewno, Dowoz, Dowoz (skutki), Karawany, Karawany (stan), Warsztaty, Rynek surowcow, Rynek broni,
             Mineraly (dubel BK)
  pieniadz:  Pieniadz swiata, Pieniadz swiata (bilans), Przeplywy osad, Przeplywy osad (kasy miast | kasy zamkow |
             kiesy wsi), Korona (powinnosci | danina i clo | zwrot zoldu), Skarbce (suma krolestw), Zold,
             Sakiewka ludzi, Paser, IronBank
  ludzie:    Ludzie, Ludzie (regiony), Ludnosc, Wyrzutki, Bitwy
  dodatkowe: ZakupyAI, Zuzycie AI, HouseLevies, Werbunek, Pobor, Ochotnicy, Komplet rekruta, Budowy oplacone, Budowy,
             PodazPopyt, WPLYW, UniqueLaw (dzien), LegendaryLaw (targi), Audyt predkosci, ArmsPricing (wojny)
  jednorazowe (naglowek i alarmy): StartStock, MineralOnce, MaterialLaw (mnoznik wydobycia), CaravanBulk, MoneyLedger,
             OutlawLaw, SoldierPay, KingdomTreasury, Kalendarz, ColdStart
  Kazdy inny prefiks, ktory powtarza sie w dobach, jest pokazany w trybie surowym (linia obcieta).

Skrypt niczego nie zapisuje (tylko stdout, UTF-8). Nieznany albo zmieniony format linii nie wywraca skryptu:
temat spada do trybu surowego i jest wymieniony w sekcji "formaty nierozpoznane".
"""

import sys
import os
import re
import glob
import argparse
from collections import Counter, OrderedDict, defaultdict

# =====================================================================================================================
# STALE I PROGI ALARMOW (zrodla: docs/paczki/103..107, docs/RAPORT-NOCNY-2026-10-06.md rozdz. 1-2, CHANGELOG wpis 102)
# =====================================================================================================================
KATALOG_LOGOW = r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\Armoury'
WZORZEC_LOGU = 'Armoury-*.log'
PLIK_CSV = 'ludzie-regiony.csv'

SZEROKOSC = 160                 # znakow na linie wyjscia
LIMIT_LINII = 150               # domyslny limit dlugosci skrotu
PROBKI = 6                      # ile dob w szeregu (pierwsza, rownomiernie rozlozone, ostatnia)
MAKS_ALARMOW = 24               # ile alarmow w skrocie (reszta: --pelny)
MAKS_GRUP_BLEDOW = 8

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
MNOZNIK_RUDY_PO_105 = 6.0
MNOZNIK_RUDY_PRZED_105 = 3.0
STOSUNEK_DOPISALY_MODEL_PO_105 = 1.3  # po 105 "wsie dopisaly" / "model" ok. 1 (przed: ok. 2)
MINERALY_ZNANE = ('iron', 'salt', 'clay', 'silver', 'marble', 'gold_ore')
MINERALY_LIMIT_WSI = {'iron': 26, 'salt': 12, 'clay': 16, 'silver': 30}
# pieniadz (102)
RESZTA_BILANSU_PROC_RUCHU = 25.0    # |reszta| bilansu swiata ponad tyle % ruchu (zrodla + ujscia)
RESZTA_KAS_PROC_RUCHU = 25.0        # |reszta| kas miast / zamkow / wsi ponad tyle % sumy pozycji zmierzonych
RESZTA_KAS_MIN_ABS = 20000          # ... i ponad tyle zlota
ZOLD_DUBEL_OD = 0.5                 # reszta bilansu w przedziale 0.5-1.5 x zold = znane podwojne liczenie zoldu (wpis 102)
ZOLD_DUBEL_DO = 1.5
TICKI_OSAD_MIN_UDZIAL = 0.5         # "N tickow osad" konsumpcji ponizej tej czesci liczby osad = uwaga (ok. 97 miast, do 130 zamkow)
# dowoz (100-101)
ZATKANE_ROZNICA_PP = 5.0            # zatkane magazyny: wsie zamkowe % minus wsie miejskie % ponad tyle punktow = uwaga
# paser (106)
PASER_MIN_DOB_BEZ_AWANSOW = 5       # "awanse: od pasera" = 0 przez tyle dob z ogniwem 106 = uwaga
PASER_ZASIEG_MIN_PROC = 5.0         # "w zasiegu miasta" / "band z ladunkiem" ponizej tylu % = uwaga (promien 20 czy 40)
# zold i skarbiec (107)
ZOLD_ROZJAZD_PROC = 2.0             # "z kies zeszlo" wobec sumy pozycji "przekazano" + "nie przekazano", %
ZOLD_KSIEGA_ROZJAZD_PROC = 10.0     # "Zold: naliczony" wobec "Przeplywy osad: zold wyplacony razem", %
SAKIEWKA_ROSNIE_DOB = 7             # "najwieksza" sakiewka rosnie tyle dob z rzedu = uwaga
# ludzie (102)
LUDNOSC_ROZJAZD_MLN = 0.2           # "Ludzie: ludnosc" wobec "Ludnosc:" (mln)

# przesuniecie numeru dnia w linii: 1 = linia drukuje (int)Now.ToDays (dzien biezacy), 0 = dzien wlasnie zakonczony.
# Doba skrotu = dzien zakonczony; tematow spoza tabeli skrypt uczy sie z sasiednich linii tej samej sekundy.
PRZESUNIECIE_DNIA = {
    'Bitwy': 1, 'Bitwa': 1, 'Budowy oplacone': 1, 'Budowy': 1, 'Budowa': 1, 'Korona': 1, 'Skarbce': 1, 'Finanse': 1,
    'Wyrzutki': 1, 'Paser': 1, 'Ludnosc': 1, 'Zold': 1, 'IronBank': 1, 'HouseLevies': 1, 'Audyt predkosci': 1,
    'Kronika unikatow': 1, 'Klimat': 1,
    'Ruda': 0, 'Drewno': 0, 'Dowoz': 0, 'Dowoz (skutki)': 0, 'Karawany': 0, 'Karawany (stan)': 0,
    'Mineraly (dubel BK)': 0, 'Pieniadz swiata': 0, 'Pieniadz swiata (bilans)': 0, 'Przeplywy osad': 0,
    'Przeplywy osad (kasy miast)': 0, 'Przeplywy osad (kasy zamkow)': 0, 'Przeplywy osad (kiesy wsi)': 0,
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
    """Jedna kolumna tematu: kotwica (doslowny tekst z kodu tuz przed wartoscia) + typ albo wlasne wyrazenie."""

    def __init__(self, nazwa, kot=None, typ='int', rx=None, sek=None, wym=False, lit=None):
        self.nazwa, self.kot, self.typ, self.sek, self.wym = nazwa, kot, typ, sek, wym
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
                if self.sek in czesc:
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
        return _liczba(g[0])


def K(nazwa, kot=None, typ='int', rx=None, sek=None, wym=False, lit=None):
    return Kol(nazwa, kot, typ, rx, sek, wym, lit)


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


# =====================================================================================================================
# TEMATY - kolumny wyprowadzone z ciagow Log.Info (galaz l107-zold) i z prawdziwych logow
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
    r['wylaczona'] = 1 if 'REGULA WYLACZONA' in t else 0
    return r


SUROWCE_KARAWAN = OrderedDict([('ruda', 'ruda'), ('drewno', 'drewno'), ('skory surowe', 'skory'), ('skora', 'skora'),
                               ('len', 'len'), ('plotno', 'plotno'), ('welna', 'welna')])
RX_STAN_KARAWAN = re.compile(r'(?:- |; )([a-z][a-z ]*?): ponizej zapasu docelowego (\?|-?\d+) -> (-?\d+) '
                             r'\(bez towaru (\?|-?\d+) -> (-?\d+)\), brakuje (-?\d+), w jukach karawan (-?\d+)(, KUPNO STOI)?')


def _extra_stan_karawan(t, d):
    r = OrderedDict()
    stoi = []
    n = 0
    for m in RX_STAN_KARAWAN.finditer(t):
        nazwa = SUROWCE_KARAWAN.get(m.group(1).strip(), m.group(1).strip().replace(' ', '_'))
        n += 1
        r[nazwa + '_ponizej'] = int(m.group(3))
        r[nazwa + '_bez'] = int(m.group(5))
        r[nazwa + '_brak'] = int(m.group(6))
        r[nazwa + '_juki'] = int(m.group(7))
        r[nazwa + '_stoi'] = 1 if m.group(8) else 0
        if m.group(8):
            stoi.append(nazwa)
    if n == 0:
        raise ValueError('brak surowcow')
    r['kupno_stoi'] = ','.join(stoi) if stoi else 'nie'
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


def _extra_bilans(t, d):
    r = {}
    zr, uj, re_ = d.get('zrodla'), d.get('ujscia'), d.get('reszta')
    if zr is not None and uj is not None and re_ is not None:
        ruch = abs(zr) + abs(uj)
        r['ruch'] = ruch
        r['reszta_proc'] = round(100.0 * abs(re_) / ruch, 1) if ruch else None
    return r


def _extra_przeplywy(t, d):
    r = {'licznik_slepy': 1 if 'licznik nie widzial wyplat' in t else 0}
    m = re.search(r'Potkniecia licznikow: okna taborow niedomkniete (\d+), konsumpcja bez pary (\d+), '
                  r'regulator pytany poza konsumpcja (\d+), wyjatki (\d+)', t)
    r['potkniecia'] = sum(int(x) for x in m.groups()) if m else 0
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


RX_REGION = re.compile(r'^(.*?) \[(miasto|zamek), ([^\]]*)\] ludzie (-?\d+)(?: \(([+-]\d+)\))?, zaloga (\d+), '
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
    r['kurek_otwarty'] = 1 if 'KUREK OTWARTY' in t else 0
    a, b = d.get('zabl_bandom'), d.get('zabl_kryjowkom')
    r['zabl_razem'] = (a or 0) + (b or 0) if (a is not None or b is not None) else None
    if d.get('w_zasiegu') is not None and d.get('z_ladunkiem'):
        r['w_zasiegu_proc'] = round(100.0 * d['w_zasiegu'] / d['z_ladunkiem'], 1)
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
        K('odp_brak_miejsca', ', brak miejsca w jukach '),
        K('odp_sprzedane_tu', ', sprzedane tu w tej wizycie '),
        K('odp_rozkaz', ', rozkaz gracza (tylko zywnosc) '),
        K('wstrzymana_rezerwa', '; sprzedaz wstrzymana rezerwa kasy miasta '),
        K('bk_wyzerowane', '; wyceny zakupu BK wyzerowane '),
    ], _extra_karawany, [
        ('wjazdy/sprzedaz | wyjazdy/zakup', '{wjazdy}/{ze_sprzedaza}|{wyjazdy}/{z_zakupem}'),
        ('sprzedane (ruda) | kupione (ruda)', '{sprz_szt}({sprz_ruda})|{kup_szt}({kup_ruda})'),
        ('zaplacily: miasta / karawany', '{miasta_zaplacily}/{karawany_zaplacily}'),
        ('odpuszczone: dosc w drodze/cena/juki', '{odp_w_drodze_dosc}/{odp_cena}/{odp_brak_miejsca}'),
    ], 'surowce', 'CaravanBulk.cs'),
    Temat('Karawany (stan)', 'Karawany (stan)', r'^dzien \d+', [
        K('miast', ' - miast ', wym=True),
        K('karawan', ', karawan '),
    ], _extra_stan_karawan, [
        ('ruda: miast ponizej zapasu/bez towaru', '{ruda_ponizej}/{ruda_bez}'),
        ('ruda: brakuje / w jukach karawan', '{ruda_brak}/{ruda_juki}'),
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
        K('potkniecia', '; potkniecia od wczytania kampanii ', wym=True),
    ], _extra_mineraly, [
        ('wsie z dublem: iron/salt/clay/silver', '{iron}/{salt}/{clay}/{silver}'),
        ('zdjetych wpisow | stan latki', '{razem} | {stan}'),
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
        K('z_niczego', ', GiveGoldAction z niczego '),
        K('ujscia', ' - zmierzone ujscia w nicosc ', wym=True),
        K('zold', rx=r'\(zold (?:naliczony )?(-?\d+)', lit=[' [P] (zold naliczony ']),
        K('przekazane', rx=r'zold naliczony -?\d+ minus (-?\d+) przekazane',
          lit=[' przekazane do sakiewek ludzi i kas osad, regulator kas skasowal ']),
        K('skasowal', 'regulator kas skasowal '),
        K('zniklo', ', z utargu wsi zniklo '),
        K('w_nicosc', ', GiveGoldAction w nicosc '),
        K('levy_oddal', rx=r'w nicosc -?\d+ minus (-?\d+) oddane', lit=[' oddane przez LevyGold notablom i miastom)']),
        K('reszta', ' + reszta ', 'sint', wym=True),
    ], _extra_bilans, [
        ('zmiana sumy | reszta [R]', '{zmiana:+}|{reszta:+}'),
        ('zmierzone zrodla / ujscia', '{zrodla}/{ujscia}'),
        ('reszta jako % ruchu (zrodla + ujscia)', '{reszta_proc}'),
        ('zrodla: zakupy mieszkancow/regulator', '{zakupy}/{dosypal}'),
        ('ujscia: zold / regulator skasowal', '{zold}/{skasowal}'),
        ('z niczego|utarg wsi zniklo|w nicosc', '{z_niczego}|{zniklo}|{w_nicosc}'),
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
        K('zold_rody', sek='zold wyplacony', rx=r'partie rodow (-?\d+) \(', lit=['partie rodow', ' partii, z niedoplata ']),
        K('zold_garnizony', sek='zold wyplacony', rx=r', garnizony (-?\d+) \(', lit=['garnizony']),
        K('zold_karawany', sek='zold wyplacony', rx=r', karawany (-?\d+) \(', lit=['karawany']),
        K('zold_inne', sek='zold wyplacony', rx=r', inne (-?\d+) \(', lit=['inne']),
        K('niedoplata_rody', sek='zold wyplacony', rx=r'partie rodow -?\d+ \(\d+ partii, z niedoplata (\d+)\)',
          lit=[' partii, z niedoplata ']),
        K('zold_razem', ', razem ', sek='zold wyplacony', wym=True),
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
        K('udzial', ' | skup lupu (udzial bandy ', 'float'),
        K('z_ladunkiem', '): band z ladunkiem '),
        K('w_zasiegu', ', z tego w zasiegu miasta '),
        K('sprzedalo', ', sprzedalo '),
        K('w_miastach', rx=r', sprzedalo \d+ w (\d+) miastach', lit=[' miastach - ']),
        K('szt', ' miastach - '),
        K('ruda', ' szt. (w tym ruda '),
        K('drewno', ', drewno ', sek='skup lupu'),
        K('za_zl', ' ladunkow) za '),
        K('po_cenach', ' zl; po cenach skupu miast '),
        K('dzialka', ' zl, dzialka pasera '),
        K('bez_zlota', '; miasto bez zlota ponad rezerwe: '),
        K('kupno_awansow', ' | kupno sprzetu u pasera: ', wym=True),
        K('kupno_zl', ' awansow za '),
        K('wejsc', ' | kryjowki: wejsc band ', wym=True),
        K('zabl_bandom', rx=r'(?:zloto z niczego zablokowane - bandom|gra dopisala z niczego bandom) (-?\d+) zl',
          lit=[', zloto z niczego zablokowane - bandom ', ', KUREK OTWARTY w ustawieniach - gra dopisala z niczego bandom ']),
        K('zabl_kryjowkom', ' zl, kryjowkom ', sek='kryjowki: wejsc band'),
        K('odlozyly', ' zl; bandy odlozyly z wlasnych kies '),
        K('kiesy_band', ' | stan: kiesy band ', wym=True),
        K('kasy_kryjowek', ' zl, kasy kryjowek '),
        K('juki_ladunek', ' zl; w jukach band: ladunek na sprzedaz '),
        K('juki_zywnosc', ' szt., zywnosc '),
        K('juki_zbroje', ', zbroje i konie '),
        K('juki_inny', ', inny sprzet '),
        K('potk_skup', ' | potkniecia: skup '),
        K('potk_kryjowki', ', kryjowki ', sek='potkniecia: skup'),
    ], _extra_paser, [
        ('bandy: z ladunkiem/w zasiegu/sprzedalo', '{z_ladunkiem}/{w_zasiegu}/{sprzedalo}'),
        ('skup lupu: szt (ruda/drewno)', '{szt} ({ruda}/{drewno})'),
        ('skup: zaplacono zl | dzialka pasera', '{za_zl}|{dzialka}'),
        ('kryjowki: wejsc band | zablokowane zl', '{wejsc}|{zabl_razem}'),
        ('odlozone w kryjowkach | awanse', '{odlozyly}|{kupno_awansow}'),
        ('kiesy band / kasy kryjowek', '{kiesy_band}/{kasy_kryjowek}'),
        ('juki band: ladunek/zywnosc', '{juki_ladunek}/{juki_zywnosc}'),
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
        K('glodne', ' | warownie glodne '),
        K('ujemny_bilans', ', z ujemnym bilansem zywnosci '),
        K('potkniecia', ' Potkniecia ksiegi: '),
    ], lambda t, d: {'nieskalibrowana': 1 if 'ludnosc nieskalibrowana' in t else 0}, [
        ('ludnosc mln (zmiana dobowa)', '{ludnosc_mln} ({ludnosc_zm:+})'),
        ('zolnierze: partie rodow/garnizony', '{rody}/{garnizony}'),
        ('zolnierze: bandy/milicje', '{bandy}/{milicje}'),
        ('dzis: zabici/zwerbowani/dezercja', '{zabici}/{zwerbowani}/{dezercja}'),
        ('pod bronia % | regionow ponad 20%', '{pod_bronia_proc} | {regionow_ponad}'),
    ], 'ludzie', 'PeopleLedger.cs', bezwar=True),
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
    Temat('WarLedger', 'WarLedger', None, [], None, [], 'ludzie', 'WarLedger.cs', dzienny=False),
    Temat('Klimat', 'Klimat', None, [], None, [], 'dodatkowe', 'WesterosClimate.cs', dzienny=False),
]

GRUPY = OrderedDict([('surowce', 'SUROWCE I DOWOZ'), ('pieniadz', 'PIENIADZ'), ('ludzie', 'LUDZIE'),
                     ('dodatkowe', 'DODATKOWE')])
# nazwy tematow w lewej kolumnie skrotu (pelne nazwy obowiazuja w --temat)
NAZWY_KROTKIE = {
    'Pieniadz swiata (bilans)': 'Pieniadz (bilans)',
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

# ogniwa lancucha: (numer, opis, wzorzec linii startowej, tematy dzienne, czy linia startowa jest zawsze)
OGNIWA = [
    ('100', 'dowoz na targ', r'^MarketRoad: wsie zamkowe woza plon na targ miasta', ['Dowoz'], True),
    ('101', 'woz x2', r'^MarketRoad: woz - udzwig taborow', [], True),
    ('102', 'ksiegi (log)', r'^MoneyLedger: ', ['Pieniadz swiata', 'Ludzie'], True),
    ('103', 'karawany', r'^CaravanBulk: ', ['Karawany', 'Karawany (stan)'], True),
    ('104', 'zapas startowy', r'^StartStock: ', [], False),
    ('105', 'mineral BK', r'^MineralOnce: ', ['Mineraly (dubel BK)'], True),
    ('106', 'paser', r'^OutlawLaw: .*zloto kryjowek', ['Paser'], True),
    ('107', 'zold i skarbiec', r'^SoldierPay: zold do obiegu', ['Zold'], True),
]
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
    if re.search(r'WYLACZON', tresc) or 'regula wylaczona w ustawieniach' in tresc:
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


def _ustal_lancuch(ses, ws):
    wszystkie = ses.start + [w for w in ws if w.blok is not None]
    najwyzsze = None
    for nr, opis, wz, dzienne, zawsze in OGNIWA:
        rx = re.compile(wz)
        trafienia = [w for w in wszystkie if rx.search(w.tresc)]
        dob = dict((n, len(ses.szer.get(n) or {})) for n in dzienne)
        linii = dict((n, len(ses.linie_tematu.get(n) or [])) for n in dzienne)
        jest = bool(trafienia)
        ses.ogniwa.append({'nr': nr, 'opis': opis, 'jest': jest, 'wpis': trafienia[0] if trafienia else None,
                           'ile': len(trafienia), 'dob': dob, 'linii': linii, 'zawsze': zawsze,
                           'status': status_linii(trafienia[0].tresc) if trafienia else None})
        if jest or any(linii.values()):
            najwyzsze = nr
    ses.ogniwo_w_grze = najwyzsze


def ma_ogniwo(ses, nr):
    for o in ses.ogniwa:
        if o['nr'] == nr:
            return o['jest'] or any(o['linii'].values())
    return False


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
            if w.blok is not None and w.pref in ('SoldierPay', 'MoneyLedger', 'CaravanBulk', 'MineralOnce', 'OutlawLaw'):
                if status_linii(w.tresc) in ('BRAK', 'NIE wpieta'):
                    al('ALARM', 'linia %s w kampanii: %s' % (status_linii(w.tresc), wycinek(w.tresc, 105)), w.nr)
        st = ses.fakty.get('stale_skarbca')
        if st and st[0] != st[1]:
            al('ALARM', 'KingdomTreasury: podmienionych stalych %d, oczekiwane %d - zloto z niczego do skarbcow moze plynac'
               % (st[0], st[1]), st[2])

    @regula
    def r_lancuch():
        pewne = [o for o in ses.ogniwa if o['zawsze'] and o['nr'] >= '102']
        obecne = [o['nr'] for o in pewne if o['jest']]
        if obecne:
            for o in pewne:
                if o['nr'] < max(obecne) and not o['jest']:
                    al('ALARM', 'lancuch niespojny: jest linia startowa ogniwa %s, a brak ogniwa %s (%s)'
                       % (max(obecne), o['nr'], o['opis']))
        for o in ses.ogniwa:
            # linia startowa z BRAK / WYLACZONE ma juz wlasny alarm - brak linii dziennych jest wtedy skutkiem, nie nowina
            if o['jest'] and o['zawsze'] and ses.bloki and o['status'] not in ('BRAK', 'NIE wpieta', 'WYLACZONE'):
                for n, ile in o['linii'].items():
                    if ile == 0 and len(ses.pelne()) >= 2:
                        al('ALARM', 'ogniwo %s wpiete, ale w %d dobach nie ma ani jednej linii "%s:"'
                           % (o['nr'], len(ses.pelne()), n), o['wpis'].nr if o['wpis'] else None)

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
        mn, nr = ses.fakty.get('mnoznik_rudy'), ses.fakty.get('mnoznik_nr')
        jest105 = ma_ogniwo(ses, '105')
        if mn is not None:
            if jest105 and abs(mn - MNOZNIK_RUDY_PO_105) > 0.01:
                al('ALARM', 'wydobycie rudy x%.1f przy wgranym ogniwie 105 (ma byc x%.1f - MCM zapisal stara wartosc '
                   'MineOutputMultiplier?)' % (mn, MNOZNIK_RUDY_PO_105), nr)
            if not jest105 and abs(mn - MNOZNIK_RUDY_PRZED_105) > 0.01:
                al('ALARM', 'wydobycie rudy x%.1f bez ogniwa 105 (dubel BK czynny: ruda %.1fx wieksza niz przy x%.1f)'
                   % (mn, mn / MNOZNIK_RUDY_PRZED_105, MNOZNIK_RUDY_PRZED_105), nr)
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
        ruda = [(bi, d) for bi, d in ses.ser('Ruda') if bi > 0 and d.get('dopisaly_do_modelu') is not None]
        if jest105 and len(ruda) >= 3:
            st = sorted(d['dopisaly_do_modelu'] for _, d in ruda)
            med = st[len(st) // 2]
            if med > STOSUNEK_DOPISALY_MODEL_PO_105:
                al('ALARM', 'Ruda: "wsie dopisaly" / "model" = %.2f (mediana) przy ogniwie 105 - mineral nadal podwojny?'
                   % med, ruda[-1][1].get('_nr'))

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
        # uwaga recenzentow wpisu 102: bilans najpewniej liczy zold dwa razy - wtedy reszta jest dodatnia i bliska zoldowi
        stos = [(d['reszta'] / float(d['zold']), bi, d) for bi, d in bil if d.get('zold') and d.get('reszta') is not None]
        blisko = [x for x in stos if ZOLD_DUBEL_OD <= x[0] <= ZOLD_DUBEL_DO]
        znany_dubel = len(stos) >= 2 and len(blisko) >= 0.6 * len(stos)
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
        if znany_dubel:
            st = sorted(x[0] for x in blisko)
            d = blisko[-1][2]
            al('uwaga', 'Pieniadz swiata (bilans): reszta bliska pozycji zoldu w %d z %d dob (mediana reszta/zold %.2f; np. '
               'reszta %s, zold %s) - znane podwojne liczenie zoldu w bilansie wpisu 102; ponad %.0f%% ruchu: %d dob'
               % (len(blisko), len(stos), st[len(st) // 2], skr(d.get('reszta'), True), skr(d.get('zold')),
                  RESZTA_BILANSU_PROC_RUCHU, ponad_w_dublu), d.get('_nr'))
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
        potk = [(bi, d) for bi, d in pa if (d.get('potk_skup') or 0) + (d.get('potk_kryjowki') or 0) > 0]
        if potk:
            al('ALARM', 'Paser: potkniecia (skup %s, kryjowki %s)' % (potk[0][1].get('potk_skup'),
                                                                     potk[0][1].get('potk_kryjowki')), potk[0][1].get('_nr'))
        km = dict(ses.ser('Przeplywy osad (kasy miast)'))
        rozne = []
        for bi, d in pa:
            k = km.get(bi)
            if k is None or d.get('za_zl') is None:
                continue
            skup = k.get('paser_skup') or 0
            if abs(abs(skup) - d['za_zl']) > 0:
                rozne.append((bi, d, skup))
        if rozne:
            bi, d, skup = rozne[0]
            al('ALARM', 'Paser: skup za %d zl, a ksiega kas miast liczy "paser band (skup lupu)" %+d (%d dob z rozjazdem)'
               % (d['za_zl'], skup, len(rozne)), d.get('_nr'))
        wy = ses.ser('Wyrzutki')
        if len(wy) >= PASER_MIN_DOB_BEZ_AWANSOW and all((d.get('aw_paser') or 0) == 0 for _, d in wy):
            al('uwaga', 'Wyrzutki: "awanse: od pasera" = 0 przez %d dob mimo ogniwa 106 (bandy nie maja za co kupowac?)'
               % len(wy), wy[-1][1].get('_nr'))
        wej = [(bi, d) for bi, d in pa if (d.get('wejsc') or 0) > 0 and not d.get('kurek_otwarty')]
        if len(wej) >= 3 and all((d.get('zabl_razem') or 0) == 0 for _, d in wej):
            al('uwaga', 'Paser: wejscia band do kryjowek w %d dobach, a "zloto z niczego zablokowane" stale 0' % len(wej),
               wej[-1][1].get('_nr'))
        lad = sum(d.get('z_ladunkiem') or 0 for _, d in pa)
        zas = sum(d.get('w_zasiegu') or 0 for _, d in pa)
        if lad >= 50 and 100.0 * zas / lad < PASER_ZASIEG_MIN_PROC:
            al('uwaga', 'Paser: w zasiegu miasta %.1f%% band z ladunkiem (%d z %d) - promien 20 prawie nie dziala '
               '(decyzja: OutlawFenceRadius 40?)' % (100.0 * zas / lad, zas, lad), pa[-1][1].get('_nr'))

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
        czesci.append('pula wyrzutkow na starcie %d' % f['pula_poczatkowa'])
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
    # lancuch
    w_grze = ses.ogniwo_w_grze
    nowa = bool(f.get('nowa_kampania'))
    # ogniwo 104 nie ma linii przy starcie gry - jego linie "StartStock:" pojawiaja sie tylko przy starcie kampanii
    brakujace = [o for o in ses.ogniwa if not o['jest'] and (o['zawsze'] or nowa) and o['nr'] >= '102']
    L.append('OGNIWO W GRZE (wg linii logu): %s%s' % (
        ('%s %s' % (w_grze, [o['opis'] for o in ses.ogniwa if o['nr'] == w_grze][0])) if w_grze else 'sprzed wpisu 100',
        (' | NIE MA linii startowych ogniw: ' + ', '.join(o['nr'] for o in brakujace)) if brakujace else
        ' | komplet linii startowych lancucha 102-107'))
    for o in ses.ogniwa:
        if o['jest']:
            op = 'JEST  linia %d%s %s' % (o['wpis'].nr, (' [%s]' % o['status']) if o['status'] != 'inne' else '',
                                          wycinek(o['wpis'].tresc, 62))
        elif not o['zawsze']:
            wyzsze = any(x['jest'] for x in ses.ogniwa if x['nr'] > o['nr'] and x['zawsze'])
            if nowa:
                op = 'BRAK  linii "StartStock:" przy starcie NOWEJ kampanii -> ogniwo nie wgrane' + (
                    ' (a wyzsze ogniwo jest - patrz alarmy)' if wyzsze else '')
            else:
                op = 'brak linii "StartStock:" - ' + ('wyzsze ogniwo wgrane, wiec 104 jest (linie tylko przy starcie nowej '
                                                      'kampanii)' if wyzsze else 'nie do rozstrzygniecia (wczytany zapis; '
                                                                                  'linie tylko przy starcie nowej kampanii)')
        else:
            op = 'BRAK  linii startowej -> ogniwo nie wgrane'
        dz = ', '.join('"%s:" %d dob' % (n, c) for n, c in o['dob'].items())
        L.append('  %s %-15s %s%s' % (o['nr'], o['opis'], op, (' | ' + dz) if dz else ''))
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
