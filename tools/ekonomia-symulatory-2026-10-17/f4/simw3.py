# f4-place-ludu (recenzja 18.10): model ZAMKNIETY kiesy W miast z KS - dokad idzie pieniadz, ktorego pracodawca nie zaplacil.
# simw2.py zwracal do W tylko phi = 18% zatrzymanego pieniadza i gubil reszte (82%) - tu kazda moneta ma platnika i odbiorce:
#   W (kiesa wyrobnikow, suma 97 miast), G (kasy miast ponad 1.5 x zapas kupcow - zrodlo wyplaty kupcom M6), OP / OR (kiesy P i R ponad zapas dni),
#   panowie i korona (to, co wychodzi z obiegu miast: danina od skarbca, czesc firmy z oszczednosci, P1, firmy DS, zaleglosci firm).
# Liczby z fin86 (Armoury-2026-10-17_23-22-37.log, doba 109235): Kiesa ludu, Akumulator kies, Firmy miast i zamkow (DS), Kasy miast (169c), Nadwyzka kas (E5/0).
# Przeplywy (zl/d, suma miast):
#   W:  + Oext (wojsko, dwor, budowy, warsztaty, jalmuzna) + kap*fW*E (robocizna i doplata z kas) + kapH*w*(0.6*OP + OR) (place z nadwyzki P i R)
#       - base (koszyk i stale) - aW*(K - RW) (zbytki kupione -> kasy, P1 -> korona, czesc firmy z oszczednosci -> panowie) - danina W (67% panom, 33% kasom)
#   G:  + baza (stacjonarna) + zatrzymane z robocizny i doplaty (E - zaplacone) (czesc qpoor w biednych miastach splaca zaleglosci firm -> panowie/korona)
#       + zmiany zakupow ludu i 33% danin - M6 = G/28 (70% P, 30% R) - przyrost firmy DS przy przegladzie (P5: co 91 dob, najwyzej 15% firmy, panom 61% / koronie 39%)
#   OP: + baza + 0.7*M6 - w*OP (60% do W x kapH - reszta zostaje u P -, 40% do R) - zbytki 0.39% -> kasy - P1 0.12% -> korona - czesc firmy 0.3% -> panowie
#       - danina P (krancowo 3% ponad poziom normalny; 67% panom, 33% kasom)
#   OR: + baza + 0.3*M6 + 0.4*w*OP - w*OR (do W x kapH) - zbytki/P1/firma jak P - danina R
# Baza G/OP/OR dobrana tak, ze bez regul (kap 1, danina 3%) G, OP, OR stoja (fin86); W rosnie +7 tys./d jak w logu.
# Wynik: zapas dni W, kasy ponad 1.5 x zapas (zapas G), P i R ponad zapas, dochod panow i korony z obiegu miast na dobe (wobec STAREGO), danina P.
W_NEED = 140e3


def run3(kmin=0.10, rate=0.01, down=False, hh=True, p5=False, lo=15.0, hi=30.0, step=0.03, days=361, K0=14.2e6,
         base=275e3, aW=0.0105, oext=197e3, E=334e3, qpoor=0.15, arrears0=1.69e6, hp_marg=1.0, ramp_a=True, cramp=True, old=False):
    need = W_NEED; RW, Hn = 10 * need, 30 * need
    w = 0.7 / 15.0                                     # place z nadwyzki R/P: 1/15 dziennie ponad zapas (DsFolkSurplusSpendDays) x 70% (SurpWage)
    luxB, p1P, p1R, fsh = 0.0039, 0.0012, 0.0015, 0.003   # zbytki kupione (0.3/60 x 78%), P1 P/R, czesc firmy z oszczednosci (ulamki/d)
    aWc, aWk, aWl = 0.0055, 0.0025, 0.0025              # podzial aW: kasy (zbytki), korona (P1), panowie (czesc firmy)
    G0, OP0, OR0 = 136.8e3 * 28, 1.28e6, 1.055e6       # M6 fin86 136 838/d = G/28; P i R ponad zapas dni (Kiesa ludu: 'kiesa ponad zapas dni')
    OPn, ORn = OP0 - 10.4e3 / 0.03, OR0 - 8.2e3 / 0.03 # poziom, od ktorego liczy sie danina P/R (fin86 danina P 10 440, R 8 238)
    farm0 = 300e3                                      # firmy miast 109.5 mln/rok = ok. 300 tys./d (panom 61%, koronie 39%)
    hP0 = 0.03 * (OP0 - OPn); hR0 = 0.03 * (OR0 - ORn)
    aP = OP0 * (w + luxB + p1P + fsh) + hP0 - 0.7 * G0 / 28.0
    aR = OR0 * (w + luxB + p1R + fsh) + hR0 - 0.3 * G0 / 28.0 - 0.4 * w * OP0
    # baza kas: stoja przy starym przeplywie (zbytki W przy K0, danina W przy 3%, zbytki P/R, 33% danin)
    hW0 = max(0.0, 0.03 * (K0 - Hn) - 100e3)
    cIn = G0 / 28.0 - (aWc * (K0 - RW) + 0.33 * (hW0 + hP0 + hR0) + luxB * (OP0 + OR0))
    K, G, OP, OR = K0, G0, OP0, OR0
    kap, idx, arrears, farmX = 1.0, 1.0, arrears0, 0.0
    netwin = []
    res = []
    for t in range(days):
        d = K / need
        if old:
            kap, fA = 1.0, 1.0
        else:
            kt = 1.0 if d <= lo else (kmin if d >= hi else 1.0 - (d - lo) / (hi - lo) * (1.0 - kmin))
            if d < lo: kap = 1.0
            else: kap += max(-step, min(step, kt - kap))
            if down and t % 7 == 6: idx = max(0.6, idx / 1.03)
            if down and d >= lo:
                rmp = min(1.0, (d - lo) / (hi - lo)) if ramp_a else 1.0
                fA = 0.11 + 0.89 * (1.0 - (1.0 - idx) * rmp)   # 86 z 97 miast 'duzo wolnych rak'
            else:
                fA = 1.0
        kapH = kap if hh else 1.0
        paidE = kap * fA * E
        retE = E - paidE
        hhFull = w * (0.6 * OP + OR)
        hhPaid = kapH * hhFull
        if old: r_ = 0.03
        elif cramp: r_ = rate + (0.03 - rate) * max(0.0, min(1.0, (kap - kmin) / max(1e-9, 1.0 - kmin)))   # (c) schodzi razem z kappa (poprawka recenzji)
        else: r_ = rate
        hW = max(0.0, r_ * (K - Hn) - 100e3) if K > Hn else 0.0
        hP = 0.03 * hp_marg * max(0.0, OP - OPn); hR = 0.03 * hp_marg * max(0.0, OR - ORn)
        m6 = max(0.0, G) / 28.0
        # zaleglosci firm w biednych miastach: czesc zatrzymanego splaca zaleglosci (panom/koronie), dopoki sa
        toArr = min(arrears, qpoor * retE); arrears -= toArr
        toG = retE - toArr
        # W
        propW = aW * max(0.0, K - RW)
        dK = oext + paidE + hhPaid - base - propW - hW
        # P i R
        pW = w * OP
        dOP = aP + 0.7 * m6 - pW * (0.4 + 0.6 * kapH) - OP * (luxB + p1P + fsh) - hP
        dOR = aR + 0.3 * m6 + 0.4 * pW - kapH * w * OR - OR * (luxB + p1R + fsh) - hR
        # kasy
        cin = cIn + toG + aWc * max(0.0, K - RW) + 0.33 * (hW + hP + hR) + luxB * (OP + OR)
        netwin.append(cin - cIn)                         # doplyw netto kupcow ponad baze (bez M6 - M6 to 'pobrane')
        if p5 and t > 0 and t % 91 == 0:
            avg = sum(netwin[-28:]) / len(netwin[-28:])
            tgt = 0.96 * avg
            farmX += max(-0.15 * farm0, min(0.15 * farm0, tgt - farmX))
        dG = cin - m6 - farmX
        # panowie i korona (z obiegu miast)
        lords = 0.67 * (hW + hP + hR) + aWl * max(0.0, K - RW) + fsh * (OP + OR) + 0.61 * (farmX + toArr)
        crown = aWk * max(0.0, K - RW) + p1P * OP + p1R * OR + 0.39 * (farmX + toArr)
        K += dK; G += dG; OP += dOP; OR += dOR
        res.append(dict(t=t, K=K, d=K / need, kap=kap, fA=fA, paid=paidE, ret=retE, hhPaid=hhPaid, hhCut=hhFull - hhPaid, hW=hW, hP=hP, hR=hR,
                        G=G, m6=m6, OP=OP, OR=OR, lords=lords, crown=crown, farmX=farmX, arr=arrears))
    return res


def show(tag, o, ref=None, at=(0, 30, 60, 120, 160, 364, 728)):
    print(tag)
    for x in o:
        if x['t'] in at:
            r = ref[x['t']] if ref else None
            dl = (' (STARE %+5.0f)' % ((x['lords'] - r['lords']) / 1e3)) if r else ''
            dc = (' (STARE %+5.0f)' % ((x['crown'] - r['crown']) / 1e3)) if r else ''
            print('  doba %3d: W %5.1f dni (kappa %.2f, fA %.2f) | zatrzymane w kasach %4.0f, u P/R %3.0f tys./d | kasy ponad 1.5x zapas %5.2f mln (M6 %4.0f) | '
                  'P/R ponad zapas %4.2f/%4.2f mln | danina W/P/R %4.0f/%3.0f/%3.0f | panom %4.0f%s, koronie %4.0f%s tys./d (firma DS +%3.0f)' % (
                      x['t'], x['d'], x['kap'], x['fA'], x['ret'] / 1e3, x['hhCut'] / 1e3, x['G'] / 1e6, x['m6'] / 1e3, x['OP'] / 1e6, x['OR'] / 1e6,
                      x['hW'] / 1e3, x['hP'] / 1e3, x['hR'] / 1e3, x['lords'] / 1e3, dl, x['crown'] / 1e3, dc, x['farmX'] / 1e3))


if __name__ == '__main__':
    D = 1500
    old = run3(old=True, days=D)
    show('STARE (danina 3%, bez regul):', old)
    show('(b)+(c) bez (a), bez P5, kmin 0.25 (galaz przed scaleniem z P5):', run3(kmin=0.25, days=D), old)
    show('(a)+(b)+(c) + P5, kmin 0.25:', run3(kmin=0.25, down=True, p5=True, days=D), old)
    show('(a)+(b)+(c) + P5, kmin 0.10:', run3(kmin=0.10, down=True, p5=True, days=D), old)
    show('(b)+(c) bez (a), bez P5, kmin 0.10:', run3(kmin=0.10, days=D), old)
    print('czulosc (zapas dni W po 160 / 364 / 1499 dobach; kappa na koncu):')
    for km in (0.25, 0.15, 0.10, 0.0):
        for (lab, kw) in (('bez (a), bez P5', dict()), ('(a)+P5', dict(down=True, p5=True))):
            for b in (250e3, 275e3, 300e3):
                o = run3(kmin=km, base=b, days=D, **kw)
                print('  kmin %.2f %-15s base %3.0f: %5.1f / %5.1f / %5.1f dni, kappa %.2f, kasy ponad 1.5x zapas %5.2f mln, P ponad zapas %4.2f mln, danina P %3.0f tys./d' % (
                    km, lab, b / 1e3, o[160]['d'], o[364]['d'], o[-1]['d'], o[-1]['kap'], o[-1]['G'] / 1e6, o[-1]['OP'] / 1e6, o[-1]['hP'] / 1e3))
    for a in (0.007, 0.015):
        o = run3(kmin=0.10, aW=a, days=D)
        print('  kmin 0.10 bez (a) alpha %.3f: %5.1f / %5.1f / %5.1f dni' % (a, o[160]['d'], o[364]['d'], o[-1]['d']))
    for hm in (0.0, 0.5):
        o = run3(kmin=0.10, hp_marg=hm, days=D)
        print('  kmin 0.10 bez (a) danina P/R krancowo x%.1f: %5.1f / %5.1f / %5.1f dni, P ponad zapas %4.2f mln' % (hm, o[160]['d'], o[364]['d'], o[-1]['d'], o[-1]['OP'] / 1e6))
