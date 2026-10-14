# czynnik P (sqrt ludzi wzgledem sredniej rodzaju, 0.9-1.4) wedlug krolestw d1; suma ludzi swiata; liczba krolestw d1
import csv, math, statistics as st
TAB = r"D:\Backup-Bannerlord\logs\2026-10-14-noc\tabele-06-01-15"
rows = []
with open(TAB + r"\ludzie-regiony.csv", encoding="utf-8") as f:
    rd = csv.DictReader(f, delimiter=";")
    for r in rd:
        if r["dzien"] != "108836": break
        rows.append(r)
mean = {k: st.mean(float(r["ludzie"]) for r in rows if r["rodzaj"] == k) for k in ("miasto", "zamek")}
by = {}
for r in rows:
    c = float(r["ludzie"]) / mean[r["rodzaj"]]
    p = min(1.4, max(0.9, math.sqrt(c)))
    by.setdefault(r["krolestwo"], []).append((r["rodzaj"], c, p, float(r["zaloga"]), float(r["milicja"])))
print("srednia ludzi regionu: miasto %d, zamek %d; ludzie swiata %d; krolestw %d" % (mean["miasto"], mean["zamek"], sum(float(r["ludzie"]) for r in rows), len(by)))
for k, v in sorted(by.items(), key=lambda kv: -len(kv[1])):
    n4 = sum((800 if x[0] == "miasto" else 400) * x[2] for x in v)
    print("%-36s twierdz %2d  c med %.2f  P med %.2f  norma 400/800 razem %5d  zaloga d1 %5d  milicja* d1 %5d" % (k, len(v), st.median(x[1] for x in v), st.median(x[2] for x in v), n4, sum(x[3] for x in v), sum(x[4] for x in v)))
