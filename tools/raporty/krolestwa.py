import csv, sys, collections
sys.stdout.reconfigure(encoding='utf-8')
# argv: balans-krolestw.csv economy.csv day_start day_target
bal, eco, d0, dt = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])

def rows(p):
    with open(p, encoding='utf-8-sig', newline='') as f:
        r = csv.reader(f, delimiter=';')
        h = next(r)
        for x in r:
            if len(x) == len(h):
                yield dict(zip(h, x))

NAMES = {'empire':'The Vale','empire_w':'The Reach','empire_s':'House Targaryen','sturgia':'Iron Islands','aserai':'Dorne','vlandia':"House Baratheon of King's Landing",
 'battania':'The North','khuzait':'Dothraki Horde','freefolk':'Free Folk','nightswatch':'Nights Watch','dragonstone':'Dragonstone','riverlands':'Riverlands','stormlands':'Stormlands',
 'volantis':'Volantis','bravos':'Braavos','pentos':'Pentos','norvos':'Norvos','qohor':'Qohor','myr':'Myr','lys':'Lys','tyrosh':'Tyrosh','sarnor':'Sarnor','valyrian':'House Targaryen, Aegon',
 'skagosi':'Skagos','summer':'Summer Isles','yiti':'Yi Ti Exiles','qarth':'Qarth','ibb':'Ibben','nord':'Lorath','westerlands':'Westerlands'}
# balans: ostatnia doba <= dt
last = {}
first = {}
lastday = 0
for x in rows(bal):
    d = int(x['dzien'])
    if d > dt: continue
    k = x['krolestwo']
    if k not in first: first[k] = x
    if d >= lastday: lastday = d
    last[k] = x
cur = {k: v for k, v in last.items() if int(v['dzien']) == lastday}

# economy: ostatnia doba <= dt, suma po krolestwie
E = collections.defaultdict(lambda: collections.Counter())
treas = {}
eday = 0
buf = []
for x in rows(eco):
    try: d = int(x['dzien'])
    except: continue
    if d > dt: continue
    if d > eday: eday = d; buf = []
    if d == eday: buf.append(x)
for x in buf:
    k = x['krolestwo'] or '-'
    c = E[k]
    def f(n):
        try: return float(x[n] or 0)
        except: return 0.0
    c['rody'] += 1
    c['zloto_rodow'] += f('zloto_glowy') + f('zloto_rodziny')
    c['wojsko'] += f('wojsko')
    c['zold'] += f('zold_dzienny')
    c['dochod'] += f('dochod_model')
    c['wydatki'] += f('wydatki_model')
    c['bilans'] += f('bilans_model')
    c['zmiana'] += f('faktyczna_zmiana_dzien')
    try: treas[k] = max(treas.get(k, 0), float(x['skarbiec_krolestwa'] or 0))
    except: pass

print(f"balans doba {lastday} (kampanii {lastday - d0}), economy doba {eday} (kampanii {eday - d0})")
tot_p = sum(int(v['ludzie_partie']) for v in cur.values()) or 1
print("krolestwo | twierdze start->teraz (zdobyte/stracone) | ludzie w partiach | zalogi | % sil swiata | rody | zloto rodow | skarbiec korony | dochod/d | wydatki/d | bilans/d | zold/d | faktyczna zmiana/d")
keys = sorted(cur, key=lambda k: -int(cur[k]['ludzie_partie']))
for k in keys:
    v = cur[k]; nm = NAMES.get(k, k); e = E.get(nm, collections.Counter())
    print(f"{nm} | {first[k]['twierdze_start']}->{v['twierdze']} (+{v['twierdze_zdobyte']}/-{v['twierdze_stracone']}) | {int(v['ludzie_partie']):,} | {int(v['ludzie_zalogi']):,} | "
          f"{100*int(v['ludzie_partie'])/tot_p:.1f}% | {int(e['rody'])} | {e['zloto_rodow']:,.0f} | {treas.get(nm,0):,.0f} | {e['dochod']:,.0f} | {e['wydatki']:,.0f} | {e['bilans']:,.0f} | {e['zold']:,.0f} | {e['zmiana']:,.0f}")
gone = [k for k in first if k not in cur]
print("krolestwa bez wiersza na koniec (upadly?):", gone)
S = collections.Counter()
for k, e in E.items():
    for n in ('wojsko', 'zloto_rodow', 'dochod', 'wydatki', 'bilans', 'zold', 'zmiana'): S[n] += e[n]
print(f"SWIAT: ludzie w partiach {sum(int(v['ludzie_partie']) for v in cur.values()):,}, zalogi {sum(int(v['ludzie_zalogi']) for v in cur.values()):,}, zloto rodow {S['zloto_rodow']:,.0f}, skarbce koron {sum(treas.values()):,.0f}, "
      f"dochod/d {S['dochod']:,.0f}, wydatki/d {S['wydatki']:,.0f}, bilans/d {S['bilans']:,.0f}, faktyczna zmiana/d {S['zmiana']:,.0f}")
