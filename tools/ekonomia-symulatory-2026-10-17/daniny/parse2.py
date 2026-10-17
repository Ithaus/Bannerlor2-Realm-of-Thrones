import re, sys, statistics as st
from collections import defaultdict, OrderedDict

# (prefix of line, key, regex) - first group = value (int, may be signed)
SPEC = [
    ('Gotowka wsi (E5/0): dzien', [
        ('G_wplynelo', r'wplynelo (\d+)'), ('G_utarg', r'= utarg (\d+)'), ('G_przelewy', r'przelewy gry (-?\d+)'),
        ('G_sakiewki', r'sakiewki zwolnionych (\d+) \|'), ('G_pan_licznik', r'pan \(licznik podatku wsi (\d+)'), ('G_pan_renta', r'\+ renta (\d+)\)'),
        ('G_majatki', r'majatki BK (\d+) \('), ('G_korona', r'korona (-?\d+) \('), ('G_W7', r'stawka wojenna \(W7\) (\d+)'), ('G_wies', r'wies \(zostalo w kiesie\) (-?\d+)')]),
    ('Utarg wsi (112): dzien', [
        ('U_powroty', r'powroty taborow z utargiem (\d+)'), ('U_oddane', r'oddane (\d+) ='), ('U_pan', r'pan \(licznik podatku\) (\d+)'),
        ('U_majatki', r'wlasciciele majatkow BK (\d+)'), ('U_kiesaBK', r'kiesa wsi od BK (\d+)'), ('U_dopisane', r'z tego dopisane kiesom wsi (\d+)'),
        ('U_kupcy_zaplacili', r'kupcy zaplacili (\d+)'), ('U_kupione_oddane', r'oddane w calosci kiesom wsi \(jak zakup gracza\) (\d+)'),
        ('U_zwrot_drewno', r'z licznikow panow do kies wsi (\d+)'), ('U_sakwy_do_kies', r'wsiom macierzystym (\d+) do kies')]),
    ('Podatek od nadwyzki wsi (obieg2): dzien', [
        ('H_stawka_x1000', r'stawka (\d+\.\d+)%/d'), ('H_pobrano', r'pobrano (\d+):'), ('H_glowy', r'do glow rodow (\d+)'), ('H_kasy_miast', r'do kas miast (\d+)'),
        ('H_kasy_zamkow', r'do kas zamkow (\d+)'), ('H_korony', r'do skarbcow koron (\d+)'), ('H_ponizej', r'ponizej poziomu normalnego (\d+), oplacily'),
        ('H_oplacily', r'oplacily (\d+)'), ('H_kiesy_przed', r'kiesy wsi przed / po (\d+)'), ('H_poziom', r'poziom normalny (\d+) \(kiesy'),
        ('H_ponad_przed', r'ponad poziom normalny przed / po (\d+)'), ('H_oblezona', r'osada macierzysta oblezona (\d+)')]),
    ('Prawa panskie (E5/B1): dzien', [('P_oplaty_stale', r'oplaty stale wsi (\d+)'), ('P_wsi', r'oplaty stale wsi \d+ \((\d+) wsi'),
        ('P_bk_podstawa', r'utargu: z (\d+) zostalo'), ('P_bk_zostalo', r'zostalo wsiom (\d+)'), ('P_dopisek_pana', r'dopisku pana z utargu (\d+)')]),
    ('Dziesiecina (E5/C3): dzien', [('T_wsie', r'= wsie (\d+)'), ('T_nalezne', r'nalezne (\d+)'), ('T_jalmuzna_wsi', r'kiesy wsi (\d+)\)'),
        ('T_bez_kaplana_wsie', r'wsie - zostalo w kiesie (\d+)'), ('T_bez_targu', r'wsie bez miasta targowego \(zostalo w kiesie\) (\d+)')]),
    ('Kiesa ludu: dzien', [('K_wsie_zakupy', r'wsie \d+: zakupy (\d+) z'), ('K_wsie_oplaty', r'oplaty stale (\d+) \(\d+ wsi\)'),
        ('K_W1_zl', r'\(E3-W1\) tak: zakupow \d+ za (\d+) zl'), ('K_zaleglosc', r'zaleglosc koszyka wsi (\d+) zl')]),
    ('Markietani (E5/A3): dzien', [('M_wsie', r'kiesy wsi (\d+) w \d+ wsiach'), ('M_do_kies', r'do kies wsi (\d+), na liczniki'),
        ('M_liczniki', r'na liczniki podatku wsi \(pan wsi\) (\d+)'), ('M_wlasne', r'wlasne wsie rodu partii bez daniny \(K3\) (\d+)'), ('M_razem', r'z sakiewek (\d+)')]),
    ('Budzet rodow (166): dzien', [('B_zwolnieni_wsie', r'sakiewki zwolnionych do kies wsi (\d+) zl')]),
    ('Stawka wojenna pana (W7): dzien', [('W7_wsie', r'ze wsi (\d+)\)'), ('W7_razem', r'pobrano (\d+) \(z ludu')]),
    ('Pozyczki od ludu (obieg2): dzien', [('L_wsi_pozyczone', r'podzamczy \d+, wsi (\d+)\)'), ('L_wsi_splacone', r'podzamczy \d+, wsi (\d+), do kas osad'),
        ('L_dlug', r'dlug rodow u ludu (\d+)')]),
    ('Zbytki ludu (P3-P5): dzien', [('Z_wsie_kupione', r'wsie \(P5\): z nadwyzka \d+, chciane \d+ zl, kupione (\d+) zl'), ('Z_wsie_chciane', r'wsie \(P5\): z nadwyzka \d+, chciane (\d+) zl')]),
    ('Rabunek osada po osadzie (E7/R1): dzien', [('R_zloto_wsi', r'zloto z kiesy wsi (\d+) zl')]),
    ('Obieg: BK i BetterEconomy: dzien', [('BK_rynek_wsi', r'rynek wsi [+-]?\d+ \(wsie (-?\d+)')]),
    ('Pompy - sztuczne przelewy (nekon): dzien', [('PM_korona_z_utargu', r'korona z utargu wsi (\d+)'), ('PM_danina_markiet', r'danina markietanow zamiast do kies wsi (\d+)'),
        ('PM_myto_wozow', r'myto wozow wsi - utarg wsi przed 112 (\d+)')]),
    ('Korona: dzien', [('C_danina_osad', r'danina wojenna z kas osad (\d+)')]),
    ('Myto targowe (E3-G3): dzien', [('MY_wozy_myto', r'wozy wsi: sprzedazy \d+, utarg netto \d+, myto (\d+)'), ('MY_wozy_utarg', r'wozy wsi: sprzedazy \d+, utarg netto (\d+)')]),
    ('Kupcy zywnosci (KS-H) dzien', [('KH_fracht_wsie', r'fracht \d+ zl \(wsie (\d+)'), ('KH_kasa_celu_mala', r'kasa celu za mala (\d+) szt'), ('KH_zamowione', r'\(zamowione (\d+)\)'), ('KH_kupione', r'kupione (\d+) szt')]),
    ('Majatki BK (187): dzien', [('MJ_udzial', r'udzial z utargu od wczoraj \(AccumulateTradeTax, placi kasa miasta\) (\d+)')]),
    ('Zelazo (lupy) (214): dzien', [('ZL_targ_zl', r'targ \d+ za (\d+) zl'), ('ZL_woz_zl', r'wozem kupione \d+ ladunkow za (\d+) zl'), ('ZL_brak_zlota', r'brak zlota wsi (\d+)')]),
    ('Kalibracja startu (KS): dzien', [('KS_przebudowa', r'przebudowa kiesy wsi (-?\d+)')]),
    ('Pieniadz swiata: dzien', [('PS_kiesy_wsi', r'kiesy wsi (\d+) \('), ('PS_licznik_wsi', r'podatek wsi (\d+) \('), ('PS_tabory', r'tabory wsi (\d+) \(')]),
]

def run(path):
    days = defaultdict(dict)
    cur = None
    for line in open(path, encoding='utf-8', errors='replace'):
        body = line[11:] if line.startswith('[') else line
        for pre, items in SPEC:
            if body.startswith(pre):
                m = re.search(r'dzien (\d+)', body)
                d = int(m.group(1)) if m else -1
                for k, rx in items:
                    mm = re.search(rx, body)
                    if mm:
                        v = mm.group(1)
                        val = float(v) if '.' in v else int(v)
                        days[d][k] = val
        if body.startswith('Przeplywy osad (przyczyny): dzien'):
            m = re.search(r'dzien (\d+)', body); d = int(m.group(1))
            seg = body.split('| kiesy wsi:')
            if len(seg) > 1:
                s = seg[1].split('| kontrola')[0]
                for name, val in re.findall(r'([A-Za-z][A-Za-z0-9 ()/\-]*?) ([+-]\d+)', s):
                    days[d]['PR_' + name.strip()] = days[d].get('PR_' + name.strip(), 0) + int(val)
    return days

def summarize(days, label, d0=None, d1=None):
    keys = OrderedDict()
    for d in days:
        for k in days[d]:
            keys[k] = 1
    ds = sorted(x for x in days if x > 0 and (d0 is None or x >= d0) and (d1 is None or x <= d1))
    print('==', label, 'dni', len(ds), ds[0] if ds else '-', ds[-1] if ds else '-')
    for k in keys:
        v = [days[d][k] for d in ds if k in days[d]]
        if not v: continue
        print('  %-28s n %3d avg %10.1f med %10.1f min %10.1f max %10.1f first %s last %s' % (k, len(v), sum(v) / len(v), st.median(v), min(v), max(v), v[0], v[-1]))

if __name__ == '__main__':
    p = sys.argv[1]
    days = run(p)
    summarize(days, p)
    if len(sys.argv) > 2 and sys.argv[2] == 'split':
        ds = sorted(x for x in days if x > 0)
        summarize(days, p + ' [pierwsze 30]', ds[0], ds[0] + 29)
        summarize(days, p + ' [ostatnie 30]', ds[-1] - 29, ds[-1])
