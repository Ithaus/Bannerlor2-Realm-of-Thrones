import sys, collections

# Uzycie: python -I ranga.py <budzet-rodow.csv> <ludzie-regiony.csv> <etykieta>
# Dla kazdego zdobycia, ktore przeszlo przez decyzje (rod dnia zdobycia != rod po decyzji),
# podaje pozycje zwyciezcy na liscie rodow krolestwa w kolejnosci Clan.All (kolejnosc wierszy budzet-rodow)
# liczac tylko rody, ktore kiedykolwiek mialy lenno (przyblizenie "Full Peerage" BK).
bud, lud, label = sys.argv[1], sys.argv[2], sys.argv[3]

order = {}          # rod_id -> pierwszy numer wiersza dnia 1 (kolejnosc Clan.All)
name2ids = collections.defaultdict(set)
day_members = collections.defaultdict(list)   # dzien -> [(krol, rod_nazwa, rod_id, rodzaj)]
ever_fief = set()
D0 = None
with open(bud, encoding='utf-8', errors='replace') as f:
    f.readline()
    for line in f:
        p = line.split(';', 7)
        try:
            d = int(p[0])
        except ValueError:
            continue
        if D0 is None:
            D0 = d
        rid, rn, kr, rk = p[2], p[3], p[4], p[5]
        if rid not in order:
            order[rid] = len(order)
        name2ids[rn].add(rid)
        day_members[d].append((kr, rn, rid, rk))
        if rk in ('miasto', 'zamek'):
            ever_fief.add(rid)

# zdobycia z ludzie-regiony (jak w lenna.py)
owner = {}
hist = collections.defaultdict(list)
name = {}
with open(lud, encoding='utf-8', errors='replace') as f:
    f.readline()
    for line in f:
        p = line.split(';', 8)
        try:
            d = int(p[0])
        except ValueError:
            continue
        sid, nm, kr, rod = p[1], p[2], p[5], p[6]
        name[sid] = nm
        o = (kr, rod)
        if owner.get(sid) != o:
            hist[sid].append((d, kr, rod))
            owner[sid] = o

dec = []
for sid, h in hist.items():
    for i in range(1, len(h)):
        d, kr, rod = h[i]
        pd, pkr, prod = h[i - 1]
        if kr == pkr or rod == prod:
            continue
        final, fday = rod, d
        if i + 1 < len(h):
            d2, kr2, rod2 = h[i + 1]
            if kr2 == kr and d2 - d <= 10:
                final, fday = rod2, d2
        dec.append((fday, sid, kr, rod, final))
dec.sort()

print(f"== {label}")
print("dzien_decyzji;osada;krolestwo;rod_dnia_zdobycia(krol);zwyciezca;pozycja_zwyciezcy;pierwsze_5_rodow_krolestwa(kolejnosc Clan.All, rody z lennem)")
stat = collections.Counter()
for (fday, sid, kr, rod, final) in dec:
    dd = fday - 1 if (fday - 1) in day_members else fday
    mem = [(order[rid], rn, rid) for (k, rn, rid, rk) in day_members.get(dd, []) if k == kr and rid in ever_fief]
    mem.sort()
    names = [rn for _, rn, _ in mem]
    pos = names.index(final) + 1 if final in names else -1
    tag = 'glos' if final != rod else 'bez zmiany (krol zatrzymal / fabula)'
    if final != rod:
        stat[pos] += 1
    print(f"{fday - D0 + 1};{name[sid]};{kr};{rod};{final};{pos if final != rod else '-'} [{tag}];{', '.join(names[:5])}")
print("\nPozycja zwyciezcy (tylko decyzje, gdzie rod sie zmienil): " + ', '.join(f"{k}: {v}" for k, v in sorted(stat.items())))
