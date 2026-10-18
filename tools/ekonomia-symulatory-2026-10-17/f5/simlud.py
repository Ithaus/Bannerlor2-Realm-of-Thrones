# f5-lud (noc 17/18.10): model ZAMKNIETY ludu miast z KS - miasto po miescie (97 miast z kiesa-obieg.csv i kasy-miast.csv testu fin88).
# Pytanie paczki: czemu kappa f4 (place W wedlug koszyka) nie sprowadzila W do pasma 15-30 dni i czemu R/P rosna; co zmienia dochod KAZDEJ klasy wedlug jej koszyka
# (W - wszystkie rodzaje dochodu, R i P - wlasne pasma) i dokad idzie pieniadz niezaplacony ludowi (kasy miast -> M6 / firmy DS przy przegladzie -> panowie i korona).
# Uzycie: python -I simlud.py <katalog CSV testu> [dni]
# Przyblizenia (jawne): dochody rodzajow z ostatniego tygodnia (z_* / dob_okna) staja w miejscu (oprocz M6, ktore idzie za nadwyzka kasy); wydatek na koszyk = potrzeba x
# min(1, kiesa / potrzeba); oferta nadwyzki kupiona w 80%; podatki, dziesiecina i podatek wojenny jako stale udzialy zarobku; czesc firmy z oszczednosci (FLf)
# proporcjonalnie do nadwyzki ponad zapas dni; reszta przeplywow kazdej klasy i kasy (jalmuzna, niewolni, skup, KS-H, karawany, ...) jako stala reszta miasta
# dobrana tak, ze model STARYCH zasad odtwarza przyrost klas i kas z ostatnich 28 dob testu. Wynik - kierunek i skala czasu, nie liczby co do tysiaca.
import csv, sys, statistics

DIR = sys.argv[1] if len(sys.argv) > 1 else '.'
DAYS = int(sys.argv[2]) if len(sys.argv) > 2 else 365
D1, D0 = '109234', '109206'          # ostatni wiersz tygodniowy i wiersz 28 dob wczesniej
RD = {'W': 10.0, 'R': 21.0, 'P': 30.0}
KEEP = {'W': 0.91, 'R': 0.85, 'P': 0.92}
TAXE = {'W': 0.01 + 0.01 + 0.042, 'R': 0.02 + 0.01 + 0.02 + 0.084, 'P': 0.03 + 0.03 + 0.126}   # pan + (cech R) + dziesiecina + podatek wojenny 184 (s 8.4% x wagi)


def fl(r, k):
    try:
        return float(r[k].replace(',', '.'))
    except Exception:
        return 0.0


def load():
    ob = [r for r in csv.DictReader(open(DIR + '/kiesa-obieg.csv', encoding='utf-8', errors='replace'), delimiter=';') if r['rodzaj'] == 'miasto']
    km = [r for r in csv.DictReader(open(DIR + '/kasy-miast.csv', encoding='utf-8', errors='replace'), delimiter=';')]
    cof1 = {r['miasto_id']: r for r in km if r['dzien'] == D1}
    cof0 = {r['miasto_id']: r for r in km if r['dzien'] == D0}
    a = {r['osada_id']: r for r in ob if r['dzien'] == D1}
    b = {r['osada_id']: r for r in ob if r['dzien'] == D0}
    towns = []
    for tid, r in a.items():
        if tid not in b or tid not in cof1 or tid not in cof0:
            continue
        r0 = b[tid]; c1 = cof1[tid]; c0 = cof0[tid]
        dob = fl(r, 'dob_okna') or 7.0
        t = dict(id=tid, name=r['osada'], dob=dob)
        for c in 'WRP':
            t['K' + c] = fl(r, 'kiesa_' + c); t['N' + c] = fl(r, 'potrzeba_' + c); t['dK' + c] = (fl(r, 'kiesa_' + c) - fl(r0, 'kiesa_' + c)) / 28.0
        for k in ('z_wojska', 'z_dworu', 'z_budow', 'z_kopaln', 'z_warsztatow', 'z_robocizny', 'z_obrotu', 'z_zaworu'):
            t[k] = fl(r, k) / dob
        t['G'] = fl(c1, 'kasa'); t['Rv'] = fl(c1, 'zapas_kupcow'); t['dG'] = (fl(c1, 'kasa') - fl(c0, 'kasa')) / 28.0
        towns.append(t)
    return towns


def band(d, lo, hi, mn):
    if d < lo:
        return 1.0
    if d >= hi:
        return mn
    return 1.0 - (d - lo) / (hi - lo) * (1.0 - mn)


class P:
    def __init__(self, **kw):
        self.ext = False          # f5: kappa W2 na dochod W z zewnatrz (uslugi, dwor, budowy, warsztaty)
        self.rp = False           # f5: kappa R i P (wlasne pasma) na caly ich dochod (zewnatrz, kasa, M6, place od P dla R)
        self.rpHoard = False      # f5: danina od skarbca R/P schodzi z kappa R/P do 1%
        self.zero = False         # f5: miasto z kotwica, ale bez potrzeby klas (koszyk KS 0) - kappa do najnizej
        self.fast = 0             # f5: przeglad firmy w gore co tyle dob w miescie z kasa ponad 1.5 x zapas kupcow (0 - tylko kwartalny)
        self.fastMax = 0.15
        self.grow = False         # f5: szybki przeglad wedlug przyrostu kasy z 28 dob (firma rosnie o przyrost kasy na dobe), nie wedlug doplywu netto
        self.noDown = False       # f5: przeglad kwartalny nie obniza firmy miasta z kasa ponad 1.5 x zapas (oprocz wymuszonego przy zaleglosciach)
        self.m6cut = True         # f5: kappa R/P tnie tez wyplate kupcom M6 (reszta zostaje w kasie)
        self.housesQ = False      # f5: takze przeglad zwykly (kwartalny) bez podwyzki przy R/P ponizej pasma
        self.share = False        # f5: nieprzyjete przez klase bogata z zarobku z zewnatrz - do biednych klas tego samego miasta (wedlug potrzeby), reszta do kasy
        self.houses = False       # f5: bez podwyzki firmy wedlug nadwyzki, dopoki R albo P miasta sa ponizej dolnej granicy pasma (nadwyzka kupcow najpierw do ich domow przez M6)
        self.shareUp = False      # recenzja f5 (uwaga 2): po podwyzce wedlug nadwyzki udzial firmy (Share) w gore - przeglad kwartalny celuje w ten sam poziom; zmiana firmy
                                  # przegladem kwartalnym - pomiar przyrostu kasy od nowa (jak DsTowns po recenzji)
        self.lo = {'W': 15.0, 'R': 21.0, 'P': 30.0}
        self.hi = {'W': 30.0, 'R': 42.0, 'P': 60.0}
        self.mn = 0.10
        self.step = 0.03
        self.hoardLo = 0.01
        self.review = 91
        self.revMax = 0.15
        self.farm0 = 310.6e3     # firmy miast 113.06 mln/rok (linia Firmy miast i zamkow, 109235)
        self.__dict__.update(kw)


def run(towns, p, days=DAYS, calib=None):
    n = len(towns)
    st = []
    for t in towns:
        s = dict(t)
        dW = s['KW'] / s['NW'] if s['NW'] > 0 else 999.0
        # f4: kappa W i (a) w chwili startu modelu - przy celu (ostatnie 28 dob kappa mediana 0.100, w pasmie liniowo)
        s['kW'] = band(dW, 15, 30, 0.10) if s['NW'] > 0 else 1.0
        s['fA'] = 1.0 - 0.4 * max(0.0, min(1.0, (dW - 15) / 15.0)) if s['NW'] > 0 else 1.0
        f = s['fA'] * s['kW']
        lab = s['z_robocizny']
        s['labFull'] = lab / (0.9 * f + 0.1)                  # robocizna z kasy przy czynniku 1 (W90/R10)
        top = 0.48 * s['z_warsztatow']                          # doplata do 35% (z linii Kiesa ludu: doplata 48 461 z place warsztatow 54 731 + doplata)
        s['topFull'] = top / (0.7 * f + 0.3)
        s['craftExt'] = s['z_warsztatow'] - top
        s['kW2'] = 1.0; s['kR'] = 1.0; s['kP'] = 1.0
        s['farm'] = 0.0; s['farmX'] = 0.0; s['lastRev'] = 0; s['lastFast'] = 0; s['Gh'] = []; s['shK'] = 1.0; s['raised'] = False
        st.append(s)
    totM6 = sum(max(0.0, s['G'] - 1.5 * s['Rv']) / 28.0 for s in st) or 1.0
    totZ = sum(s['z_zaworu'] for s in st) or 1.0
    for s in st:
        s['farm'] = p.farm0 * max(1.0, s['z_obrotu'] + s['z_robocizny']) / max(1.0, sum(x['z_obrotu'] + x['z_robocizny'] for x in st))
    out = []
    net0 = None
    netHist = []
    for day in range(days + 1):
        agg = dict(day=day, KW=0, KR=0, KP=0, NW=0, NR=0, NP=0, G=0, S=0, m6=0, hW=0, hR=0, hP=0, lords=0, crown=0, farm=0, cutW=0, cutR=0, cutP=0,
                   kW=[], kW2=[], kR=[], kP=[], dW=[], dR=[], dP=[], net=0.0, qUp=0, qDown=0, qCutAfter=0, fUp=0)
        nets = []
        for i, s in enumerate(st):
            d = {}
            for c in 'WRP':
                d[c] = s['K' + c] / s['N' + c] if s['N' + c] > 0 else (9999.0 if p.zero else float('nan'))
            # kappy (raz na dobe, krok 3%)
            if day > 0:
                if s['NW'] > 0:
                    kt = band(d['W'], 15, 30, 0.10)
                    s['kW'] = 1.0 if d['W'] < 15 else s['kW'] + max(-p.step, min(p.step, kt - s['kW']))
                    s['fA'] = 1.0 - 0.4 * max(0.0, min(1.0, (d['W'] - 15) / 15.0))
                if p.ext and d['W'] == d['W']:
                    kt = band(d['W'], p.lo['W'], p.hi['W'], p.mn)
                    s['kW2'] = 1.0 if d['W'] < p.lo['W'] else s['kW2'] + max(-p.step, min(p.step, kt - s['kW2']))
                for c in 'RP':
                    if p.rp and d[c] == d[c]:
                        kt = band(d[c], p.lo[c], p.hi[c], p.mn)
                        s['k' + c] = 1.0 if d[c] < p.lo[c] else s['k' + c] + max(-p.step, min(p.step, kt - s['k' + c]))
            kW, fA, kW2, kR, kP = s['kW'], s['fA'], s['kW2'], s['kR'], s['kP']
            # ---- dochody (pelne -> zaplacone; reszta zostaje w kasie / u placacego)
            svc, court, build, craft = s['z_wojska'], s['z_dworu'], s['z_budow'] + s['z_kopaln'], s['craftExt']
            extW = 0.8 * svc + 0.8 * court + 0.6 * build + 0.7 * craft
            extR = 0.1 * svc + 0.2 * court + 0.4 * build + 0.3 * craft
            extP = 0.1 * svc
            inW = extW * kW2; inR = extR * kR; inP = extP * kP
            cutExt = extW * (1 - kW2) + extR * (1 - kR) + extP * (1 - kP)          # nieprzyjete - wolajacy daje kasie miasta
            if p.share and cutExt > 0:
                # f5: czesc nieprzyjeta przez klase ponad pasmem idzie do klas tego miasta ponizej dolnej granicy pasma (wedlug potrzeby), reszta do kasy
                poor = [c for c in 'WRP' if s['N' + c] > 0 and s['K' + c] < p.lo[c] * s['N' + c]]
                if poor:
                    tn = sum(s['N' + c] for c in poor)
                    for c in poor:
                        g = cutExt * s['N' + c] / tn
                        if c == 'W': inW += g
                        elif c == 'R': inR += g
                        else: inP += g
                    cutExt = 0.0
            labW = 0.9 * s['labFull'] * fA * kW; labR = 0.1 * s['labFull'] * kR
            topW = 0.7 * s['topFull'] * fA * kW; topR = 0.3 * s['topFull'] * kR
            turn = s['z_obrotu'] * kP
            cofFull = s['labFull'] + s['topFull'] + s['z_obrotu']
            cofPaid = labW + labR + topW + topR + turn
            G = s['G']
            m6Full = max(0.0, G - 1.5 * s['Rv']) / 28.0
            m6P = 0.7 * m6Full * (kP if p.m6cut else 1.0); m6R = 0.3 * m6Full * (kR if p.m6cut else 1.0)
            # place z nadwyzki R/P (1/15 nadwyzki ponad zapas x 70%)
            ovR = max(0.0, s['KR'] - RD['R'] * s['NR']); ovP = max(0.0, s['KP'] - RD['P'] * s['NP'])
            wpR = 0.7 * ovR / 15.0; wpP = 0.7 * ovP / 15.0
            hhRW = wpR * kW; hhPW = 0.6 * wpP * kW; hhPR = 0.4 * wpP * kR
            inW += labW + topW + hhRW + hhPW
            inR += labR + topR + m6R + hhPR
            inP += turn + m6P
            # ---- wydatki
            spend = {}
            for c in 'WRP':
                N = s['N' + c]; K = s['K' + c]
                spend[c] = N * min(1.0, max(0.0, K) / N) if N > 0 else 0.0
            offW = 0.8 * max(0.0, s['KW'] - RD['W'] * s['NW']) / 60.0
            offR = 0.8 * 0.3 * ovR / 60.0; offP = 0.8 * 0.3 * ovP / 60.0
            rentW = 0.08 * spend['W']; rentR = 0.13 * spend['R']
            healW = 0.01 * spend['W']; healP = 0.02 * spend['P']; servP = 0.06 * spend['P']
            # podatki od zarobku (zarobek = przyjete)
            taxW, taxR, taxP = TAXE['W'] * inW, TAXE['R'] * inR, TAXE['P'] * inP
            # czesc firmy z oszczednosci (FLf) - 0.37%/d nadwyzki ponad zapas dni (45 tys./d przy fin88)
            flfW = 0.0037 * max(0.0, s['KW'] - RD['W'] * s['NW']); flfR = 0.0037 * ovR; flfP = 0.0037 * ovP
            # danina od skarbca: W - stawka f4 (schodzi z kappa W); R/P - 3% albo f5 (schodzi z kappa R/P)
            rW = p.hoardLo + (0.03 - p.hoardLo) * max(0.0, min(1.0, (kW - 0.1) / 0.9))
            rR = 0.03 if not p.rpHoard else p.hoardLo + (0.03 - p.hoardLo) * max(0.0, min(1.0, (kR - p.mn) / (1 - p.mn)))
            rP = 0.03 if not p.rpHoard else p.hoardLo + (0.03 - p.hoardLo) * max(0.0, min(1.0, (kP - p.mn) / (1 - p.mn)))
            # ulamki: zaplacone juz M9 / FLf / P1 wliczone w danine (pre) - dobrane tak, ze STARE zasady w dobie 0 daja danine z linii 'Akumulator kies' fin88
            # (109235: W 13 708, R 44 054, P 33 675 zl/d); przyblizenie - podstawa w modelu to kiesa ponad 3 x zapas miasta po miescie
            hW = rW * max(0.0, s['KW'] - 30 * s['NW']) * 0.10
            hR = rR * max(0.0, s['KR'] - 63 * s['NR']) * 0.73
            hP = rP * max(0.0, s['KP'] - 90 * s['NP']) * 0.77
            dKW = inW + servP + 0.5 * (taxW * 0.15) - spend['W'] - offW - taxW - flfW - hW - hhRW * 0
            dKW = inW + servP - spend['W'] - offW - taxW - flfW - hW
            dKR = inR + healW + healP - spend['R'] - offR - taxR - flfR - hR - hhRW   # R placi W tylko czesc zaplacona (reszta zostaje w kiesie R)
            dKP = inP + (2.0 / 3.0) * (rentW + rentR) - spend['P'] - offP - taxP - flfP - hP - (hhPW + hhPR)   # sluzba i leczenie P sa w spend (N zawiera /keep)
            # wewnetrzne: czynsz i leczenie juz w spend (N zawiera /keep) - do P/R/pana; dKW/dKR zawieraja spend, wiec czynsz W/R wyplywa w spend
            # kasa: zakupy ludu (towar = spend x keep), oferta, 33% danin, nieprzyjete z zewnatrz, minus place z kasy, M6
            goods = sum(spend[c] * KEEP[c] for c in 'WRP') + offW + offR + offP
            dG = goods + 0.33 * (hW + hR + hP) + cutExt - cofPaid - (m6P + m6R)
            net = goods + 0.33 * (hW + hR + hP) + cutExt - cofPaid          # doplyw netto kupcow (M6 to 'pobrane')
            nets.append(net)
            # podatki od zarobku: pan 1/2/3%, podatek wojenny 184 (s 8.4% x wagi 0.5/1/1.5) - do panow i korony; dziesiecina osobno (kaplani: jalmuzna, septy)
            lords = 0.67 * (hW + hR + hP) + flfW + flfR + flfP + (1.0 / 3.0) * (rentW + rentR) + 0.61 * (s['farm'] + s['farmX']) + 0.01 * inW + 0.02 * inR + 0.03 * inP
            crown = 0.39 * (s['farm'] + s['farmX']) + 0.042 * inW + 0.084 * inR + 0.126 * inP
            if calib is not None:
                # reszta miasta (stale przeplywy spoza modelu) - z kalibracji starych zasad
                cW, cR, cP, cG = calib[i]
                dKW += cW; dKR += cR; dKP += cP; dG += cG
            else:
                s['_m'] = (dKW, dKR, dKP, dG)
            # firma DS: przyrost firmy (farmX) placi kasa
            dG -= s['farmX']
            if day > 0:
                s['KW'] = max(0.0, s['KW'] + dKW); s['KR'] = max(0.0, s['KR'] + dKR); s['KP'] = max(0.0, s['KP'] + dKP); s['G'] = max(0.0, s['G'] + dG)
            agg['KW'] += s['KW']; agg['KR'] += s['KR']; agg['KP'] += s['KP']; agg['NW'] += s['NW']; agg['NR'] += s['NR']; agg['NP'] += s['NP']
            agg['G'] += s['G']; agg['S'] += max(0.0, s['G'] - 1.5 * s['Rv']); agg['m6'] += m6P + m6R
            agg['hW'] += hW; agg['hR'] += hR; agg['hP'] += hP; agg['lords'] += lords; agg['crown'] += crown; agg['farm'] += s['farm'] + s['farmX']
            agg['cutW'] += extW * (1 - kW2); agg['cutR'] += extR * (1 - kR) + 0.1 * s['labFull'] * (1 - kR) + 0.3 * s['topFull'] * (1 - kR) + 0.3 * m6Full * (1 - kR)
            agg['cutP'] += extP * (1 - kP) + s['z_obrotu'] * (1 - kP) + 0.7 * m6Full * (1 - kP)
            agg['net'] += net
            for c in 'WRP':
                if s['N' + c] > 0:
                    agg['d' + c].append(s['K' + c] / s['N' + c])
            agg['kW'].append(kW); agg['kW2'].append(kW2); agg['kR'].append(kR); agg['kP'].append(kP)
        netHist.append(nets)
        # przeglad firm: kwartalny (wszystkie) i szybki w gore (kasa ponad 1.5 x zapas)
        if day > 0:
            for i, s in enumerate(st):
                if p.grow:
                    # jak DsTowns.SurplusReview: pomiar od nowa, gdy kasa nie ponad 1.5 x zapas; przyrost od poczatku okna (najmniej p.fast dob)
                    if s['G'] <= 1.5 * s['Rv']: s['Gh'] = []
                    else: s['Gh'].append(s['G'])
                else:
                    s['Gh'].append(s['G'])
                    if len(s['Gh']) > 29: s['Gh'].pop(0)
                win = [h[i] for h in netHist[-28:]]
                avg = sum(win) / len(win)
                base = netHist[0][i]
                tgt = 0.96 * (avg - base)                 # cel ponad firme z dnia startu modelu (firma = udzial x doplyw netto)
                F = s['farm'] + s['farmX']
                due = day - s['lastRev'] >= p.review
                fastOk = p.fast > 0 and (p.grow or day - s['lastFast'] >= p.fast) and s['G'] > 1.5 * s['Rv']
                surplus = s['G'] > 1.5 * s['Rv']
                poorRP = p.houses and ((s['NR'] > 0 and s['KR'] / s['NR'] < p.lo['R']) or (s['NP'] > 0 and s['KP'] / s['NP'] < p.lo['P']))
                if due:
                    if p.shareUp:
                        nf = max(-p.revMax * F, min(p.revMax * F, s['shK'] * max(0.0, s['farm'] + tgt) - F))   # cel calej firmy = udzial (po podwyzkach) x doplyw
                    else:
                        nf = max(-p.revMax * F, min(p.revMax * F, tgt - s['farmX']))
                    if p.noDown and surplus and nf < 0: nf = 0.0
                    if p.housesQ and poorRP and nf > 0: nf = 0.0     # f5: przeglad zwykly bez podwyzki, dopoki R/P ponizej pasma (nadwyzka najpierw do ich domow przez M6)
                    if nf > 1e-9: agg['qUp'] += 1
                    elif nf < -1e-9:
                        agg['qDown'] += 1
                        if s['raised']: agg['qCutAfter'] += 1
                    s['raised'] = False
                    if p.shareUp and abs(nf) > 1e-9: s['Gh'] = []   # jak DsTowns po recenzji: pomiar przyrostu od nowa po zmianie firmy przegladem zwyklym
                    s['farmX'] += nf; s['lastRev'] = day
                    if not p.grow: s['lastFast'] = day
                if poorRP: s['Gh'] = []                    # jak KsFolkIncome.HousesFirst: pomiar od nowa
                if fastOk and p.grow and not poorRP and len(s['Gh']) > p.fast:
                    g = (s['Gh'][-1] - s['Gh'][0]) / (len(s['Gh']) - 1)
                    s['Gh'] = [s['G']]                     # przyrost liczony od tej doby (bez liczenia drugi raz tych samych dob) - takze bez podwyzki
                    if g > 0:
                        s['farmX'] += min(p.fastMax * F, g); s['lastFast'] = day
                        s['raised'] = True; agg['fUp'] += 1
                        if p.shareUp:
                            tot = s['farm'] + tgt
                            if tot > 0: s['shK'] = max(s['shK'], (s['farm'] + s['farmX']) / tot)
                elif fastOk and not p.grow and not due and tgt > s['farmX']:
                    nf = min(p.fastMax * F, tgt - s['farmX'])
                    s['farmX'] += nf; s['lastFast'] = day
        out.append(agg)
    return out, st


def calibrate(towns):
    p = P()
    _, st = run(towns, p, days=0)
    cal = []
    for s, t in zip(st, towns):
        m = s['_m']
        cal.append((t['dKW'] - m[0], t['dKR'] - m[1], t['dKP'] - m[2], t['dG'] - m[3]))
    return cal


def med(v):
    return statistics.median(v) if v else float('nan')


def show(tag, o, ref=None, at=(0, 30, 60, 91, 120, 160, 182, 273, 364)):
    print(tag)
    for a in o:
        if a['day'] not in at:
            continue
        r = ref[a['day']] if ref else None
        dl = ' (%+4.0f)' % ((a['lords'] - r['lords']) / 1e3) if r else ''
        dc = ' (%+4.0f)' % ((a['crown'] - r['crown']) / 1e3) if r else ''
        print('  d%3d W/R/P %5.1f/%5.1f/%5.1f dni (med %4.1f/%4.1f/%4.1f) kiesy %5.2f/%4.2f/%4.2f mln | kappa med W %.2f W2 %.2f R %.2f P %.2f | kasy %5.2f mln, ponad 1.5x %5.2f (M6 %3.0f) | '
              'danina W/R/P %3.0f/%3.0f/%3.0f | panom %4.0f%s koronie %4.0f%s tys./d | firmy +%3.0f tys./d' % (
                  a['day'], a['KW'] / a['NW'], a['KR'] / a['NR'], a['KP'] / a['NP'], med(a['dW']), med(a['dR']), med(a['dP']), a['KW'] / 1e6, a['KR'] / 1e6, a['KP'] / 1e6,
                  med(a['kW']), med(a['kW2']), med(a['kR']), med(a['kP']), a['G'] / 1e6, a['S'] / 1e6, a['m6'] / 1e3, a['hW'] / 1e3, a['hR'] / 1e3, a['hP'] / 1e3,
                  a['lords'] / 1e3, dl, a['crown'] / 1e3, dc, (a['farm'] - 310.6e3) / 1e3))


def trend(o, a, b, key):
    return (o[b][key] - o[a][key]) / (b - a)


if __name__ == '__main__':
    towns = load()
    cal = calibrate(towns)
    print('miast %d | start: kiesy W/R/P %.2f/%.2f/%.2f mln, kasy %.2f mln; przyrost 28 dob z testu W/R/P %+.0f/%+.0f/%+.0f, kasy %+.0f zl/d' % (
        len(towns), sum(t['KW'] for t in towns) / 1e6, sum(t['KR'] for t in towns) / 1e6, sum(t['KP'] for t in towns) / 1e6, sum(t['G'] for t in towns) / 1e6,
        sum(t['dKW'] for t in towns), sum(t['dKR'] for t in towns), sum(t['dKP'] for t in towns), sum(t['dG'] for t in towns)))
    S0, _ = run(towns, P(), calib=cal)
    show('S0 STARE (f4 jak w fin88, danina R/P 3%):', S0)
    F5 = dict(ext=True, rp=True, rpHoard=True, zero=True, fast=14, grow=True, noDown=True, fastMax=0.25, houses=True)
    sc = [
        ('S1 tylko W2 (caly dochod W wedlug koszyka):', P(ext=True, zero=True)),
        ('S2 W2 + R/P + danina R/P z kappa, BEZ przegladu wedlug nadwyzki (kasa = nowy akumulator):', P(ext=True, rp=True, rpHoard=True, zero=True)),
        ('F5 WDROZONE: S2 + przeglad firmy wedlug nadwyzki kasy co 14 dob (najwyzej +25%, domy najpierw), zwykly bez obnizki przy nadwyzce:', P(**F5)),
        ('F5 + podzial nieprzyjetego miedzy biedne klasy miasta:', P(**dict(F5, share=True))),
        ('F5 + recenzja (uwaga 2): udzial firmy idzie za podwyzka (jedna miara z przegladem kwartalnym):', P(**dict(F5, shareUp=True))),
        ('czulosc: F5 bez "domy najpierw":', P(**dict(F5, houses=False))),
        ('czulosc: F5, przeglad co 28 dob:', P(**dict(F5, fast=28))),
        ('czulosc: F5, najwyzej +15%:', P(**dict(F5, fastMax=0.15))),
        ('czulosc: F5 bez ciecia M6 (P trzyma przejscie):', P(**dict(F5, m6cut=False))),
        ('czulosc: F5, R/P najnizej 0.25:', P(**dict(F5, mn=0.25))),
    ]
    res = {}
    for tag, p in sc:
        o, _ = run(towns, p, calib=cal)
        res[tag] = o
        show(tag, o, S0)
    poorR = {t['id'] for t in towns if t['NR'] > 0 and t['KR'] / t['NR'] < 21}
    poorP = {t['id'] for t in towns if t['NP'] > 0 and t['KP'] / t['NP'] < 30}
    nr = sum(t['NR'] for t in towns if t['id'] in poorR) or 1.0; npp = sum(t['NP'] for t in towns if t['id'] in poorP) or 1.0
    print('biedne R (%d miast ponizej 21 dni na starcie) / biedne P (%d miast ponizej 30 dni) - dni koszyka na starcie %.1f / %.1f:' % (
        len(poorR), len(poorP), sum(t['KR'] for t in towns if t['id'] in poorR) / nr, sum(t['KP'] for t in towns if t['id'] in poorP) / npp))
    for tag, p in [('S0', P())] + sc:
        for dd in (160, 364):
            _, st = run(towns, p, days=dd, calib=cal)
            print('  %-75s d%d: R %5.1f dni, P %5.1f dni' % (tag[:75], dd, sum(x['KR'] for x in st if x['id'] in poorR) / nr, sum(x['KP'] for x in st if x['id'] in poorP) / npp))
    print('przeglady firm (suma 364 dob): podwyzki wedlug nadwyzki / kwartalne w gore / kwartalne w dol / w dol w pierwszym kwartalnym po podwyzce (pila)')
    for tag, o in [('S0', S0)] + list(res.items()):
        print('  %-75s %5d / %4d / %4d / %4d' % (tag[:75], sum(a['fUp'] for a in o), sum(a['qUp'] for a in o), sum(a['qDown'] for a in o), sum(a['qCutAfter'] for a in o)))
    print('trendy ostatnich 28 dob przed d160 i d364 (zl/d): kiesy W, R, P, kasy')
    for tag, o in [('S0', S0)] + list(res.items()):
        print('  %-75s d132-160: W %+7.0f R %+7.0f P %+7.0f kasy %+7.0f | d336-364: W %+7.0f R %+7.0f P %+7.0f kasy %+7.0f' % (
            tag[:75], trend(o, 132, 160, 'KW'), trend(o, 132, 160, 'KR'), trend(o, 132, 160, 'KP'), trend(o, 132, 160, 'G'),
            trend(o, 336, 364, 'KW'), trend(o, 336, 364, 'KR'), trend(o, 336, 364, 'KP'), trend(o, 336, 364, 'G')))
