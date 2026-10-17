import sys
sys.path.insert(0, sys.argv[1])
import parse1
rows = parse1.parse(sys.argv[2])
pk = max(rows, key=lambda r: r['stan'])
print('peak', pk['day'], pk['stan'])
for r in rows[::10] + [rows[-1]]:
    print(r['day'], r['stan'], 'med', r.get('med'), 'max', r.get('max'), '<1000', r.get('lt1000'), 'korona', r['korona'], 'kiesaLudu', r['kiesaLudu'], 'reszta', r['reszta'], 'utarg', r['utarg'])
# 10-day window averages
import statistics
for i in range(0, len(rows), 20):
    w = rows[i:i+20]
    f = lambda k: sum(x[k] for x in w)/len(w)
    print('okno %d-%d zmiana %.0f utarg %.0f gga %.0f renty %.0f korona %.0f kiesaLudu %.0f markiet %.0f budzet %.0f reszta %.0f' % (w[0]['day'], w[-1]['day'], f('zmiana'), f('utarg'), f('gga'), f('renty'), f('korona'), f('kiesaLudu'), f('markietani'), f('budzet166'), f('reszta')))
