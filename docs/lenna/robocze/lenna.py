import sys, collections, csv, io

# Uzycie: python -I lenna.py <ludzie-regiony.csv> <etykieta>
path, label = sys.argv[1], sys.argv[2]
owner = {}          # sid -> (krol, rod)
name = {}
kind = {}
hist = collections.defaultdict(list)  # sid -> [(dzien, krol, rod)] tylko zmiany
first_day = None
last_day = None
day_fiefs = {}      # dzien -> Counter((krol, rod)) (miasto=1, zamek=1 osobno)
cur_day = None
cnt = collections.Counter()
cnt_t = collections.Counter()
start_cnt = None
with open(path, encoding='utf-8', errors='replace') as f:
    hdr = f.readline().rstrip('\n').split(';')
    ix = {h: i for i, h in enumerate(hdr)}
    for line in f:
        p = line.rstrip('\n').split(';')
        try:
            d = int(p[0])
        except ValueError:
            continue
        sid, nm, rk, kr, rod = p[1], p[2], p[3], p[5], p[6]
        if cur_day is not None and d != cur_day:
            day_fiefs[cur_day] = (cnt, cnt_t)
            cnt = collections.Counter(); cnt_t = collections.Counter()
        cur_day = d
        if first_day is None:
            first_day = d
        last_day = d
        name[sid] = nm; kind[sid] = rk
        cnt[(kr, rod)] += 1
        if rk == 'miasto':
            cnt_t[(kr, rod)] += 1
        o = (kr, rod)
        if owner.get(sid) != o:
            hist[sid].append((d, kr, rod))
            owner[sid] = o
    day_fiefs[cur_day] = (cnt, cnt_t)

D0 = first_day
def rel(d):
    return d - D0 + 1

print(f"== {label}: dni {rel(first_day)}..{rel(last_day)} (dzien gry {first_day}..{last_day})")

# Zmiany krolestwa = zdobycia (lub odejscia rodu z lennem)
events = []
for sid, h in hist.items():
    for i in range(1, len(h)):
        d, kr, rod = h[i]
        pd, pkr, prod = h[i-1]
        if kr != pkr:
            # wynik: pierwsza zmiana rodu w tym samym krolestwie do 10 dni po
            final = rod; fday = d
            for j in range(i+1, len(h)):
                d2, kr2, rod2 = h[j]
                if kr2 != kr:
                    break
                if d2 - d <= 10:
                    final = rod2; fday = d2
                break
            typ = 'odejscie_rodu' if rod == prod else 'zdobycie'
            events.append((d, sid, pkr, prod, kr, rod, final, fday, typ))
events.sort()

def fiefs_of(day, kr, rod):
    c, ct = day_fiefs.get(day, (collections.Counter(), collections.Counter()))
    return c[(kr, rod)], ct[(kr, rod)]

print("\n-- Zdobycia (krolestwo zmienione, rod inny niz poprzedni) --")
print("dzien;osada;rodzaj;od_krol;od_rod;do_krol;rod_dnia_zdobycia;rod_po_decyzji;dzien_decyzji;twierdze_zwyciezcy_przed;twierdze_zwyciezcy_po;miasta_zwyciezcy_po")
per_k = collections.defaultdict(collections.Counter)
per_k_n = collections.Counter()
for (d, sid, pkr, prod, kr, rod, final, fday, typ) in events:
    if typ != 'zdobycie':
        continue
    before = fiefs_of(fday - 1, kr, final)[0]
    after, after_t = fiefs_of(fday, kr, final)
    print(f"{rel(d)};{name[sid]};{kind[sid]};{pkr};{prod};{kr};{rod};{final};{rel(fday)};{before};{after};{after_t}")
    per_k[kr][final] += 1
    per_k_n[kr] += 1

print("\n-- Odejscia rodow z lennami (rod ten sam, krolestwo inne) --")
od = collections.Counter()
for (d, sid, pkr, prod, kr, rod, final, fday, typ) in events:
    if typ == 'odejscie_rodu':
        od[(pkr, kr, rod)] += 1
for k, v in od.most_common():
    print(f"{k[0]} -> {k[1]} rod {k[2]}: {v}")

print("\n-- Komu przypadly zdobycze, wg krolestwa --")
for kr, n in per_k_n.most_common():
    parts = ', '.join(f"{r} {c}" for r, c in per_k[kr].most_common())
    print(f"{kr}: {n} zdobyc -> {parts}")

# Zmiany rodu wewnatrz krolestwa bez zdobycia (nadania, fabula, bunty)
print("\n-- Zmiany wlasciciela w obrebie krolestwa (nie w ciagu 10 dni po zdobyciu) --")
cap_days = {(sid, fday) for (d, sid, pkr, prod, kr, rod, final, fday, typ) in events}
inner = []
for sid, h in hist.items():
    for i in range(1, len(h)):
        d, kr, rod = h[i]
        pd, pkr, prod = h[i-1]
        if kr == pkr and rod != prod and (sid, d) not in cap_days:
            inner.append((d, sid, kr, prod, rod))
inner.sort()
for (d, sid, kr, prod, rod) in inner:
    print(f"{rel(d)};{name[sid]};{kind[sid]};{kr};{prod} -> {rod}")

# Twierdze rodow: start i koniec, top
print("\n-- Twierdze rodow (miasta+zamki): start -> koniec, top 25 koncowych --")
c0, ct0 = day_fiefs[first_day]
c1, ct1 = day_fiefs[last_day]
keys = set(c0) | set(c1)
rows = sorted(keys, key=lambda k: (-c1[k], -c0[k]))
for k in rows[:25]:
    print(f"{k[0]} / {k[1]}: {c0[k]} (miasta {ct0[k]}) -> {c1[k]} (miasta {ct1[k]})")

# Udzial top rodu w krolestwie na koniec
print("\n-- Koncentracja na koniec: krolestwo: rodow z twierdza / twierdz / najwiekszy rod --")
byk = collections.defaultdict(list)
for k, v in c1.items():
    byk[k[0]].append((v, k[1]))
for kr, lst in sorted(byk.items(), key=lambda x: -sum(v for v, _ in x[1])):
    lst.sort(reverse=True)
    tot = sum(v for v, _ in lst)
    print(f"{kr}: rodow {len(lst)}, twierdz {tot}, top: " + ', '.join(f"{r} {v}" for v, r in lst[:4]))
