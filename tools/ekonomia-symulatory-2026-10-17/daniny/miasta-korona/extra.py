import re, sys
from collections import defaultdict
pats = {
 'Pompy': [('A_kasy_lud', r'A kasy miast -> lud (\d+)'), ('B_lud_podatki', r'B lud -> podatki od oszczednosci (\d+)'), ('C_osady_panowie_korony', r'C kasy osad -> panowie i korony (\d+)'),
           ('C_zawor_miast', r'zawor miast (\d+)'), ('C_zawor_zamkow', r'zawor zamkow (\d+)'), ('C_danina_kas', r'danina wojenna z kas (\d+)'), ('C_korona_utarg_wsi', r'korona z utargu wsi (\d+)'),
           ('D_korona_rody', r'D korona -> rody tej samej doby (\d+)'), ('E_rody_dwor', r'E rody -> dwor \(sprzet niewydany\) (\d+)'), ('F_naturalne', r'F naturalne z obrotu \(E3, poza suma\) (\d+)'), ('chleb_M3', r'chleb M3 (\d+)')],
 'Nadwyzka kas': [('danina_miast', r'danina wojenna \d+ z (\d+)'), ('roadtolls', r'Road Tolls \d+ z (\d+)'), ('zamki_kasy', r'zamki 130: kasy (\d+)')],
 'Markietani': [('mark_sakiewki', r'z sakiewek (\d+)'), ('mark_kasy_miast', r'-> kasy miast (\d+)'), ('mark_wsie', r'kiesy wsi (\d+) w'), ('mark_licznik_pana', r'na liczniki podatku wsi \(pan wsi\) (\d+)')],
 'Sakiewka ludzi': [('sak_zycie', r'na zycie w miastach (\d+)'), ('sak_zold', r'zold wplacony do sakiewek (\d+)')],
 'Kasy miast (169c)': [('udzial28', r'srednia 28 dob ([\d.]+)% \(\d+ dob')],
 'Utarg wsi': [],
}
def run(paths):
    data = defaultdict(dict)
    for p in paths:
        for line in open(p, encoding='utf-8', errors='replace'):
            b = line[11:]
            for pre, lst in pats.items():
                if b.startswith(pre):
                    dm = re.search(r'dzien (\d{6})', b)
                    if not dm: continue
                    d = int(dm.group(1))
                    for m, rx in lst:
                        mm = re.search(rx, b)
                        if mm and m not in data[d]: data[d][m] = float(mm.group(1))
    days = sorted(data)
    out = {}
    for m in sorted({m for d in days for m in data[d]}):
        def av(ds):
            v = [data[d][m] for d in ds if m in data[d]]
            return sum(v)/len(v) if v else float('nan')
        out[m] = (av(days[1:8]), av(days[-28:]))
    return out
sets = {'base': ['D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-16_19-02-22.log'], 'ks': ['D:/Backup-Bannerlord/logi-testow/Armoury-2026-10-17_06-07-51.log'],
        'jeff': ['D:/Backup-Bannerlord/logi-jeffa/2026-10-16/Armoury-2026-10-15_22-48-11.log','D:/Backup-Bannerlord/logi-jeffa/2026-10-16/Armoury-2026-10-16_03-08-43.log','D:/Backup-Bannerlord/logi-jeffa/2026-10-16/Armoury-2026-10-16_08-50-07.log','D:/Backup-Bannerlord/logi-jeffa/2026-10-16/Armoury-2026-10-16_10-14-41.log']}
res = {n: run(p) for n, p in sets.items()}
for m in sorted({m for n in res for m in res[n]}):
    print('%-24s' % m + ' | '.join('%s %8.0f/%8.0f' % (n, *res[n].get(m, (float('nan'), float('nan')))) for n in res))
