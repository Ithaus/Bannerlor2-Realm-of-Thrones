import re, sys, json, statistics

# Parse village-purse related daily lines from an Armoury log. Output: per-day dict -> JSON + summary.
path = sys.argv[1]
out = sys.argv[2]

def num(s):
    return int(s.replace(' ', '')) if s is not None else None

def g(rx, line, grp=1, default=None):
    m = re.search(rx, line)
    return num(m.group(grp)) if m else default

FULL = [("renty", "renty"), ("budowy", "budowy"), ("korona (danina, clo, mennica)", "korona"), ("pozostale moduly ticku", "pozostale"),
 ("budzet rodow (166): dwor do kas siedzib, sakiewki zwolnionych do kies wsi", "budzet"),
 ("markietani (E5/A3): sakiewki ludzi w polu -> kasy miast i kiesy wsi", "markietani"),
 ("kiesa ludu (E5/B1): place, robocizna, obrot P, roboty gminy, zakupy wsi, dziesiecina (E5/C3), zwrot przy wylaczeniu (kasy osad <-> kiesy ludu i wsi)", "kiesa_ludu"),
 ("awans stanow (ZC6): kupno i odkup ziemi - kiesy wsi i panowie ziemi", "zc6"),
 ("kalibracja startu (KS): przebudowa zajec wsi (kiesy wsi -> kasy miast) i pola mieszczan (kasy miast -> kiesy ludu)", "ks"),
 ("kupcy zywnosci (KS-H): towar kasa celu -> kasa zrodla, fracht -> kiesy wsi i portow, zwroty", "ksh")]
days = {}
def D(day):
    return days.setdefault(day, {})

TICK_NAMES = {
    'renty': 'renty', 'korona (danina, clo, mennica)': 'korona', 'pozostale moduly ticku': 'pozostale',
    'budzet rodow (166)': 'budzet', 'markietani (E5/A3)': 'markietani', 'kiesa ludu (E5/B1)': 'kiesa_ludu',
    'kalibracja startu (KS)': 'ks', 'kupcy zywnosci (KS-H)': 'ksh', 'awans stanow (ZC6)': 'zc6', 'budowy': 'budowy',
}

with open(path, encoding='utf-8', errors='replace') as f:
    for line in f:
        if '] ' not in line:
            continue
        body = line.split('] ', 1)[1]
        if body.startswith('Przeplywy osad (kiesy wsi): dzien'):
            day = g(r'dzien (\d+)', body)
            d = D(day)
            d['stan'] = g(r'stan (-?\d+)', body)
            d['zmiana'] = g(r'zmiana ([+-]?\d+)', body)
            d['utarg_do_kiesy'] = g(r'z utargu taborow ([+-]?\d+)', body, default=0)
            d['przelewy_gry'] = g(r'przelewy gry \(GiveGoldAction\) ([+-]?\d+)', body, default=0)
            m = re.search(r'lordowie \+(\d+)/-(\d+)', body)
            if m: d['gga_lord_in'] = int(m.group(1)); d['gga_lord_out'] = int(m.group(2))
            m = re.search(r'notable \+(\d+)/-(\d+)', body.split('Armoury tick')[0])
            if m: d['gga_not_in'] = int(m.group(1)); d['gga_not_out'] = int(m.group(2))
            d['tick'] = g(r'Armoury tick dobowy ([+-]?\d+)', body, default=0)
            m = re.search(r'Armoury tick dobowy [+-]?\d+ \[P\] \((.*?)\); reszta', body)
            if m:
                seg = m.group(1)
                for full, key in FULL:
                    mm = re.search(re.escape(full) + r' ([+-]?\d+)', seg)
                    if mm: d['t_' + key] = int(mm.group(1))
            d['reszta'] = g(r'reszta - niezmierzone[^|]*? ([+-]?\d+) \[R\]', body, default=0)
            m = re.search(r'rozklad \[P\]: osad (\d+), min (-?\d+), mediana (-?\d+), max (-?\d+), ponizej 1000 zlota (\d+)', body)
            if m:
                d['n'], d['min'], d['med'], d['max'], d['below1000'] = [int(x) for x in m.groups()]
        elif body.startswith('Przeplywy osad (przyczyny): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            part = body.split('kiesy wsi:')[-1] if 'kiesy wsi:' in body else ''
            d['pr_bk_rynek'] = g(r'BK rynek wsi ([+-]?\d+)', part, default=0)
            d['pr_w1'] = g(r'zakupy wsi wozem \(E3-W1\) ([+-]?\d+)', part, default=0)
            d['pr_inne'] = g(r'Armoury bez licznika: inne ([+-]?\d+)', part, default=0)
            d['pr_rest_after'] = g(r'reszta po nowym pomiarze ([+-]?\d+)', part, default=0)
        elif body.startswith('Gotowka wsi (E5/0): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            m = re.search(r'wplynelo (\d+) = utarg (\d+) \+ przelewy gry (-?\d+) \+ sakiewki zwolnionych (\d+)', body)
            if m: d['gw_in'], d['gw_utarg'], d['gw_przelewy'], d['gw_sakiewki'] = [int(x) for x in m.groups()]
            m = re.search(r'pan \(licznik podatku wsi (\d+) \+ renta (\d+)\)', body)
            if m: d['gw_pan_licznik'], d['gw_renta'] = int(m.group(1)), int(m.group(2))
            d['gw_majatki'] = g(r'majatki BK (\d+)', body, default=0)
            d['gw_korona'] = g(r'korona (\d+) \(', body, default=0)
            d['gw_w7'] = g(r'stawka wojenna \(W7\) (\d+)', body, default=0)
            d['gw_wies'] = g(r'wies \(zostalo w kiesie\) (-?\d+)', body, default=None)
        elif body.startswith('Utarg wsi (112): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            m = re.search(r'oddane (\d+) = pan \(licznik podatku\) (\d+) \(([\d.]+)% po dopisaniu\) \+ wlasciciele majatkow BK (\d+) \+ kiesa wsi od BK (\d+) \+ bez odbiorcy (\d+)', body)
            if m:
                d['u_oddane'], d['u_pan'] = int(m.group(1)), int(m.group(2)); d['u_pan_pct'] = float(m.group(3))
                d['u_majatki'], d['u_kiesa_bk'], d['u_dopisane'] = int(m.group(4)), int(m.group(5)), int(m.group(6))
            d['u_powroty'] = g(r'powroty taborow z utargiem (\d+)', body, default=0)
            d['u_food_paid'] = g(r'kupcy zaplacili (\d+)', body, default=0)
            d['u_food_back'] = g(r'oddane w calosci kiesom wsi \(jak zakup gracza\) (\d+)', body, default=0)
            d['u_coal_back'] = g(r'z licznikow panow do kies wsi (\d+)', body, default=0)
        elif body.startswith('Kiesa ludu: dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            m = re.search(r'wsie (\d+): zakupy (\d+) z (\d+|0) \(([^;]*);\s*chciane z nadwyzka (\d+)', body)
            if m:
                d['kl_zakupy'] = int(m.group(2)); d['kl_potrzeba'] = int(m.group(3)); d['kl_chciane_nadw'] = int(m.group(5))
            else:
                d['kl_zakupy'] = g(r'wsie \d+: zakupy (\d+)', body, default=0)
            d['kl_oplaty'] = g(r'oplaty stale (\d+) \(\d+ wsi\)', body, default=0)
            d['kl_w1'] = g(r'woz kupuje w miescie sprzedazy \(E3-W1\) tak: zakupow \d+ za (\d+) zl', body, default=0)
            d['kl_zaleglosc'] = g(r'zaleglosc koszyka wsi (\d+) zl', body, default=0)
        elif body.startswith('Podatek od nadwyzki wsi (obieg2): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['hv_pobrano'] = g(r'pobrano (\d+)', body, default=0)
            d['hv_glowy'] = g(r'do glow rodow (\d+)', body, default=0)
            d['hv_miasta'] = g(r'do kas miast (\d+)', body, default=0)
            d['hv_zamki'] = g(r'do kas zamkow (\d+)', body, default=0)
            d['hv_korony'] = g(r'do skarbcow koron (\d+)', body, default=0)
            d['hv_stawka'] = float(re.search(r'stawka ([\d.]+)%', body).group(1)) if re.search(r'stawka ([\d.]+)%', body) else None
            m = re.search(r'wsi z potrzeba (\d+), ponizej poziomu normalnego (\d+), oplacily (\d+)', body)
            if m: d['hv_wsi'], d['hv_below'], d['hv_paid_n'] = [int(x) for x in m.groups()]
            d['hv_norm'] = g(r'poziom normalny (\d+) \(kiesy', body, default=None)
        elif body.startswith('Dziesiecina (E5/C3): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['ti_wsie'] = g(r'= wsie (\d+)', body, default=0)
            d['ti_jalm_wsi'] = g(r'kiesy wsi (\d+)\)', body, default=0)
            d['ti_nopriest_wsi'] = g(r'wsie - zostalo w kiesie (\d+)', body, default=0)
            d['ti_nomarket'] = g(r'wsie bez miasta targowego \(zostalo w kiesie\) (\d+)', body, default=0)
        elif body.startswith('Markietani (E5/A3): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['su_wsie'] = g(r'kiesy wsi (\d+) w \d+ wsiach', body, default=0)
            d['su_do_kies'] = g(r'do kies wsi (\d+)', body, default=0)
            d['su_licznik'] = g(r'na liczniki podatku wsi \(pan wsi\) (\d+)', body, default=0)
        elif body.startswith('Stawka wojenna pana (W7): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['w7_wsi'] = g(r'ze wsi (\d+)', body, default=0)
        elif body.startswith('Pozyczki od ludu (obieg2): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['ln_wsi'] = g(r'z kies miast \d+, podzamczy \d+, wsi (\d+)\)', body, default=0)
            d['ln_rep_wsi'] = g(r'splacone \d+ \(do kies miast \d+, podzamczy \d+, wsi (\d+)', body, default=0)
            d['ln_dlug'] = g(r'dlug rodow u ludu (\d+)', body, default=None)
        elif body.startswith('Obieg: BK i BetterEconomy: dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['bk_rynek_wsi'] = g(r'rynek wsi [+-]?\d+ \(wsie (-?\d+)', body, default=0)
            d['bk_rynek_pominiete'] = g(r'pominiete bez zlota (\d+)', body, default=0)
        elif body.startswith('Zbytki ludu (P3-P5): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            m = re.search(r'wsie \(P5\): z nadwyzka (\d+), chciane (\d+) zl, kupione (\d+) zl', body)
            if m: d['p5_n'], d['p5_chciane'], d['p5_kupione'] = [int(x) for x in m.groups()]
        elif body.startswith('Budzet rodow (166): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['br_sakiewki'] = g(r'sakiewki zwolnionych do kies wsi (\d+)', body, default=0)
        elif body.startswith('Myto targowe (E3-G3): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['my_wozy'] = g(r'wozy wsi: sprzedazy \d+, utarg netto \d+, myto (\d+)', body, default=0)
            d['my_utarg'] = g(r'wozy wsi: sprzedazy \d+, utarg netto (\d+)', body, default=0)
        elif body.startswith('Drobne zrodla (E5/A7): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['ra_gra'] = g(r'z kies wsi (\d+), uciete', body, default=0)
        elif body.startswith('Rabunek osada po osadzie (E7/R1): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['ra_r1'] = g(r'zloto z kiesy wsi (\d+) zl', body, default=0)
        elif body.startswith('Zelazna cena (WW1): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            v = re.findall(r'zloto z kies wsi (\d+)', body)
            d['ww1'] = sum(int(x) for x in v)
        elif body.startswith('KS: pieniadze planu'):
            day = g(r'doba (\d+)', body); d = D(day + 1)
            d['ks_wyd_spoza'] = g(r'wydatki wsi spoza koszyka (\d+) zl/d', body, default=None)
            d['ks_fracht'] = g(r'fracht KS-H do wsi (\d+) zl/d', body, default=None)
        elif body.startswith('Koszyk stanow (ZC4): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            m = re.search(r'wsie: zakupow (\d+) za (\d+) zl', body)
            if m: d['zc4_n'], d['zc4_zl'] = int(m.group(1)), int(m.group(2))
        elif body.startswith('Pieniadz swiata: dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['ps_wsi'] = g(r'kiesy wsi (\d+)', body, default=None)
            d['ps_licznik'] = g(r'podatek wsi (\d+)', body, default=None)
            d['ps_tabory'] = g(r'tabory wsi (\d+)', body, default=None)
        elif body.startswith('Korona: dzien') and 'danina wojenna z kas osad' in body:
            day = g(r'dzien (\d+)', body); d = D(day - 1)
            d['ko_danina_all'] = g(r'danina wojenna z kas osad (\d+)', body, default=0)
        elif body.startswith('Prawa panskie (E5/B1): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            d['pp_oplaty'] = g(r'oplaty stale wsi (\d+)', body, default=0)
        elif body.startswith('Hearth (E4/0): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            m = re.search(r'ludzie wsi (\d+)', body)
            if m: d['ludzie_wsi'] = int(m.group(1))
        elif body.startswith('Straznik obiegu (obieg2): dzien'):
            day = g(r'dzien (\d+)', body); d = D(day)
            m = re.search(r'poziom normalny ludu \d+ \([^)]*wsie (\d+)\)', body)
            if m: d['sg_norm_wsi'] = int(m.group(1))
            m = re.search(r'kiesy z potrzeba \d+ \([^)]*wsie (\d+)\)', body)
            if m: d['sg_need_wsi'] = int(m.group(1))

json.dump({str(k): v for k, v in sorted(days.items())}, open(out, 'w'), indent=0)
print('days', len(days), 'first', min(days), 'last', max(days))
