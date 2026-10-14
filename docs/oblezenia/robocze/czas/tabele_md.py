# -*- coding: utf-8 -*-
# Tabele markdown do ROZPOZNANIE-CZAS.md z fin30.json / fin21.json (wynik oblezenia_czas.py). Tylko odczyt.
import json, sys, csv
from collections import Counter, defaultdict
base = r'C:/Users/GAME/Bannerlor2-Realm-of-Thrones/docs/oblezenia/robocze/czas/'
D0 = 108836

def q(v, p):
    v = sorted(v)
    if not v: return None
    k = (len(v) - 1) * p; lo = int(k); hi = min(lo + 1, len(v) - 1)
    return round(v[lo] + (v[hi] - v[lo]) * (k - lo), 1)

def rel(d): return d - D0 + 1

out = []
for tag in ('fin30', 'fin21'):
    J = json.load(open(base + tag + '.json', encoding='utf-8'))
    E = J['epizody']
    # csv wszystkich epizodow
    with open(base + tag + '-epizody.csv', 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f, delimiter=';')
        w.writerow(['region', 'nazwa', 'rodzaj', 'start_dzien', 'start_doba_biegu', 'koniec_dzien', 'dob', 'wynik', 'pan_przed', 'pan_po', 'zaloga0', 'milicja0',
                    'zywnosc0', 'bilans0', 'dni_jedzenia0', 'dob_glodu', 'szturmy', 'szturm_doby_od_startu', 'szturm_wynik', 'szturm_A_O_moc', 'szturm_A_ludzi',
                    'szturm_O_ludzi', 'szturm_A_straty', 'szturm_O_straty', 'wypady', 'pod_murami', 'O0', 'okup', 'campfever'])
        for e in sorted(E, key=lambda e: (e['start'], e['region'])):
            w.writerow([e['region'], e['nazwa'], e['rodzaj'], e['start'], rel(e['start']), e['koniec'], e['dob'], e['wynik'], e['pan_przed'], e['pan_po'],
                        e['zaloga0'], e['milicja0'], e['zywnosc0'], e['bilans0'], e['dni_jedzenia0'], e['dob_glodu'], e['szturmy'], e['szturm_dni'], e['szturm_wyn'],
                        e['szturm_ratio'], e['szturm_A'], e['szturm_O'], e['szturm_Astr'], e['szturm_Ostr'], e['wypady'], e['pod_murami'], e['o0'], e['okup'], e['campfever']])

    # tabela wynikow
    L = []
    L.append('#### %s - wynik oblezenia x rodzaj (epizody = ciagi dob z `oblezona=1` w spichlerze-regiony.csv) [P]' % tag)
    L.append('')
    L.append('| wynik | miasta: n / mediana / p90 / max dob | zamki: n / mediana / p90 / max dob | razem n | udzial |')
    L.append('|---|---|---|---|---|')
    order = ['zdobyte szturmem', 'zdobyte po wypadzie', 'okup (O1)', 'wypad - pobity oblegajacy', 'pod murami - pobity oblegajacy',
             'szturm bez zdobycia, potem odstapione', 'odstapione bez bitwy', 'zmiana pana bez szturmu', 'trwa na koncu biegu']
    for wyn in order:
        row = [wyn]
        tot = 0
        for kind in ('miasto', 'zamek'):
            v = [e['dob'] for e in E if e['wynik'] == wyn and e['rodzaj'] == kind]
            tot += len(v)
            row.append('%d / %s / %s / %s' % (len(v), q(v, .5), q(v, .9), max(v)) if v else '0')
        if tot == 0: continue
        row.append(str(tot)); row.append('%.0f%%' % (100.0 * tot / len(E)))
        L.append('| ' + ' | '.join(row) + ' |')
    for kind in ('miasto', 'zamek'):
        pass
    vm = [e['dob'] for e in E if e['rodzaj'] == 'miasto']; vz = [e['dob'] for e in E if e['rodzaj'] == 'zamek']
    L.append('| **wszystkie** | %d / %s / %s / %s | %d / %s / %s / %s | %d | 100%% |' % (len(vm), q(vm, .5), q(vm, .9), max(vm), len(vz), q(vz, .5), q(vz, .9), max(vz), len(E)))
    L.append('')
    hist = Counter(e['dob'] for e in E)
    L.append('Histogram dlugosci (doby: liczba oblezen) [P]: ' + ', '.join('%d: %d' % (k, hist[k]) for k in sorted(hist)) + '.')
    L.append('')
    sd = sum(e['dob'] for e in E)
    L.append('Doby oblezen razem [P]: %d (miasta %d, zamki %d) w %d dobach biegu = srednio %.2f twierdzy naraz.' % (sd, sum(vm), sum(vz), J['D1'] - J['D0'] + 1, sd / (J['D1'] - J['D0'] + 1)))
    L.append('')
    out.append('\n'.join(L))

    if tag == 'fin30':
        # tabela zdobyc
        C = [e for e in E if e['wynik'].startswith('zdobyte')]
        L = []
        L.append('#### fin30 - wszystkie 57 zdobyc (kolejnosc wedlug startu oblezenia) [P]')
        L.append('')
        L.append('Kolumny: start = doba biegu (dzien 108836 = doba 1), dob = doby z `oblezona=1` (migawka o polnocy, +-1), O0 = pierwsza decyzja gry o szturmie: wiek oblezenia w dobach '
                 '(z linii "Okup oblezenia (O0)"), sila gry oblegajacy/obroncy i wykladnik murow w; szturm = doba od startu, w ktorej SKONCZYLA sie bitwa szturmu (B175 "Siege"), '
                 'A/O = moc oblegajacych / moc zalogi z B175, ludzie A/O, straty A/O; jedzenie = zapas zywnosci / dzienny ubytek w pierwszej dobie oblezenia.')
        L.append('')
        L.append('| # | osada | rodzaj | pan przed -> po | start | dob | O0: wiek / sila / w | szturmy (doba: wynik) | A/O moc | ludzie A / O | straty A / O | jedzenie (dob) |')
        L.append('|---|---|---|---|---|---|---|---|---|---|---|---|')
        for i, e in enumerate(sorted(C, key=lambda e: e['start']), 1):
            o0 = e['o0'][0] if e['o0'] else None
            o0s = ('%.1f / %.1f / %.2f' % (o0[1], o0[10] / max(1, o0[11]), o0[5])) if o0 else '-'
            sz = ', '.join('%d: %s' % (d, w.replace('/tak', ' zdob.').replace('/nie', ' bez')) for d, w in zip(e['szturm_dni'], e['szturm_wyn'])) or ('wypad' if e['wynik'] == 'zdobyte po wypadzie' else '-')
            rr = ', '.join('%.1f' % r for r in e['szturm_ratio'] if r) or '-'
            lu = ', '.join('%d / %d' % (a, o) for a, o in zip(e['szturm_A'], e['szturm_O'])) or '-'
            st = ', '.join('%d / %d' % (a, o) for a, o in zip(e['szturm_Astr'], e['szturm_Ostr'])) or '-'
            L.append('| %d | %s | %s | %s -> %s | %d | %d | %s | %s | %s | %s | %s | %s |' % (
                i, e['nazwa'], e['rodzaj'], e['pan_przed'], e['pan_po'], rel(e['start']), e['dob'], o0s, sz, rr, lu, st,
                ('%.0f' % e['dni_jedzenia0']) if e['dni_jedzenia0'] else '-'))
        L.append('')
        out.append('\n'.join(L))

open(base + 'tabele.md', 'w', encoding='utf-8').write('\n\n'.join(out))
print('ok')
