import re, sys, statistics as st

MN = ["renty", "budowy", "korona (danina, clo, mennica)", "pozostale moduly ticku", "paser band (skup lupu)",
      "bandy i kryjowki (zycie w miastach)", "kontrakty surowca (174)",
      "budzet rodow (166): dwor do kas siedzib, sakiewki zwolnionych do kies wsi",
      "markietani (E5/A3): sakiewki ludzi w polu -> kasy miast i kiesy wsi",
      "kiesa ludu (E5/B1): place, robocizna, obrot P, roboty gminy, zakupy wsi, dziesiecina (E5/C3), zwrot przy wylaczeniu (kasy osad <-> kiesy ludu i wsi)",
      "awans stanow (ZC6): kupno i odkup ziemi - kiesy wsi i panowie ziemi",
      "kalibracja startu (KS): przebudowa zajec wsi (kiesy wsi -> kasy miast) i pola mieszczan (kasy miast -> kiesy ludu)",
      "kupcy zywnosci (KS-H): towar kasa celu -> kasa zrodla, fracht -> kiesy wsi i portow, zwroty",
      "wywoz kupcow (PodazPopyt: bron, zbroja, konie i uprzaz)"]
SHORT = ["renty", "budowy", "korona", "pozostale", "paser", "bandy", "kontrakty", "budzet166", "markietani", "kiesaLudu", "ZC6", "KS", "KS-H", "wywoz"]

def num(s):
    return int(s.replace('+', ''))

def parse(path, d0=None, d1=None):
    rows = []
    for line in open(path, encoding='utf-8', errors='replace'):
        if 'Przeplywy osad (kiesy wsi): dzien' not in line:
            continue
        r = {}
        m = re.search(r'dzien (\d+) \| stan (\d+), zmiana ([+-]?\d+)', line)
        r['day'] = int(m.group(1)); r['stan'] = int(m.group(2)); r['zmiana'] = num(m.group(3))
        m = re.search(r'z utargu taborow ([+-]\d+)', line); r['utarg'] = num(m.group(1)) if m else 0
        m = re.search(r'przelewy gry \(GiveGoldAction\) ([+-]\d+)', line); r['gga'] = num(m.group(1)) if m else 0
        for side in ['lordowie', 'notable', 'karawany', 'inne partie', 'gracz', 'inni bohaterowie', 'inne osady']:
            m = re.search(r'[(,] ?' + side + r' \+(\d+)/-(\d+)', line)
            r['gga_' + side + '+'] = int(m.group(1)) if m else 0
            r['gga_' + side + '-'] = int(m.group(2)) if m else 0
        m = re.search(r'Armoury tick dobowy ([+-]\d+)', line); r['tick'] = num(m.group(1)) if m else 0
        for n, s in zip(MN, SHORT):
            m = re.search(re.escape(n) + r' ([+-]\d+)', line)
            r[s] = num(m.group(1)) if m else 0
        m = re.search(r'reszta - niezmierzone[^)]*\) ([+-]?\d+)', line); r['reszta'] = num(m.group(1)) if m else 0
        m = re.search(r'osad (\d+), min (\d+), mediana (\d+), max (\d+), ponizej 1000 zlota (\d+)', line)
        if m:
            r['n'] = int(m.group(1)); r['min'] = int(m.group(2)); r['med'] = int(m.group(3)); r['max'] = int(m.group(4)); r['lt1000'] = int(m.group(5))
        rows.append(r)
    # dedupe by day keep last
    dd = {}
    for r in rows:
        dd[r['day']] = r
    rows = [dd[k] for k in sorted(dd)]
    if d0: rows = [r for r in rows if r['day'] >= d0]
    if d1: rows = [r for r in rows if r['day'] <= d1]
    return rows

def summary(rows, label):
    keys = ['zmiana', 'utarg', 'gga', 'gga_lordowie+', 'gga_lordowie-', 'gga_notable+', 'gga_notable-', 'gga_karawany+', 'gga_karawany-', 'gga_inne partie+', 'gga_gracz+', 'gga_gracz-', 'tick'] + SHORT + ['reszta']
    print('==', label, 'dni', len(rows), 'od', rows[0]['day'], 'do', rows[-1]['day'], 'stan', rows[0]['stan'], '->', rows[-1]['stan'])
    for k in keys:
        v = [r.get(k, 0) for r in rows]
        if all(x == 0 for x in v):
            continue
        print('  %-18s avg %9.0f  med %9.0f  min %9d  max %9d' % (k, sum(v) / len(v), st.median(v), min(v), max(v)))
    for k in ['med', 'max', 'lt1000']:
        v = [r[k] for r in rows if k in r]
        print('  %-8s start %s  end %s  avg %.0f' % (k, v[0], v[-1], sum(v) / len(v)))

if __name__ == '__main__':
    for p in sys.argv[1:]:
        rows = parse(p)
        summary(rows, p)
        n = len(rows)
        if n > 40:
            summary(rows[:30], p + ' [pierwsze 30]')
            summary(rows[-30:], p + ' [ostatnie 30]')
