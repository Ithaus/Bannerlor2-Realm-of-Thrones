# -*- coding: utf-8 -*-
"""Kronika Westeros - zgodnosc DANYCH z SILNIKIEM K0 (m105-kronika krok B2).

Sprawdza, ze KAZDA akcja uzyta w danych (arm_kronika_k01..k10.tsv: skutki okien i opcji, po_rozbiciu band, nagroda zadan) ma w silniku
(Armoury/src/Chronicle*.cs) procedure w slowniku Impl albo jest SWIADOMIE POMINIETA (procedura bez zadnego Res.Done - kazdy return to Res.Skip z powodem),
ze kazdy uchwyt kod:<nazwa> z danych ma galaz w RunCode (Chronicle.Codes.cs) i ze kazdy wyzwalacz okna powracajacego (kolumna powtorz) jest w
Recurring.Triggers i nie jest w Recurring.Unsupported.

Uzycie (z katalogu repo):  python -I tools/kronika/kronika_silnik.py [--repo <katalog repo>]
Wynik: tabela akcja | wierszy | uzyc | stan, potem uchwyty i wyzwalacze, na koncu linia "kronika_silnik: ... | WYNIK OK|BLAD" (kod wyjscia 0 = OK).
Tylko biblioteka standardowa (zasada repo: python -I).
"""
import argparse
import collections
import glob
import os
import re
import sys

COLS = {
    '#okno': ['id', 'war', 'od', 'do', 'po', 'przed', 'warunki', 'gracz', 'forma', 'inni', 'wersja', 'temat', 'lektor',
              'tytul', 'gora', 'dol', 'zmienne', 'wybor', 'skutki', 'powtorz'],
    '#wybor': ['okno', 'opcja', 'etykieta', 'podpowiedz', 'dostepna', 'skutki', 'bierna'],
    '#tekst': ['id', 'tekst'],
    '#banda': ['id', 'klan', 'osada', 'min', 'max', 'herszt', 'zachowanie', 'dni', 'list', 'po_rozbiciu'],
    '#zadanie': ['id', 'rodzaj', 'cel', 'co', 'ilosc', 'dni', 'nagroda', 'tytul', 'opis'],
    '#para': ['okno', 'wiersz', 'kierunek'],
    '#usuniete': ['id', 'powod'],
}
ACT_COLS = {'#okno': ['skutki'], '#wybor': ['skutki'], '#banda': ['po_rozbiciu'], '#zadanie': ['nagroda']}


def read_data(md):
    """(akcja -> zbior (plik, wiersz)), (akcja -> liczba uzyc), (kod -> liczba), (wyzwalacz -> lista okien)."""
    rows = collections.defaultdict(set)
    uses = collections.Counter()
    codes = collections.Counter()
    trig = collections.defaultdict(list)
    files = sorted(glob.glob(os.path.join(md, 'arm_kronika_k*.tsv')))
    for path in files:
        base = os.path.basename(path)
        if base == 'arm_kronika_k00.tsv':
            continue
        sec = None
        with open(path, encoding='utf-8') as f:
            for i, line in enumerate(f, 1):
                line = line.rstrip('\r\n')
                if not line.strip() or line.startswith('# ') or line == '#':
                    continue
                c = line.split('\t')
                if c[0].startswith('#'):
                    sec = c[0]
                    continue
                if sec not in COLS:
                    continue
                d = dict(zip(COLS[sec], [x.strip() for x in c] + [''] * (len(COLS[sec]) - len(c))))
                if sec == '#okno' and d.get('powtorz') and not d.get('war'):
                    t = d['powtorz'].split(';')[0].strip().split(':')[0]
                    trig[t].append(d['id'])
                for col in ACT_COLS.get(sec, []):
                    for raw in d.get(col, '').split(';'):
                        raw = raw.strip()
                        if not raw:
                            continue
                        name = raw.split(':', 1)[0]
                        rows[name].add((base, i))
                        uses[name] += 1
                        if name == 'kod':
                            codes[raw.split(':')[1] if ':' in raw else ''] += 1
    return files, rows, uses, codes, trig


def method_body(src, name):
    """Tresc metody 'private static Res <name>(Ctx c, ...' (nawiasy klamrowe zbalansowane) albo None."""
    m = re.search(r'static\s+Res\s+' + re.escape(name) + r'\s*\(\s*Ctx\s+c\b[^)]*\)\s*\{', src)
    if not m:
        return None
    i = m.end()
    depth = 1
    while i < len(src) and depth > 0:
        ch = src[i]
        if ch == '{':
            depth += 1
        elif ch == '}':
            depth -= 1
        i += 1
    return src[m.end():i - 1]


def read_engine(srcdir):
    src = ''
    for p in sorted(glob.glob(os.path.join(srcdir, 'Chronicle*.cs'))):
        with open(p, encoding='utf-8') as f:
            src += f.read() + '\n'
    impl = {}
    m = re.search(r'Dictionary<string, Handler>\s+Impl\s*=\s*new[^{]*\{(.*?)\n\s*\};', src, re.S)
    if m:
        for a, h in re.findall(r'\{\s*"(\w+)"\s*,\s*(\w+)\s*\}', m.group(1)):
            impl[a] = h
    known = []
    m = re.search(r'KnownActions\s*=\s*\{(.*?)\};', src, re.S)
    if m:
        known = re.findall(r'"(\w+)"', m.group(1))
    codes = set()
    m = re.search(r'internal static Res RunCode\(.*?switch\s*\(name\)\s*\{(.*?)\n\s*\}\s*\n', src, re.S)
    if m:
        codes = set(re.findall(r'case\s+"(\w+)"\s*:', m.group(1)))
    triggers, unsupported = [], []
    m = re.search(r'Triggers\s*=\s*\{(.*?)\};', src, re.S)
    if m:
        triggers = re.findall(r'"(\w+)"', m.group(1))
    m = re.search(r'HashSet<string>\s+Unsupported\s*=\s*new HashSet<string>\([^)]*\)\s*(\{(.*?)\})?\s*;', src, re.S)
    if m and m.group(2):
        unsupported = re.findall(r'"(\w+)"', m.group(2))
    return src, impl, known, codes, triggers, unsupported


def state_of(src, handler, seen=None):
    """'impl' - procedura z wynikiem (Res.Done albo wynik innej procedury), 'skip:<powod>' - zawsze POMINIETA z powodem, 'brak' - brak metody."""
    seen = seen or set()
    body = method_body(src, handler)
    if body is None:
        return 'brak'
    if 'Res.Done' in body:
        return 'impl'
    rets = re.findall(r'return\s+([^;]+);', body)
    if rets and all(r.strip() == 'Res.Skip' for r in rets):
        reasons = re.findall(r'detail\s*=\s*"([^"]+)"', body)
        return 'skip:' + (reasons[-1] if reasons else '(bez powodu w kodzie!)')
    for r in rets:
        mm = re.match(r'(\w+)\s*\(', r.strip())
        if mm and mm.group(1) not in seen and mm.group(1) not in ('FromCanon',):
            seen.add(mm.group(1))
            st = state_of(src, mm.group(1), seen)
            if st != 'brak':
                return st
    return 'impl'


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--repo', default='.')
    o = ap.parse_args()
    md = os.path.join(o.repo, 'Armoury', 'ModuleData')
    srcdir = os.path.join(o.repo, 'Armoury', 'src')
    files, rows, uses, codes, trig = read_data(md)
    src, impl, known, ecodes, triggers, unsupported = read_engine(srcdir)
    errors = 0
    print('akcja\twierszy\tuzyc\tstan')
    nimpl = nskip = 0
    for name in sorted(rows, key=lambda n: (-len(rows[n]), n)):
        if name not in known:
            st, errors = 'BLAD - akcja spoza rejestru KnownActions silnika', errors + 1
        elif name not in impl:
            st, errors = 'BLAD - brak procedury w Impl (silnik pisalby "akcja jeszcze niewspierana")', errors + 1
        else:
            s = state_of(src, impl[name])
            if s == 'brak':
                st, errors = 'BLAD - procedura %s nie znaleziona w Chronicle*.cs' % impl[name], errors + 1
            elif s.startswith('skip:'):
                st = 'SWIADOMA POMINIETA (%s): %s' % (impl[name], s[5:])
                nskip += 1
                if '(bez powodu' in s:
                    errors += 1
            else:
                st = 'OK (%s)' % impl[name]
                nimpl += 1
        print('%s\t%d\t%d\t%s' % (name, len(rows[name]), uses[name], st))
    print()
    print('uchwyt kod:\tuzyc\tstan')
    for c in sorted(codes):
        ok = c in ecodes
        if not ok:
            errors += 1
        print('%s\t%d\t%s' % (c, codes[c], 'OK (RunCode)' if ok else 'BLAD - brak galezi w RunCode'))
    print()
    print('wyzwalacz powtorz\tokna\tstan')
    for t in sorted(trig):
        if t not in triggers:
            st, errors = 'BLAD - spoza Recurring.Triggers', errors + 1
        elif t in unsupported:
            st, errors = 'BLAD - Recurring.Unsupported (okno czeka)', errors + 1
        else:
            st = 'OK'
        print('%s\t%s\t%s' % (t, ','.join(trig[t]), st))
    unused = sorted(set(impl) - set(rows))
    print()
    print('kronika_silnik: plikow danych %d, akcji uzytych %d (wierszy %d, uzyc %d) - z procedura %d, swiadomie POMINIETE %d | uchwytow %d | wyzwalaczy %d '
          '| procedur bez uzycia w danych %d (%s) | bledy %d | WYNIK %s'
          % (len([f for f in files if not f.endswith('k00.tsv')]), len(rows), sum(len(v) for v in rows.values()), sum(uses.values()), nimpl, nskip,
             len(codes), len(trig), len(unused), ','.join(unused) or '-', errors, 'OK' if errors == 0 else 'BLAD'))
    return 0 if errors == 0 else 1


if __name__ == '__main__':
    sys.exit(main())
