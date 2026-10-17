# Wlasne przeliczenie z logow (projekt DS - daniny sredniowieczne). Tylko odczyt logow.
# Uzycie: python rc.py <log> <etykieta> <out.json>
import re, sys, json
from collections import defaultdict
N = r'(-?\d+)'
SPEC = [
 # wsie
 ('Przeplywy osad (kiesy wsi): dzien', [
   ('v_stan', r'\| stan ' + N), ('v_zmiana', r'zmiana ' + N), ('v_utarg', r'z utargu taborow \+?' + N),
   ('v_przelewy', r'przelewy gry \(GiveGoldAction\) \+?' + N), ('v_renty', r'renty ' + N),
   ('v_korona', r'korona \(danina, clo, mennica\) ' + N), ('v_166', r'sakiewki zwolnionych do kies wsi \+?' + N),
   ('v_markiet', r'kiesy wsi \+?' + N + r', kiesa ludu'), ('v_kiesaludu', r'zwrot przy wylaczeniu \(kasy osad <-> kiesy ludu i wsi\) ' + N),
   ('v_ksh', r'fracht -> kiesy wsi i portow, zwroty \+?' + N), ('v_reszta', r'niezmierzone \(m\.in\. podatek gry od zakupow we wsi\) ' + N),
   ('v_med', r'mediana ' + N), ('v_max', r'max ' + N), ('v_pod1000', r'ponizej 1000 zlota ' + N)]),
 ('Utarg wsi (112): dzien', [
   ('u_oddane', r'oddane ' + N + r' ='), ('u_pan', r'pan \(licznik podatku\) ' + N), ('u_majatki', r'wlasciciele majatkow BK ' + N),
   ('u_kiesaBK', r'kiesa wsi od BK ' + N), ('u_dopisane', r'z tego dopisane kiesom wsi ' + N), ('u_kupcy', r'kupcy zaplacili ' + N)]),
 ('Podatek od nadwyzki wsi (obieg2): dzien', [
   ('hv_pobrano', r'pobrano ' + N + ':'), ('hv_glowy', r'do glow rodow ' + N), ('hv_miasta', r'do kas miast ' + N),
   ('hv_zamki', r'do kas zamkow ' + N), ('hv_korony', r'do skarbcow koron ' + N), ('hv_stawka', r'stawka ([\d.]+)%/d')]),
 ('Podatek od nadwyzki ludu', [
   ('hl_pobrano', r'pobrano ' + N + ' z'), ('hl_glowy', r'do glow rodow ' + N), ('hl_miasta', r'do kas miast ' + N),
   ('hl_zamki', r'do kas zamkow ' + N), ('hl_korony', r'do skarbcow koron z miast i zamkow ' + N)]),
 ('Dziesiecina (E5/C3): dzien', [('ti_wsie', r'= wsie ' + N), ('ti_nalezne', r'nalezne ' + N), ('ti_jalm_wsi', r'kiesy wsi ' + N + r'\)'),
   ('ti_razem', r'razem ' + N)]),
 ('Markietani (E5/A3): dzien', [('mk_razem', r'z sakiewek ' + N), ('mk_miasta', r'-> kasy miast ' + N), ('mk_wsie', r'kiesy wsi ' + N + r' w \d+ wsiach'),
   ('mk_do_kies', r'do kies wsi ' + N + ', na liczniki'), ('mk_liczniki', r'na liczniki podatku wsi \(pan wsi\) ' + N)]),
 ('Obieg: BK i BetterEconomy: dzien', [('bk_rynek_wsi', r'rynek wsi [+-]?\d+ \(wsie ' + N)]),
 ('Kiesa ludu: dzien', [('kl_wsie_zakupy', r'wsie \d+: zakupy ' + N), ('kl_robocizna', r'robocizna ' + N + r' \(w tym'),
   ('kl_lud_zaplacil', r'zaplacone przez lud miast ' + N), ('kl_doplata35', r'doplata do 35% ' + N), ('kl_obrotP', r'obrot P ' + N)]),
 ('Pozyczki od ludu (obieg2): dzien', [('lo_wsi', r'podzamczy \d+, wsi ' + N + r'\)'), ('lo_dlug', r'dlug rodow u ludu ' + N)]),
 ('Stawka wojenna pana (W7): dzien', [('w7_razem', r'pobrano ' + N + r' \(z ludu')]),
 ('Prawa panskie (E5/B1): dzien', [('pp_lud', r'od zarobku ludu miast ' + N), ('pp_W', r'\(W ' + N), ('pp_R', r'/ R ' + N), ('pp_P', r'/ P ' + N + r'\)'),
   ('pp_podz', r'podzamcza ' + N), ('pp_farma', r'farma gmin ' + N), ('pp_wsie', r'oplaty stale wsi ' + N)]),
 # korona
 ('Korona: wplywy dnia (165)', [
   ('k_wpl', r'wplywy dnia ' + N + r' \(nasze'), ('k_nasze', r'1/9 prawa trzecich \(178\) ' + N), ('k_184', r'w tym podatek wojenny ludu \(184\) ' + N),
   ('k_P1', r'danina z oszczednosci ludu \(P1\) ' + N), ('k_inne', r'splata dlugu wobec korony ' + N + r'\)'), ('k_zapas', r'zapasu ponad 500000 ' + N),
   ('k_rata186', r'rata kredytu Banku \(186\) ' + N), ('k_dary', r'dary \(182\) ' + N), ('k_rep', r'raty reparacji ' + N),
   ('k_kontr', r'kontrakty najemnikow \(185\) ' + N), ('k_zwrot', r'zwrot zoldu ' + N + r' \(nalezny'), ('k_zwrot_kred', r'w tym z kredytu Banku \(186\) ' + N),
   ('k_renty', r'renty \(180\) ' + N), ('k_W5', r'\(W5, przed rentami\) ' + N), ('k_M1', r'\(M1\) ' + N),
   ('k_M3', r'\(M3, z reszty wplywow przed rentami\) ' + N), ('k_dwor', r'\(Crown Court Goods tak\) ' + N),
   ('k_W5b', r'\(W5b, poza wplywami dnia - z zapasu\) ' + N), ('k_zostalo', r'zostalo z wplywow dnia w skarbcach ' + N)]),
 ('danina wojenna z kas osad', [('kd_danina', r'danina wojenna z kas osad ' + N), ('kd_clo', r'clo od handlu miast ' + N)]),
 ('powinnosci wasali', [('kd_wasale', r'powinnosci wasali ' + N)]),
 ('Kasy miast (169c)', [('t_zawor', r'zawor: zeszlo ' + N), ('t_zawor_pan', r'zawor: zeszlo -?\d+ = panom ' + N), ('t_zawor_kor', r'\+ koronie ' + N),
   ('t_kasy', r'kasy ' + N + ' wobec'), ('t_zapas', r'zapas kupcow ' + N + ', ponad'), ('t_ponad', r'ponad zapasem ' + N), ('t_lud_zapl', r'lud zaplacil kasom \(E5/B1\) ' + N)]),
 ('Zawor zamkow', [('z_pan', r'zawor: ' + N + ' do panow'), ('z_kor', r'skarbcom krolestw ' + N), ('z_stan', r'stan ' + N + ' w')]),
 ('Danina z oszczednosci (P1)', [('p1_pob', r'pobrano ' + N + ' zl z'), ('p1_nadw', r'razem ' + N + ' / ')]),
 ('Pieniadz swiata (rody - przyczyny)', [('bk_bogaci', r'podatek BK od bogatych do skarbcow ' + N)]),
 ('Korona: podatek wojenny ludu', [('t184', r'pobrano ' + N + r' zl \(ludzie')]),
 ('Myto targowe', [('my_razem', r'pobrano ' + N + ' ='), ('my_korony', r'do zapasu koron ' + N)]),
 ('Kupcy zywnosci (KS-H) dzien', [('kh_kasa_mala', r'kasa celu za mala ' + N + ' szt'), ('kh_zamow', r'\(zamowione ' + N + r'\)'), ('kh_kupione', r'kupione ' + N + ' szt')]),
]
DAYRX = re.compile(r'(?:dzien|doba) (\d{6})')

def run(path):
    days = defaultdict(dict)
    with open(path, encoding='utf-8', errors='replace') as fh:
        for line in fh:
            body = line[11:] if line.startswith('[') else line
            for pre, items in SPEC:
                if pre in body[:200]:
                    m = DAYRX.search(body)
                    if not m: continue
                    d = int(m.group(1))
                    for k, rx in items:
                        mm = re.search(rx, body)
                        if mm:
                            v = mm.group(1)
                            days[d][k] = float(v)
    return days

def avg(days, keys, ds):
    out = {}
    for k in keys:
        v = [days[d][k] for d in ds if k in days[d]]
        out[k] = sum(v) / len(v) if v else None
        out[k + '#n'] = len(v)
    return out

if __name__ == '__main__':
    path, lab, outp = sys.argv[1], sys.argv[2], sys.argv[3]
    days = run(path)
    keys = sorted({k for d in days for k in days[d]})
    ds = sorted(d for d in days if len(days[d]) > 5)
    last = ds[-28:]
    res = {'label': lab, 'first_day': ds[0], 'last_day': ds[-1], 'n_days': len(ds),
           'all': avg(days, keys, ds), 'last28': avg(days, keys, last), 'first7': avg(days, keys, ds[:7]),
           'series': {k: [(d, days[d].get(k)) for d in ds if k in days[d]] for k in ('v_stan', 'v_med', 'v_pod1000')}}
    json.dump(res, open(outp, 'w'), indent=0)
    print(lab, ds[0], ds[-1], len(ds))
