# F1: rampa 28 dob, obieg przez dzisiejsze pobory miast jako opoznienie 1 rzedu (tau 14 i 28 dob)
import math
cases = {'ks': (64643, 82574, 0.60, 680028, 18095), 'base': (82558, 112290, 0.58, 646438, 26585), 'jeff': (97722, 140002, 0.57, 649037, 34970)}
for tau in (14.0, 28.0):
    for k, (stat, totown, sc, inc, lst) in cases.items():
        y = 0.0; worst = (0, 0); res = {}
        for t in range(1, 181):
            r = min(1.0, t / 28.0)
            x = r * totown
            y += (x - y) / tau
            dc = -r * stat + sc * 0.98 * y
            dl = -r * lst + (1 - sc) * 0.98 * y
            if dc < worst[0]: worst = (dc, t)
            if t in (28, 56, 90, 180): res[t] = (round(dc), round(dl))
        print('tau %2.0f %-5s najgorzej korona %+.0f (%.1f%%) w dobie %d | doba: (korona, panowie) %s' % (tau, k, worst[0], 100 * worst[0] / inc, worst[1], res))
