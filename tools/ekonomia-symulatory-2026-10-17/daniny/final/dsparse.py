# DS (daniny sredniowieczne) - parser logow pod model zasad ZAIMPLEMENTOWANYCH (galaz w-toku/noc-ds, 2d2ab146). Tylko odczyt logow.
# Uzycie: python -I dsparse.py <etykieta> <out.json> <log1> [log2 ...]   (logi w kolejnosci - pozniejszy plik nadpisuje te sama dobe)
# Wynik: {"w": {doba: {klucz: liczba}}, "k": {doba: {krolestwo: {klucz: liczba}}}}
import json
import re
import sys
from collections import defaultdict

N = r'(-?\d+)'
F = r'(-?\d+(?:\.\d+)?)'
DAYRX = re.compile(r'(?:dzien|doba) (\d{6})')

# (prefiks linii, [(klucz, regex)]) - swiat na dobe
SPEC = [
    ('Przeplywy osad (kiesy wsi): dzien', [
        ('v_stan', r'\| stan ' + N), ('v_zmiana', r'zmiana \+?' + N), ('v_utarg', r'z utargu taborow \+?' + N),
        ('v_przelewy', r'przelewy gry \(GiveGoldAction\) \+?' + N), ('v_renty', r'\(renty ' + N),
        ('v_korona', r'korona \(danina, clo, mennica\) ' + N), ('v_166', r'sakiewki zwolnionych do kies wsi \+?' + N),
        ('v_markiet', r'kiesy wsi \+?' + N + r', kiesa ludu'), ('v_kiesaludu', r'zwrot przy wylaczeniu \(kasy osad <-> kiesy ludu i wsi\) ' + N),
        ('v_ksh', r'fracht -> kiesy wsi i portow, zwroty \+?' + N), ('v_reszta', r'niezmierzone \(m\.in\. podatek gry od zakupow we wsi\) ' + N),
        ('v_osad', r'rozklad \[P\]: osad ' + N), ('v_med', r'mediana ' + N), ('v_max', r'max ' + N), ('v_pod1000', r'ponizej 1000 zlota ' + N),
        ('v_ds', r'daniny wsi przy wozie \(DS\) ' + N)]),
    ('Utarg wsi (112): dzien', [
        ('u_oddane', r'oddane ' + N + r' ='), ('u_pan', r'pan \(licznik podatku\) ' + N), ('u_majatki', r'wlasciciele majatkow BK ' + N),
        ('u_kiesaBK', r'kiesa wsi od BK ' + N), ('u_dopisane', r'z tego dopisane kiesom wsi ' + N)]),
    ('Podatek od nadwyzki wsi (obieg2): dzien', [
        ('hv_pobrano', r'pobrano ' + N + ':'), ('hv_glowy', r'do glow rodow ' + N), ('hv_miasta', r'do kas miast ' + N),
        ('hv_zamki', r'do kas zamkow ' + N), ('hv_korony', r'do skarbcow koron ' + N), ('hv_poziom', r'poziom normalny ' + N)]),
    ('Podatek od nadwyzki ludu (kiesa2): dzien', [
        ('hl_pobrano', r'pobrano ' + N + ' z'), ('hl_glowy', r'do glow rodow ' + N), ('hl_miasta', r'do kas miast ' + N),
        ('hl_zamki', r'do kas zamkow ' + N), ('hl_korony', r'do skarbcow koron z miast i zamkow ' + N)]),
    ('Dziesiecina (E5/C3): dzien', [('ti_wsie', r'= wsie ' + N), ('ti_nalezne', r'nalezne ' + N), ('ti_jalm_wsi', r'kiesy wsi ' + N + r'\)'),
                                    ('ti_razem', r'razem ' + N)]),
    ('Markietani (E5/A3): dzien', [('mk_razem', r'z sakiewek ' + N), ('mk_miasta', r'-> kasy miast ' + N), ('mk_wsie', r'kiesy wsi ' + N + r' w \d+ wsiach'),
                                   ('mk_do_kies', r'do kies wsi ' + N + ', na liczniki'), ('mk_liczniki', r'na liczniki podatku wsi \(pan wsi\) ' + N)]),
    ('Obieg: BK i BetterEconomy: dzien', [('bk_rynek_wsi', r'rynek wsi [+-]?\d+ \(wsie ' + N)]),
    ('Kiesa ludu: dzien', [('kl_wsie_zakupy', r'wsie \d+: zakupy ' + N), ('kl_wsie_chciane', r'wsie \d+: zakupy -?\d+ z ' + N),
                           ('kl_wsie_extra', r'chciane z nadwyzka ' + N), ('kl_lud_zaplacil', r'zaplacone przez lud miast ' + N),
                           ('kl_P1', r'danina z oszczednosci do korony \(P1\) ' + N),
                           ('kl_ovW', r'kiesa ponad zapas dni przed / po: W ' + N), ('kl_ovR', r', R ' + N + r' / -?\d+, P'),
                           ('kl_ovP', r', P ' + N + r' / -?\d+, podzamcza'), ('kl_ovC', r'podzamcza ' + N + r' / -?\d+ \| kontrola')]),
    ('Zbytki ludu (P3-P5): dzien', [('p5v_chciane', r'wsie \(P5\): z nadwyzka \d+, chciane ' + N), ('p5v_kupione', r'wsie \(P5\): z nadwyzka \d+, chciane -?\d+ zl, kupione ' + N)]),
    ('Pozyczki od ludu (obieg2): dzien', [('lo_wsi', r'podzamczy -?\d+, wsi ' + N + r'\)')]),
    ('Stawka wojenna pana (W7): dzien', [('w7_razem', r'pobrano ' + N + r' \(z ludu'), ('w7_lud', r'\(z ludu ' + N), ('w7_wsie', r'ze wsi ' + N + r'\)')]),
    ('Prawa panskie (E5/B1): dzien', [('pp_lud', r'od zarobku ludu miast ' + N), ('pp_W', r'\(W ' + N), ('pp_R', r'/ R ' + N), ('pp_P', r'/ P ' + N + r'\)'),
                                      ('pp_podz', r'podzamcza ' + N), ('pp_farma', r'farma gmin ' + N), ('pp_wsie', r'oplaty stale wsi ' + N)]),
    ('Korona: wplywy dnia (165): dzien', [
        ('k_wpl', r'wplywy dnia ' + N + r' \(nasze'), ('k_nasze', r'1/9 prawa trzecich \(178\) ' + N), ('k_184', r'w tym podatek wojenny ludu \(184\) ' + N),
        ('k_P1', r'danina z oszczednosci ludu \(P1\) ' + N), ('k_inne', r'splata dlugu wobec korony ' + N + r'\)'), ('k_zapas', r'zapasu ponad 500000 ' + N),
        ('k_rata186', r'rata kredytu Banku \(186\) ' + N), ('k_dary', r'dary \(182\) ' + N), ('k_rep', r'raty reparacji ' + N),
        ('k_kontr', r'kontrakty najemnikow \(185\) ' + N), ('k_wezw', r'wezwania sojusznikow do wojny \(168\) ' + N),
        ('k_zwrot', r'zwrot zoldu ' + N + r' \(nalezny'), ('k_zwrot_nal', r'zwrot zoldu -?\d+ \(nalezny ' + N), ('k_zwrot_kred', r'w tym z kredytu Banku \(186\) ' + N),
        ('k_renty', r'renty \(180\) ' + N), ('k_W5', r'\(W5, przed rentami\) ' + N), ('k_M1', r'\(M1\) ' + N),
        ('k_M3', r'\(M3, z reszty wplywow przed rentami\) ' + N), ('k_dwor', r'\(Crown Court Goods tak\) ' + N),
        ('k_W5b', r'\(W5b, poza wplywami dnia - z zapasu\) ' + N), ('k_zostalo', r'zostalo z wplywow dnia w skarbcach ' + N)]),
    ('Korona: dzien', [('kd_danina', r'danina wojenna z kas osad ' + N), ('kd_clo', r'clo od handlu miast ' + N), ('kd_wasale', r'powinnosci wasali ' + N),
                       ('kd_owncut', r'minus wydatki ludzi we wlasnych miastach rodu ' + N), ('kd_zwrot_nal', r'= nalezne ' + N)]),
    ('Kasy miast (169c): dzien', [('t_zawor_pan', r'zawor: zeszlo -?\d+ = panom ' + N), ('t_zawor_kor', r'\+ koronie ' + N), ('t_lud_zapl', r'lud zaplacil kasom \(E5/B1\) ' + N)]),
    ('Zawor zamkow (110): dzien', [('z_pan', r'zawor: ' + N + ' do panow'), ('z_kor', r'skarbcom krolestw ' + N), ('z_podz', r'czesc podzamcza do kies podzamczy \(E5/B1\) ' + N)]),
    ('Nadwyzka kas (E5/0): dzien', [('nk_kasy', r'miasta \d+: kasy ' + N), ('nk_zapas', r'zapas kupcow \(D4\) ' + N), ('nk_zkasy', r'zamki \d+: kasy ' + N),
                                    ('nk_zzapas', r'zapas \(CastlePurse\.Reserve\) ' + N), ('nk_zapas_wojny', r'zapas wojny \(max\(500000, 60 dob sr28 zwrotu\)\) razem ' + N)]),
    ('Podstawa robocizny (E5/B1): dzien', [('pr_lud', r'towar ludu ' + N), ('pr_wsie', r', wsie ' + N), ('pr_zycie', r'zolnierze i zalogi \(75%\) ' + N),
                                           ('pr_dwor', r'dwor \(80%\) ' + N), ('pr_sale', r'\(Sale\) ' + N), ('pr_kar', r'karawany i gracz ' + N)]),
    ('Kredyt korony (186): dzien', [('b_poz', r'pozyczono dzis ' + N), ('b_spl', r'splacono ' + N + ' zl'), ('b_dlug', r'dlug razem ' + N), ('b_kap', r'Bank: kapital ' + N)]),
    ('Pozyczki koron (korona2): dzien', [('kl2_dlug', r'dlug koron u miast ' + N)]),
    ('Danina z oszczednosci (P1): dzien', [('p1_nadw', r'razem ' + N + ' / ')]),
    ('Pieniadz swiata: dzien', [('sw_razem', r'razem ' + N), ('sw_kiesy_wsi', r'kiesy wsi ' + N), ('sw_skarbce', r'skarbce krolestw ' + N), ('sw_glowy', r'glowy rodow ' + N),
                                ('sw_bank', r'Bank Zelazny ' + N)]),
    ('D staly', [('D_staly', r'mediana -?\d+, 10% -?\d+, 90% -?\d+, razem ' + N), ('D_ziemia', r'czesci D \(swiat, srednio na dobe\): ziemia ' + N)]),
]
SPEC_C = [(p, [(k, re.compile(r)) for k, r in items]) for p, items in SPEC]

K165 = 'na krolestwo (wplywy+zapas, wydatki): '
SKRE = re.compile(r"Skarbce: dzien (\d+) - (.+?)( \(WOJNA\))?: skarbiec (-?\d+) \(([+-]?\d+)\)(.*)")
T184K = re.compile(r"(?:^|; )([^;:]+?): s ([\d.]+)% / pulap ([\d.]+)%, (wojna|pokoj)[^|;]*?skarbiec (-?\d+) / zapas wojny (\d+)(?:, dlug 186 (\d+))?(?:, pobrano (\d+))?")
W5K = re.compile(r"(?:^| ; )([^;:]+?): garnek (\d+) \(danina (\d+)\), potrzeba (\d+) u (\d+) rodow")


def parse(paths):
    w = defaultdict(dict)
    k = defaultdict(lambda: defaultdict(dict))
    for path in paths:
        fw = defaultdict(dict)                         # ten plik (nadpisuje poprzednie dla swoich dob)
        fk = defaultdict(lambda: defaultdict(dict))
        with open(path, encoding='utf-8', errors='replace') as fh:
            for line in fh:
                if len(line) < 14 or line[0] != '[':
                    continue
                body = line[11:]
                c0 = body[:1]
                if c0 not in 'PUDMOKZSN':
                    continue
                if body.startswith('Skarbce: dzien'):
                    m = SKRE.match(body)
                    if m:
                        d = int(m.group(1)); r = fk[d][m.group(2)]
                        r['war'] = 1 if m.group(3) else 0
                        r['skarb'] = int(m.group(4))
                        mm = re.search(r'dlug w Banku \(186\) (\d+)', m.group(6))
                        if mm:
                            r['dlug186'] = int(mm.group(1))
                    continue
                dm = DAYRX.search(body[:120])
                if not dm:
                    continue
                d = int(dm.group(1))
                if body.startswith('Korona: wplywy dnia (165)'):
                    i = body.find(K165)
                    if i > 0:
                        seg = body[i + len(K165):]
                        hits = [(mm.start(), mm) for mm in re.finditer(r"(?:^|, )([A-Z][A-Za-z' ,]*?) (\d+)\+(\d+)", seg)]
                        for j, (pos, mm) in enumerate(hits):
                            end = hits[j + 1][0] if j + 1 < len(hits) else len(seg)
                            chunk = seg[mm.end():end]
                            r = fk[d][mm.group(1).strip(', ')]
                            r['wpl'] = int(mm.group(2)); r['zapas'] = int(mm.group(3))
                            for nm, rx in (('t184', r'\(184 (\d+)\)'), ('P1', r'danina P1 (\d+)'), ('rata186', r'rata186 -(\d+)'), ('kred186', r'kredyt186 \+(\d+)'),
                                           ('dar_out', r'dar -(\d+)'), ('dar_in', r'dar \+(\d+)'), ('kontr', r'kontr\. (\d+)'), ('wezw', r'wezw\. (\d+)'),
                                           ('zwrot', r'zwrot (\d+)/'), ('zwrot_nal', r'zwrot \d+/(\d+)'), ('renty', r'renty (\d+)'), ('W5', r'doplata W5 (\d+)'),
                                           ('M1', r'z rent M1 (\d+)'), ('W5b', r'ze skarbca W5b -(\d+)'), ('M3', r'chleb M3 (\d+)'), ('dwor', r'dwor (\d+)')):
                                m2 = re.search(rx, chunk)
                                r[nm] = int(m2.group(1)) if m2 else 0
                elif body.startswith('Korona: podatek wojenny ludu (184)'):
                    i = body.find('na krolestwo: ')
                    if i > 0:
                        for mm in T184K.finditer(body[i + len('na krolestwo: '):]):
                            r = fk[d][mm.group(1).strip()]
                            r['s184'] = float(mm.group(2)) / 100; r['cap184'] = float(mm.group(3)) / 100; r['war184'] = 1 if mm.group(4) == 'wojna' else 0
                            r['skarb184'] = int(mm.group(5)); r['rez'] = int(mm.group(6)); r['dlug184'] = int(mm.group(7) or 0); r['p184'] = int(mm.group(8) or 0)
                elif body.startswith('Doplata wedlug potrzeby (W5)'):
                    i = body.find('| na krolestwo: ')
                    if i > 0:
                        for mm in W5K.finditer(body[i + len('| na krolestwo: '):]):
                            r = fk[d][mm.group(1).strip()]
                            r['w5pot'] = int(mm.group(2)); r['w5need'] = int(mm.group(4))
                for pre, items in SPEC_C:
                    if body.startswith(pre) or (pre == 'D staly' and pre in body[:60]):
                        for key, rx in items:
                            if key in fw[d]:
                                continue
                            mm = rx.search(body)
                            if mm:
                                try:
                                    fw[d][key] = float(mm.group(1))
                                except ValueError:
                                    pass
        for d, v in fw.items():
            w[d].update(v)
        for d, kk in fk.items():
            for name, v in kk.items():
                k[d][name].update(v)
    return w, k


if __name__ == '__main__':
    lab, outp, paths = sys.argv[1], sys.argv[2], sys.argv[3:]
    w, k = parse(paths)
    json.dump({'label': lab, 'w': {str(d): v for d, v in w.items()}, 'k': {str(d): {n: dict(x) for n, x in v.items()} for d, v in k.items()}}, open(outp, 'w'))
    ds = sorted(d for d in w if len(w[d]) > 20)
    print(lab, 'dob', len(ds), ds[0] if ds else '-', ds[-1] if ds else '-', 'krolestwa (ostatnia doba):', len(k[ds[-1]]) if ds else 0)
