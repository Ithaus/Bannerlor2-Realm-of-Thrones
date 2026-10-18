"""r5core (r4-kupcy, H8 cz. 2): niezalezny port Pythona rdzenia decyzji KS-H (KsFoodCore.Decide) z flagami rundy 4 WLACZONYMI (FarAlways, RelKeep, ByNeed,
Famine, Consign, DestValue z j0) - te same 9 stanow port4 (snap_*.json) i te same funkcje rynku co uprzaz port5 (Program.cs, tryb "on"). Wynik: lista lotow
"doba zrodlo cel towar sztuki koszt fracht" + liczniki doby; porownanie z wyjsciem C# (port5 tryb on) linia w linie.

Uzycie: python -I r5core.py <katalog snap> <wyjscie.txt> [--tight] [--porownaj <wyjscie C# port5 on / ontight>]
<katalog snap> = D:/Backup-Bannerlord/scratch-2026-10-17/kmoney/port4/snap. Wynik recenzji r4-kupcy (noc 17/18.10): on 2191 z 2191 lotow zgodnych, --tight 1799 z 1799 linii
(1790 lotow + 9 linii ovd: cel z zaleglym i zrodlo bez miejsca) - rdzen C# i port Pythona licza to samo.
"""
import glob
import json
import math
import os
import struct
import sys

TAU_LAND, TAU_SEA, MARGIN = 0.025, 0.0025, 0.03
SRC_KEEP, KX_UP, KX_DOWN, KX_MAX = 1.25, 1.10, 0.98, 2.5
MAX_DUR, MAX_PER = 30.0, 7.0
MAX_SRC, KEEP = 3, 5
N_FAR_ALWAYS = 8
REL_MIN, REL_MED, REL_STAPLE, REL_SHARE, FAM_COVER = 0.5, 1.1, 1.0, 0.67, 0.5


def f32(x):
    return struct.unpack("f", struct.pack("f", x))[0]


def cs_round(x):
    # Math.Round (MidpointRounding.ToEven) - Python round() tak samo dla float
    return int(round(x))


def flat(a):
    out = []
    for r in a:
        for x in r:
            out.append(1.0 if x is True else (0.0 if x is False else float(x)))
    return out


def dest_integral(p0, A0, c, cap, n):
    if n <= 0 or not (p0 > 0):
        return 0.0
    if not (A0 > 0) or not (c > 0):
        return n * (min(p0, cap) if cap > 0 else p0)
    s, j0 = 0.0, 0.0
    if cap > 0 and p0 > cap:
        js = A0 * (math.pow(p0 / cap, 1.0 / 0.6) - 1.0) / c
        if js >= n:
            return n * cap
        s += cap * js
        j0 = js
    s += p0 * math.pow(A0, 0.6) / (0.4 * c) * (math.pow(A0 + c * n, 0.4) - math.pow(A0 + c * j0, 0.4))
    return max(s, float(n))


def dest_integral_from(p0, A0, c, cap, j0, n):
    if j0 <= 0:
        return dest_integral(p0, A0, c, cap, n)
    if n <= 0 or not (p0 > 0):
        return 0.0
    if not (A0 > 0) or not (c > 0):
        return n * (min(p0, cap) if cap > 0 else p0)
    ja, jb = float(j0), float(j0) + n
    lo, s = ja, 0.0
    if cap > 0 and p0 > cap:
        js = A0 * (math.pow(p0 / cap, 1.0 / 0.6) - 1.0) / c
        if js >= jb:
            return n * cap
        if js > ja:
            s += cap * (js - ja)
            lo = js
    s += p0 * math.pow(A0, 0.6) / (0.4 * c) * (math.pow(A0 + c * jb, 0.4) - math.pow(A0 + c * lo, 0.4))
    return max(s, float(n))


def run_day(j, tight=False):
    nt, nk, day = j["nt"], j["nk"], j["day"]
    mk = j["mk"]
    review = j["review"]
    siege = [False] * nt
    sh = flat(j["sh"])
    shf = flat(j["shf"])
    inc = flat(j["inc"])
    ix = flat(j["ix"])
    dp = flat(j["dp"])
    off = flat(j["off"])
    kx = [f32(x) for x in flat(j["kx"])]
    ts = flat(j["ts"])
    per = j["per"]
    staple = j["staple"]
    gold = [float(x) for x in j["gold"]]
    cand = j["cand"]
    candfar = j["candfar"]
    dayT = flat(j["days"])
    sea = flat(j["sea"])
    war = flat(j["war"])
    val = [float(x) for x in j["val"]]
    cat = j["cat"]
    catD = flat(j["catD"])
    catS = flat(j["catS"])
    catX = flat(j["catV0x"])
    nc = len(catD) // nt
    startIdx = flat(j["startidx"])
    c0 = float(j["c0"])

    def days(s, t):
        return f32(dayT[s * nt + t])

    def index(D, S, V):
        den = 0.1 * S + 0.04 * V + c0
        x = math.pow(D / den, 0.6) if D > 0 else 0.0
        return 0.1 if x < 0.1 else (10.0 if x > 10.0 else x)

    def v0(s, c):
        v = catX[s * nc + c]
        for k in range(nk):
            if cat[k] == c:
                v += max(0.0, shf[s * nk + k]) * val[k]
        return v

    def lot_price(s, k, n):
        c = cat[k]
        V0 = v0(s, c)
        D = catD[s * nc + c]
        S = catS[s * nc + c]
        v = val[k]

        def P(m):
            return max(1, int(math.ceil(v * index(D, S, max(0.0, V0 - m * v)) - 1e-9)))
        if n < 8:
            return sum(P(m) for m in range(n))
        m1, m2 = n // 8, n // 2
        return m1 * P(0) + (m2 - m1) * P(m1) + (n - m2) * P(m2)

    def dest_unit(t, k):
        up = val[k] * ix[t * nk + k]
        if shf[t * nk + k] <= 5:
            up = min(up, 1.5 * startIdx[mk[t] * nk + k] * val[k])
        return max(1, int(math.ceil(up - 1e-9)))

    def dest_value(t, k, j0, n):
        c = cat[k]
        V0 = v0(t, c)
        D = catD[t * nc + c]
        S = catS[t * nc + c]
        v = val[k]
        p0 = v * index(D, S, V0)
        A0 = 0.1 * S + 0.04 * V0 + c0
        cc = 0.04 * v
        cap = 3.0 * startIdx[mk[t] * nk + k] * v
        return int(math.floor(dest_integral_from(p0, A0, cc, cap, j0, n) * (1.0 - MARGIN)))

    lots = []

    def ship(s, t, k, n, cost, fr):
        if gold[s] < fr:
            return 0
        take = min(n, int(max(0.0, shf[s * nk + k])) - 5)
        if take <= 0:
            return 0
        if take < n:
            cost = cs_round(cost * float(take) / n)
            fr = cs_round(fr * float(take) / n)
            n = take
        shf[s * nk + k] -= n
        gold[s] -= fr
        lots.append((s, t, k, n, cost, fr))
        return n

    src_room = [max(0.0, (0.02 if tight else 0.5) * g) for g in gold]
    overdue = [tight and (i % 7 == 3) for i in range(nt)]
    fam_good = [k < min(4, nk) for k in range(nk)]
    st = {"ord": 0, "nop": 0, "far": 0, "src": 0, "ovd": 0, "famreq": 0, "famreqk": 0, "famord": 0, "famgot": 0}

    # --- Decide: zamowienie, kx
    dz = [0.0] * (nt * nk)
    dzB = [0.0] * (nt * nk)
    sh2 = list(sh)
    for i in range(nt):
        lmin = max(1.0, days(cand[i][0], i)) if cand[i] else 1.0
        for k in range(nk):
            o = i * nk + k
            p = dp[o]
            b = max(p, off[o])
            T = ts[mk[i] * nk + k]
            kxv = kx[o]
            H = sh2[o] + inc[o]
            if p > 0 and sh2[o] <= KEEP + 1 and H < (T + lmin) * b:
                kxv = min(KX_MAX, kxv * KX_UP)
            elif not (b > 0 and inc[o] >= lmin * b):
                kxv = max(1.0, kxv * KX_DOWN)
            kx[o] = f32(kxv)
            dzB[o] = b
            dz[o] = b * kxv
    # --- oferta H3 (zapas wzgledny)
    offer = [0.0] * (nt * nk)
    med = [0.0] * (2 * nk)
    for m in range(2):
        for k in range(nk):
            tmp = []
            for i in range(nt):
                if (1 if mk[i] == 1 else 0) != m:
                    continue
                o = i * nk + k
                if dzB[o] > 0:
                    tmp.append(sh2[o] / dzB[o])
            if not tmp:
                med[m * nk + k] = float("inf")
                continue
            tmp.sort()
            med[m * nk + k] = tmp[(len(tmp) - 1) // 2]
    for i in range(nt):
        for k in range(nk):
            if siege[i]:
                continue
            o = i * nk + k
            T = ts[mk[i] * nk + k]
            keepT = max(REL_MIN * T, min(SRC_KEEP * T, REL_MED * med[(1 if mk[i] == 1 else 0) * nk + k]))
            sur = sh2[o] - keepT * dzB[o] - KEEP
            if sur > 1:
                offer[o] = math.floor((REL_STAPLE if staple[k] else REL_SHARE) * sur)

    src_spent = [0.0] * nt
    far_mark = [False] * nt
    fam_seen = [False] * nt

    def add_cand(cl, s, t, k, lim):
        if offer[s * nk + k] < 1 or siege[s]:
            return
        L = days(s, t)
        if not (L >= 0.0) or L > lim:
            return
        if war[s * nt + t] > 0.5:
            return
        tau = TAU_SEA if sea[s * nt + t] > 0.5 else TAU_LAND
        c = ix[s * nk + k] * (1.0 + tau * L)
        if ix[t * nk + k] < c * (1.0 + MARGIN):
            return
        cl.append((c, s))

    def serve(t, k, fam):
        lim = MAX_PER if per[k] else MAX_DUR
        d = dz[t * nk + k]
        T = ts[mk[t] * nk + k]
        Hc = sh2[t * nk + k] + inc[t * nk + k]
        cl = []
        for s in cand[t]:
            add_cand(cl, s, t, k, lim)
        farN = 0
        for s in candfar[t]:
            if farN >= N_FAR_ALWAYS:
                break
            if offer[s * nk + k] < 1:
                continue
            L0 = days(s, t)
            if not (L0 >= 0.0):
                continue
            if L0 > lim:
                break
            before = len(cl)
            add_cand(cl, s, t, k, lim)
            if len(cl) > before:
                farN += 1
                far_mark[s] = True
        if fam:
            st["famreqk"] += 1
            if not fam_seen[t]:
                fam_seen[t] = True
                st["famreq"] += 1

        def clear_far():
            for _, s in cl:
                far_mark[s] = False
        if not cl:
            clear_far()
            return
        cl.sort()
        if overdue[t]:
            # H5: cel z rachunkiem starszym niz zwloka - bez dostaw; pierwszy lot, jaki by poszedl = "cel z zaleglym"
            for (_, s) in cl:
                L = days(s, t)
                q = min(offer[s * nk + k], (T + L + 2) * d - Hc)
                if q < max(5.0, 0.5 * d):
                    continue
                n0 = int(math.floor(q))
                st["ord"] += n0
                st["ovd"] += n0
                if fam:
                    st["famord"] += n0
                break
            clear_far()
            return
        dunit = dest_unit(t, k)
        used = 0
        ord0, got0 = -1, 0
        src_hit = False
        for (_, s) in cl:
            if used >= MAX_SRC:
                break
            L = days(s, t)
            want = (T + L + 2) * d - Hc
            q = min(offer[s * nk + k], want)
            if q < max(5.0, 0.5 * d):
                continue
            n = int(math.floor(q))
            if ord0 < 0:
                ord0 = n
            is_sea = sea[s * nt + t] > 0.5
            tau = TAU_SEA if is_sea else TAU_LAND
            cost = lot_price(s, k, n)
            fr = cs_round(cost * tau * L)
            j0 = got0
            if dunit > 0:
                tries = 0
                while n > 0 and cost + fr > dest_value(t, k, j0, n) and tries < 3:
                    n //= 2
                    tries += 1
                    if n < max(5.0, 0.5 * d):
                        n = 0
                        break
                    cost = lot_price(s, k, n)
                    fr = cs_round(cost * tau * L)
                if n <= 0 or cost + fr > dest_value(t, k, j0, n):
                    st["nop"] += 1
                    continue
            avail = src_room[s] - src_spent[s]
            if cost + fr > avail:
                nf = int(math.floor(n * avail / float(cost + fr))) if (cost + fr > 0 and avail > 0) else 0
                src_hit = True
                if nf < max(5.0, 0.5 * d):
                    continue
                n = nf
                cost = lot_price(s, k, n)
                fr = cs_round(cost * tau * L)
                if cost + fr > avail + 1:
                    continue
            if n <= 0 or cost <= 0:
                continue
            got = ship(s, t, k, n, cost, fr)
            if got <= 0:
                continue
            got0 += got
            used += 1
            if far_mark[s]:
                st["far"] += 1
            offer[s * nk + k] -= got
            sh2[s * nk + k] -= got
            Hc += got
            src_spent[s] += cost + fr
        if ord0 > 0:
            st["ord"] += ord0
            miss = max(0, ord0 - got0)
            if src_hit:
                st["src"] += miss
            if fam:
                st["famord"] += ord0
                st["famgot"] += got0
        clear_far()

    req = []
    for k in range(nk):
        lim = MAX_PER if per[k] else MAX_DUR
        for i in range(nt):
            if siege[i] or not review[i]:
                continue
            d = dz[i * nk + k]
            if not (d > 0):
                continue
            T = ts[mk[i] * nk + k]
            H = sh2[i * nk + k] + inc[i * nk + k]
            if H >= (T + lim + 2) * d:
                continue
            cov = H / max(1e-9, T * d)
            fam = fam_good[k] and H < FAM_COVER * T * d
            req.append((not fam, cov, -ix[i * nk + k], i, k, fam))
    req.sort()
    for (_, _, _, t, k, fam) in req:
        serve(t, k, fam)
    return day, lots, st


def main(argv):
    snap, outp = argv[1], argv[2]
    cmp_path = argv[argv.index("--porownaj") + 1] if "--porownaj" in argv else None
    tight = "--tight" in argv
    files = sorted(glob.glob(os.path.join(snap, "snap_*.json")))
    lines = []
    tot = {"lots": 0, "pcs": 0}
    agg = {}
    for f in files:
        j = json.load(open(f, encoding="utf-8"))
        day, lots, st = run_day(j, tight)
        for (s, t, k, n, cost, fr) in lots:
            lines.append(f"{day} {s} {t} {k} {n} {cost} {fr}")
            tot["pcs"] += n
        if tight:
            lines.append(f"ovd {day} {st['ovd']} {st['src']}")
        tot["lots"] += len(lots)
        for k2, v in st.items():
            agg[k2] = agg.get(k2, 0) + v
    open(outp, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print(f"r5core (Python, flagi rundy 4 wl.{' , ciasny kredyt 0.02 x kasa i co 7. cel z zaleglym' if tight else ''}): dob {len(files)}, lotow {tot['lots']}, szt. {tot['pcs']}, zamowione {agg.get('ord')}, bez zysku {agg.get('nop')}, "
          f"z dalszych {agg.get('far')}, zrodlo bez miejsca {agg.get('src')}, cel z zaleglym {agg.get('ovd')}, glod celow {agg.get('famreq')} (zamowien {agg.get('famreqk')}) zam. {agg.get('famord')} wysl. {agg.get('famgot')}")
    if cmp_path:
        cs = [ln.strip() for ln in open(cmp_path, encoding="utf-8") if ln.strip() and not ln.startswith("stats ")]
        py = lines
        same = sum(1 for a, b in zip(cs, py) if a == b)
        first = next((i for i, (a, b) in enumerate(zip(cs, py)) if a != b), None)
        print(f"porownanie z C#: linii C# {len(cs)}, Python {len(py)}, zgodnych linii {same}" + ("" if first is None else f", pierwsza roznica linia {first + 1}: C# '{cs[first]}' / Py '{py[first]}'"))
        return 0 if (len(cs) == len(py) and same == len(cs)) else 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
