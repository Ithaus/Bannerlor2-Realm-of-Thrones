# -*- coding: utf-8 -*-
"""skoki-bee.py - pomiar "znikajacych" wyplat BetterEconomy w pliku CrashScribe economy-*.csv (tylko odczyt).

Uzycie:  python skoki-bee.py <economy-*.csv> [<economy-*.csv> ...]
         (pliki: Dokumenty\\Mount and Blade II Bannerlord\\CrashScribe\\economy-<data>_<godzina>.csv)

Metoda (ta sama co weryfikator fundamentu, SCR/fundament-weryf-better-economy/w1.py):
  reszta = dobowa zmiana kiesy glowy rodu AI - bilans_model tego dnia; zdarzenie = reszta w oknie wokol kwoty BEE:
    12 tys.         wplata AI do skarbca zamku    (CastleEconomyCampaignBehavior.cs:351-358)
    30/60/100 tys.  oboz szkoleniowy AI           (CastleEconomyCampaignBehavior.cs:1069-1074); 42 = 30 + 12 tego samego dnia
    5/15/30 tys.    inwestycja pana we wies       (VillageInvestmentCampaignBehavior.cs:311-375)
    5 tys.          oplata AI za dostep do targu  (VillageDevelopmentCampaignBehavior.cs:599-625), od ok. 21. doby kampanii
  Kwoty 5 i 30 tys. maja po dwa zrodla - skrypt ich nie rozdziela.
  To jest SZACUNEK po kwotach: reszta zawiera tez inne wydatki rodu poza modelem (zakupy sprzetu, werbunek), wiec kazde
  okno ma tlo. Miara tla: zdarzenia u rodow, ktorych dane ujscie BEE dotknac nie moze - dla okien zamkowych (12k, 42k
  i zlozenia) rody BEZ zamku, dla okien wiejskich (5k, 15k, 30k) rody BEZ wsi. Kolumny "u wlascicieli" i "tlo".
  Pewny pomiar u zrodla: VerboseLogging=1 w Modules\\BetterEconomy\\better_economy_user.cfg (plik bee_verbose_log.txt,
  linie castle-ai-contribute, castle-training-ai-build-start, lord-village-invest, village-market-access).
  Pozycja "GiveGoldAction w nicosc" w linii "Pieniadz swiata (bilans):" logu Armoury (wpis 102) NIE jest pomiarem samego
  BEE: trafiaja do niej takze ujemne dobowe bilanse rodow z modelu finansow (ClanVariablesCampaignBehavior.cs:414),
  werbunek i awanse wojska - porownywac nia wolno tylko te same doby tej samej kampanii (OPIS.md, rozdz. 5.2).
"""
import collections
import csv
import io
import sys

OKNA = [  # (etykieta, dol, gora, kwota nominalna, czy okno zamkowe - inaczej wiejskie)
    ("5k", 4300, 5700, 5000, False),
    ("12k", 11000, 13000, 12000, True),
    ("15k", 14200, 15800, 15000, False),
    ("17k (12+5)", 16300, 17700, 17000, True),
    ("27k (12+15)", 26300, 27700, 27000, True),
    ("30k", 29200, 30800, 30000, False),
    ("42k (30+12)", 41000, 43500, 42000, True),
    ("47k (42+5)", 46300, 47800, 47000, True),
    ("57k (42+15)", 56300, 57800, 57000, True),
    ("60k", 59000, 61000, 60000, True),
    ("72k (60+12)", 71000, 73500, 72000, True),
    ("100k", 98500, 101500, 100000, True),
    ("112k (100+12)", 110500, 113500, 112000, True),
]


def num(s):
    try:
        return float(s.replace(",", "."))
    except Exception:
        return None


def analiza(sciezka):
    rows = list(csv.DictReader(io.open(sciezka, encoding="utf-8-sig"), delimiter=";"))
    if not rows:
        print("PUSTY PLIK:", sciezka)
        return
    dni = sorted({int(r["dzien"]) for r in rows})
    d0 = dni[0]
    rody = collections.defaultdict(dict)
    for r in rows:
        rody[r["rod"]][int(r["dzien"])] = r
    zdarzenia = []      # (doba wzgledna, reszta, zamki, wioski)
    spadki = 0.0
    for rod, d in rody.items():
        ds = sorted(d)
        if any(d[x]["kategoria"] == "gracz" for x in ds):
            continue
        for a, b in zip(ds, ds[1:]):
            if b - a != 1:
                continue
            ga, gb = num(d[a]["zloto_glowy"]), num(d[b]["zloto_glowy"])
            if ga is None or gb is None:
                continue
            zm = gb - ga
            if zm < 0:
                spadki += -zm
            reszta = zm - (num(d[b]["bilans_model"]) or 0)
            zdarzenia.append((b - d0, reszta, int(d[b]["zamki"] or 0), int(d[b]["wioski"] or 0)))
    pierwszy = [r for r in rows if int(r["dzien"]) == dni[0] and r["kategoria"] != "gracz"]
    ostatni = [r for r in rows if int(r["dzien"]) == dni[-1] and r["kategoria"] != "gracz"]
    dl = max(len(dni) - 1, 1)
    print("=== %s" % sciezka)
    print("    dni %d-%d (%d dob), rodow AI %d, zamkow %d, wsi %d" % (
        dni[0], dni[-1], len(dni) - 1, len(ostatni),
        sum(int(r["zamki"] or 0) for r in ostatni), sum(int(r["wioski"] or 0) for r in ostatni)))
    g0 = sum(num(r["zloto_glowy"]) or 0 for r in pierwszy)
    g1 = sum(num(r["zloto_glowy"]) or 0 for r in ostatni)
    print("    kiesy glow rodow AI: %.0f -> %.0f (zmiana %+.0f, %+.0f na dobe); suma dobowych spadkow %.0f" % (
        g0, g1, g1 - g0, (g1 - g0) / float(dl), spadki))
    razem = 0
    suma = {True: [0, 0], False: [0, 0]}      # zamkowe / wiejskie: [u wlascicieli, tlo]
    z_zamkiem = sum(1 for r in ostatni if int(r["zamki"] or 0) >= 1)
    ze_wsia = sum(1 for r in ostatni if int(r["wioski"] or 0) >= 1)
    print("    rody AI z zamkiem %d (bez %d), ze wsia %d (bez %d) - stan z ostatniej doby" % (
        z_zamkiem, len(ostatni) - z_zamkiem, ze_wsia, len(ostatni) - ze_wsia))
    print("    %-14s %-8s %7s %13s %5s %10s  %s" % ("okno", "rodzaj", "zdarzen", "u wlascicieli", "tlo", "nominalnie",
                                                   "wg doby (1..%d)" % dl))
    for et, lo, hi, kw, zamkowe in OKNA:
        es = [e for e in zdarzenia if lo <= -e[1] <= hi]
        u = sum(1 for e in es if (e[2] if zamkowe else e[3]) >= 1)
        wgdnia = collections.Counter(e[0] for e in es)
        razem += kw * len(es)
        suma[zamkowe][0] += u
        suma[zamkowe][1] += len(es) - u
        print("    %-14s %-8s %7d %13d %5d %10d  %s" % (et, "zamkowe" if zamkowe else "wiejskie", len(es), u, len(es) - u,
                                                       kw * len(es),
                                                       " ".join(str(wgdnia.get(i, 0)) for i in range(1, dl + 1))))
    print("    RAZEM nominalnie %d = %.0f%% sumy spadkow; srednio %.0f na dobe" % (
        razem, 100.0 * razem / spadki if spadki else 0.0, razem / float(dl)))
    print("    Okna zamkowe: %d zdarzen u rodow z zamkiem, %d u rodow bez zamku (tlo). "
          "Okna wiejskie: %d u rodow ze wsia, %d u rodow bez wsi (tlo)." % (
              suma[True][0], suma[True][1], suma[False][0], suma[False][1]))
    print("    Po zamknieciu ujsc oczekiwane: zdarzenia \"u wlascicieli\" spadaja do poziomu tla "
          "(tlo przeliczyc proporcja liczby rodow).")


if __name__ == "__main__":
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    for p in sys.argv[1:]:
        analiza(p)
