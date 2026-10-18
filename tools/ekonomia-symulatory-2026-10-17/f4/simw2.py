# f4-place-ludu: model kiesy W miast (suma) z fin86 (doby 109200-109235) - wersja z placami z nadwyzki R/P (Households)
# dK = Oext + kap*Hh + kap*idx*E + phi*retained(lag) - base - alpha*(K-R) - hoardW
def run(kmin=0.25, rate=0.01, down=True, hh=True, lo=15.0, hi=30.0, step=0.03, days=361, phi=0.18, lag=28, K0=14.0e6, base=275e3, alpha=0.0105):
    need = 140e3; R, Hn = 10 * need, 30 * need
    Oext = 197e3     # wojsko/dwor/budowy/warsztaty dla W (184k) + jalmuzna i sluzba (13k)
    Hh = 85e3        # place z nadwyzki R/P dla W
    E = 334e3        # robocizna W + doplata W z kas
    credit = 100e3
    K = K0; kap = 1.0; idx = 1.0; hist = [0.0] * lag; res = []
    lordsW = 0
    for t in range(days):
        d = K / need
        kt = 1.0 if d <= lo else (kmin if d >= hi else 1.0 - (d - lo) / (hi - lo) * (1.0 - kmin))
        if d < lo: kap = 1.0
        else: kap += max(-step, min(step, kt - kap))
        if down and t % 7 == 6: idx = max(0.6, idx / 1.03)
        ie = (0.11 + 0.89 * idx) if (down and d >= lo) else 1.0
        paid = kap * ie * E
        hpaid = (kap if hh else 1.0) * Hh
        retained = (E - paid) + (Hh - hpaid)
        back = phi * hist[t % lag]; hist[t % lag] = retained
        hoard = max(0.0, rate * (K - Hn) - credit) if K > Hn else 0.0
        K += Oext + hpaid + paid + back - base - alpha * max(0.0, K - R) - hoard
        res.append((t, K, d, kap, ie, paid, retained, hoard))
    return res

def show(tag, o):
    print(tag)
    for t, K, d, kap, ie, paid, ret, hd in o:
        if t in (0, 30, 60, 90, 120, 160, 240, 360):
            print('  doba %3d: kiesa W %5.2f mln = %5.1f dni koszyka, kappa %.2f, wskaznik W %.2f, place W z kas %6.0f, zostaje u placacych %6.0f, danina W %6.0f' % (t, K / 1e6, d, kap, ie, paid, ret, hd))

show('STARE: danina 3%, bez regul', run(kmin=1.0, rate=0.03, down=False, hh=False))
show('tylko (c) danina 1%', run(kmin=1.0, rate=0.01, down=False, hh=False))
show('(a)+(b)+(c) bez domow (Households NIE), kmin 0.25', run(kmin=0.25, hh=False))
show('(a)+(b)+(c) z domami (domyslne), kmin 0.25', run(kmin=0.25))
for base in (250e3, 300e3):
    o = run(kmin=0.25, base=base)
    print('czulosc base %d:' % base, ' '.join('d%d %.0f dni' % (t, d) for (t, K, d, *_r) in o if t in (60, 120, 160, 240, 360)))
for a in (0.007, 0.015):
    o = run(kmin=0.25, alpha=a)
    print('czulosc alpha %.3f:' % a, ' '.join('d%d %.0f dni' % (t, d) for (t, K, d, *_r) in o if t in (60, 120, 160, 240, 360)))
for ph in (0.0, 0.4):
    o = run(kmin=0.25, phi=ph)
    print('czulosc phi %.2f:' % ph, ' '.join('d%d %.0f dni' % (t, d) for (t, K, d, *_r) in o if t in (60, 120, 160, 240, 360)))
