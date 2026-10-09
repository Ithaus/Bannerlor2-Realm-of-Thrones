"""Tempo doby (174b, krytyka 4 i 16): czas doby z znacznikow czasu logu Armoury - od "Warsztaty: dzien N" do "Warsztaty: dzien N+1".

Uzycie (Python 3, tylko odczyt):
    python -I tools/p174b_doby.py <log 174b> [<log porownawczy, np. Z1b>] [--zapis]
Nowa kampania: srednia i MEDIANA d1-39 i d31-39 (bez pierwszej doby sesji - doba samokontroli pamieci polki), informacyjnie (P7).
Zapis (--zapis): pelne doby (bez pierwszej i ostatniej niepelnej), mediana; z logiem porownawczym (ta sama sesja autotestu, uruchomione kolejno) -
werdykt P8: regres = mediana 174b > mediana porownawcza + 1 s (1 s = rozrzut median Z1/Z1b na tym samym zapisie).
"""
import re
import sys
import statistics as st


def days(path):
    t = {}
    prev, off = None, 0
    with open(path, encoding="utf-8", errors="replace") as f:
        for line in f:
            m = re.match(r"\[(\d\d):(\d\d):(\d\d)\] Warsztaty: dzien (\d+)", line)
            if not m:
                continue
            s = int(m.group(1)) * 3600 + int(m.group(2)) * 60 + int(m.group(3))
            if prev is not None and s + off < prev - 3600:
                off += 86400   # polnoc zegara
            s += off
            prev = s
            t.setdefault(int(m.group(4)), s)
    ks = sorted(t)
    return [t[ks[i + 1]] - t[ks[i]] for i in range(len(ks) - 1) if ks[i + 1] == ks[i] + 1]


def show(name, d, zapis):
    if not d:
        print("%s: brak dob" % name)
        return None
    d = d[1:]   # bez pierwszej doby sesji (samokontrola pamieci polki 1/64; krytyka 4)
    if zapis:
        print("%s: %d pelnych dob %s | srednio %.1f, mediana %.1f s/dobe" % (name, len(d), d, st.mean(d), st.median(d)))
        return st.median(d)
    a, b = d[:39], d[30:39]
    print("%s: d2-40 srednio %.2f, mediana %.1f | d31-39 srednio %s, mediana %s s/dobe (n=%d)"
          % (name, st.mean(a), st.median(a), ("%.2f" % st.mean(b)) if b else "-", ("%.1f" % st.median(b)) if b else "-", len(d)))
    return st.median(a)


def main():
    a = [x for x in sys.argv[1:] if not x.startswith("--")]
    zapis = "--zapis" in sys.argv
    if not a:
        print(__doc__)
        return
    m1 = show(a[0].replace("\\", "/").split("/")[-1], days(a[0]), zapis)
    if len(a) > 1:
        m2 = show(a[1].replace("\\", "/").split("/")[-1], days(a[1]), zapis)
        if m1 is not None and m2 is not None:
            if zapis:
                print("P8: mediana %.1f wobec %.1f + 1 s -> %s" % (m1, m2, "TAK" if m1 <= m2 + 1.0 else "NIE (regres - nastepny krok wedlug linii 'Koszt 171-174')"))
            else:
                print("P7 (informacyjnie): mediana %.1f wobec %.1f (roznica %+.1f s/dobe)" % (m1, m2, m1 - m2))


if __name__ == "__main__":
    main()
