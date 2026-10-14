# Szacunek OB1 (SiegeWalls, 14.10) na 113 decyzjach AI o szturmie z biegu fin30. Wejscie: linie "Okup oblezenia (O0)" wyciagniete grepem z
# D:/Backup-Bannerlord/logs/2026-10-14-noc/Armoury-2026-10-14_06-01-15.log oraz docs/oblezenia/robocze/mody/fin30-decyzje-szturmu-O0-wynik.csv.
# Argumenty 3-4: mnoznik stolicy i zalozony mnoznik machin obroncow (log fin30 ich nie ma). A) weto w chwili decyzji fin30 (HP murow 1, machiny z logu),
# B) po taranie i wiezy (w 0.8) - nadal <= 1 = szturm dopiero po rozbiciu murow. Na koncu czas budowy przy tempie OB4 (x0.125 gry / klasa) i doby do murow 0
# (trebusze RBM 1200 x 5 strzalow = 6000 na dobe kazdy, budowane po kolei, bez machin obroncow). Uruchamiac: python -I.
import re, sys, csv, statistics as st
# szacunek OB1 (mury w pelni, w = 0.8 x HP x 1.25 bez taranu x 1.25 bez wiezy) na 113 decyzjach O0 fin30
o0p, wyp = sys.argv[1], sys.argv[2]
CAP = float(sys.argv[3]) if len(sys.argv) > 3 else 1.25
DEN = float(sys.argv[4]) if len(sys.argv) > 4 else 1.0
R = re.compile(r"dzien (\d+) \| osada (\S+) \((.*?), (miasto|zamek),.*?druzyn (\d+) \(ludzi (\d+).*?oblezenie ([\d.]+) dob.*?zold armii (\d+)/d.*?stosunek gry (\S+) \(oblegajacy (\d+) / obroncy (\d+) x przewaga ([\d.]+)\^(\S+); maszyny (\d+)(, taran)?(, wieza)?.*?\| mury (\S+) \(bez czlonu czasu: obroncy (\d+) .*?stolica (tak|nie)")
out = {}
for r in csv.DictReader(open(wyp, encoding="utf-8"), delimiter=";"):
    out[(r["dzien"], r["osada"])] = r["wynik_w_10_dobach"]
rows = []
for ln in open(o0p, encoding="utf-8", errors="replace"):
    m = R.search(ln)
    if not m:
        print("BRAK", ln[:120]); continue
    d, sid, nm, kind, parties, men, age, wage, sg, att, de, adv, w, n, ram, tow, mury, def0, cap = m.groups()
    att, def0, adv, n = float(att), float(def0), float(adv), int(n)
    ram, tow = ram is not None, tow is not None
    nr = max(0, n - (1 if ram else 0) - (1 if tow else 0))
    div = 1 + (0.25 if (ram or tow) else 0) + (0.16 if ram else 0) + (0.16 if tow else 0) + 0.08 * nr
    base = adv * div
    L = min(3, max(1, round(base - 4)))
    capf = (CAP if cap == "tak" else 1.0) * DEN
    w0 = 0.8 * (1 if ram else 1.25) * (1 if tow else 1.25)
    if def0 <= 0:
        s_now = s_rt = 999.0
    else:
        s_now = att / (def0 * (adv * capf) ** w0)
        adv_rt = (4 + L) / (1 + 0.25 + 0.16 + 0.16 + 0.08 * nr) * capf
        s_rt = att / (def0 * adv_rt ** 0.8)
    rows.append(dict(d=d, sid=sid, nm=nm, kind=kind, cap=cap, att=att, def0=def0, adv=adv, L=L, ram=ram, tow=tow, nr=nr, w0=w0,
                     s_now=s_now, s_rt=s_rt, mury=float(mury) if mury[0].isdigit() else 999.0, wynik=out.get((d, sid), "?"), age=float(age), men=int(men)))
print("decyzji", len(rows), "| CAP", CAP)
print("poziom murow (wywnioskowany):", {l: sum(1 for r in rows if r["L"] == l) for l in (1, 2, 3)})
print("taran", sum(r["ram"] for r in rows), "wieza", sum(r["tow"] for r in rows), "dystansowe (sr.)", round(st.mean(r["nr"] for r in rows), 2))
veto_now = [r for r in rows if r["s_now"] <= 1]
veto_rt = [r for r in rows if r["s_rt"] <= 1]
print("A) OB1 w chwili decyzji fin30 (HP 1, machiny jak w logu): wstrzymane", len(veto_now), "z", len(rows))
print("   w tym stolice", sum(1 for r in veto_now if r["cap"] == "tak"), "z", sum(1 for r in rows if r["cap"] == "tak"))
print("   wedlug wyniku fin30:", {k: sum(1 for r in veto_now if r["wynik"] == k) for k in sorted(set(r["wynik"] for r in rows))})
print("B) po taranie i wiezy (w 0.8, HP 1): nadal <= 1 (szturm dopiero po rozbiciu murow)", len(veto_rt))
print("   wedlug wyniku fin30:", {k: sum(1 for r in veto_rt if r["wynik"] == k) for k in sorted(set(r["wynik"] for r in rows))})
print("   miasta / zamki", sum(1 for r in veto_rt if r["kind"] == "miasto"), "/", sum(1 for r in veto_rt if r["kind"] == "zamek"), "; stolice", sum(1 for r in veto_rt if r["cap"] == "tak"))
print("   s_rt tych: ", sorted(round(r["s_rt"], 2) for r in veto_rt))
print("   ludzie obozu tych (mediana)", st.median([r["men"] for r in veto_rt]) if veto_rt else "-")
for r in rows:
    if r["cap"] == "tak":
        print("   stolica", r["nm"], r["kind"], "L", r["L"], "s_now %.2f s_rt %.2f mury_fin30 %.2f wynik %s" % (r["s_now"], r["s_rt"], r["mury"], r["wynik"]))
# kolejnosc i czas budowy (osobodni RBM: przygotowania 24, taran 12, wieza 16, trebusz 40) przy tempie OB4
def days(md, men, mult):
    return md * 24.0 / (men ** 0.5) / mult / 24.0
for men in (1350, 2352, 4589):
    for kind, f in (("zamek", 1.0), ("miasto", 1.5), ("stolica", 2.5)):
        mult = 0.25 * 0.5 / f
        prep = days(24, men, mult); rt = days(28, men, mult); tr = days(40, men, mult)
        # mury: trebusze po kolei, 6000/d kazdy (1200 x 5 strzalow, trafienie 1.0, RBM); bez machin obroncow
        out2 = []
        for L, hp in ((1, 30000), (2, 50000), (3, 67000)):
            t = prep + rt; left = float(hp); dt = 0.01
            while left > 0 and t < 400:
                k = min(4, int((t - prep - rt) / tr))
                left -= 6000.0 * k * dt
                t += dt
            out2.append("L%d %.1f" % (L, t))
        print("ludzi %d %s: przygotowania %.1f d, taran+wieza gotowe %.1f d, trebusz co %.1f d, mury 0 przy: %s" % (men, kind, prep, prep + rt, tr, ", ".join(out2)))
