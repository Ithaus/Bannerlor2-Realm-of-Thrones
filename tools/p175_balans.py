# p175_balans.py - analiza testu paczki 175 (ARMIE KROLESTW): balans krolestw, konie AI, budzety rodow; progi z projektu
# docs/PROJEKT-175-ARMIE-2026-10-09.md rozdz. 6.2 (z przedzialem ufnosci i jawnym "brak danych"). Tylko odczyt.
#
# Uzycie (python -I, katalogi biegow autotestu - plik Armoury-*.log i katalog sesji z bitwy.log, *.csv, gdziekolwiek ponizej):
#   python -I tools/p175_balans.py <bieg> [--baza <bieg B0>] [--okna 40 120] [--zmienione battania,khuzait,sturgia,pentos,qarth]
#   python -I tools/p175_balans.py <bieg T5 +10> --t5 <bieg T5 0>           (N1/N2 - przewaga Polnocy, bieg celowany)
# Bieg bez linii "Bitwa: B175" (np. kopiaT9-120 sprzed 175) - bitwy lordow z linii H3 (moc = ludzie x moc tieru gry, jak baza_balans.py).
# Zrodla: "Bitwa: B175" (bitwy.log), linie H3 (bitwy.log - zabici przez Dothrakow w D6), balans-krolestw.csv (narastajaco od startu
# sesji), konie-krolestwa.csv (doba; wiersze krolestw = AI lordowie i AI zalogi, osobno rod_gracza i inne_partie), budzet-rodow.csv
# (nazwy krolestw - mapa id=nazwa z linii "KingdomBalance (175.0): krolestwa id=nazwa" w logu Armoury).
# PRZEGLAD 175: N1 (bitwy Polnocy o stosunku 0.9-1.1) w zwyklym biegu nie ma proby (baza: 14 z 520 bitew lord-lord swiata w tym
# oknie, Polnoc 5 bitew w 120 dob) - hamulec +10% wynika z konstrukcji (autobitwa rozstrzyga sila); test progiem tylko w biegu T5
# (wymuszona wojna, tryb -ForceWar autotestu - galaz narzedzi, NIE zbudowany w paczce 175: bez T5 N1/N2 = "brak danych").
# W T5 obok N1 test N1b na WSZYSTKICH bitwach lordow Polnocy: srednia "wygrana - p(wygranej)" (WPO na bitwe) bieg +10 wobec 0, z sigma.
import sys, os, re, math, csv, collections, glob

def find(root, name):
    hits = sorted(glob.glob(os.path.join(root, "**", name), recursive=True), key=os.path.getmtime)
    return hits[-1] if hits else None

# znane id -> nazwa (gdy log nie ma linii z mapa); z ROT-Content/ModuleData ROT_spkingdoms.xml i spkingdoms.xslt (ROT 8.1.8)
KNOWN = {"battania": "The North", "khuzait": "Dothraki Horde", "sturgia": "Iron Islands", "aserai": "Dorne", "empire": "The Vale",
         "empire_w": "The Reach", "empire_s": "House Targaryen", "vlandia": "House Baratheon of King's Landing",
         "valyrian": "House Targaryen, Aegon", "freefolk": "Free Folk", "nightswatch": "Nights Watch", "dragonstone": "Dragonstone",
         "riverlands": "Riverlands", "stormlands": "Stormlands", "volantis": "Volantis", "bravos": "Braavos", "pentos": "Pentos",
         "norvos": "Norvos", "qohor": "Qohor", "myr": "Myr", "lys": "Lys", "tyrosh": "Tyrosh", "sarnor": "Sarnor", "skagosi": "Skagos",
         "summer": "Summer Isles", "yiti": "Yi Ti Exiles", "qarth": "Qarth", "ibb": "Ibben", "lorath": "Lorath"}
FREE7 = ["pentos", "myr", "lys", "tyrosh", "norvos", "qohor", "lorath"]
NOT_KINGDOM_ROWS = ("bez_krolestwa", "rod_gracza", "inne_partie")   # wiersze konie-krolestwa.csv, ktore nie sa krolestwami

# przeglad 175: PolnocT3+ to liczba ludzi i procent z jednym miejscem ("PolnocT3+ 75 (22.4%)"); pierwsza wersja pisala sam procent
SIDE = (r"krol=(?P<k{0}>\S+) rodzaj=(?P<r{0}>\w+) partii (?P<p{0}>\d+) ludzi (?P<m{0}>\d+) moc (?P<pw{0}>[\d.]+) konni (?P<c{0}>\d+)% KL (?P<kl{0}>\d+)%"
        r" PolnocT3\+ (?P<n{0}>\d+)(?:%| \((?P<np{0}>[\d.]+)%\)) straty (?P<s{0}>\d+)")
B175 = re.compile(r"Bitwa: B175 dzien (?P<day>\d+)(?: \[GRACZ\])? \| (?P<typ>\w+)(?: morze)? \| kontekst (?P<ctx>\w+) \| region (?P<reg>\S+) \((?P<rc>\S+)\)"
                  r" \| A: " + SIDE.format("a") + r" \| O: " + SIDE.format("d") +
                  r" \| stosunek sil (?P<ratio>[\d.]+) \((?P<rk>[^)]*)\) \| wygrywa (?P<win>\w+) \| lordowie (?P<lord>\w+)"
                  r" \| przewaga Polnocy: (?P<edge>\w+)(?: tak \(ciosow (?P<hits>\d+)\))?")
H3SIDE = re.compile(r"(przegrany|zwyciezca) (.+?) \(([^()]*?)(?:, partii (\d+))?\) (\d+) ludzi \(konni ([\d.]+)%, tier ([\d.]+)\)")
H3KILL = re.compile(r"\| H3: zabici (\d+)")
NONK = ("Broken Men", "Wild Hares", "Looters", "Sea Raiders", "Forest Bandits", "Mountain Bandits", "Desert Bandits", "Steppe Bandits")

def P(t): return (2 + t) * (10 + t) * 0.02

class Run:
    def __init__(self, root):
        self.root = root
        self.names = dict(KNOWN)
        alog = find(root, "Armoury-*.log")
        if alog:
            for line in open(alog, encoding="utf-8", errors="replace"):
                if "krolestwa id=nazwa:" in line:
                    for part in line.split("id=nazwa:", 1)[1].strip().rstrip(".").split("; "):
                        if "=" in part:
                            i, n = part.split("=", 1); self.names[i.strip()] = n.strip()
        self.byname = {v: k for k, v in self.names.items()}
        self.battles = []      # slowniki: dzien, krolA/O, moc, zwyciezca, lordowie, typ, przewaga, ciosy, PolnocT3+ (ludzie), straty, rodzaj
        self.source = "brak"
        self.h3_doth_kills = collections.Counter()   # D6: zabici przez Dothrakow w H3 (wsie i karawany 7 Wolnych Miast), wedlug dnia
        bl = find(root, "bitwy.log")
        h3 = []
        if bl:
            for line in open(bl, encoding="utf-8", errors="replace"):
                if "Bitwa: B175" in line:
                    m = B175.search(line)
                    if not m: continue
                    g = m.groupdict()
                    self.battles.append(dict(day=int(g["day"]), typ=g["typ"], ctx=g["ctx"], rc=g["rc"], ka=g["ka"], ra=g["ra"], pa=float(g["pwa"]),
                                             na=int(g["na"]), sa=int(g["sa"]), kd=g["kd"], rd=g["rd"], pd=float(g["pwd"]), nd=int(g["nd"]),
                                             sd=int(g["sd"]), win=g["win"], lord=g["lord"] == "tak", edge=g["edge"], hits=int(g["hits"] or 0)))
                elif "Bitwa: H3" in line and "FieldBattle" in line:
                    h3.append(line)
        if self.battles: self.source = "B175"
        elif h3: self.source = "H3"
        free7names = {self.names.get(k, k) for k in FREE7}
        doth = self.names.get("khuzait", "Dothraki Horde")
        for line in h3:
            md = re.search(r"dzien (\d+)", line); day = int(md.group(1)) if md else 0
            ms = H3SIDE.findall(line)
            if len(ms) < 2: continue
            (_, ln, lf, _a, lm, lk, lt), (_, wn, wf, _b, wm, wk, wt) = ms[0], ms[1]
            # D6: zwyciezca Dothrakowie, przegrany - tabor wsi albo karawana jednego z 7 Wolnych Miast
            if wf == doth and lf in free7names and (ln.startswith("Villagers of") or "Caravan" in ln):
                mk = H3KILL.search(line)
                if mk: self.h3_doth_kills[day] += int(mk.group(1))
            if self.source != "H3": continue
            if ln.startswith("Villagers of") or "Caravan" in ln or ln.endswith("Patrol"): continue
            if wn.startswith("Villagers of") or "Caravan" in wn or wn.endswith("Patrol"): continue
            if lf == wf or lf == ln or wf == wn or lf in NONK or wf in NONK: continue
            lp, wp = int(lm) * P(float(lt)), int(wm) * P(float(wt))
            if lp <= 0 or wp <= 0: continue
            ka, kd = self.byname.get(wf, wf), self.byname.get(lf, lf)
            self.battles.append(dict(day=day, typ="FieldBattle", ctx="?", rc="?", ka=ka, ra="lord", pa=wp, na=0, sa=0, kd=kd, rd="lord", pd=lp, nd=0,
                                     sd=0, win="A", lord=True, edge="nie", hits=0))
        self.days = sorted({b["day"] for b in self.battles})
        self.bal = self.csv("balans-krolestw.csv")
        self.horse = self.csv("konie-krolestwa.csv")
        self.budget = self.csv("budzet-rodow.csv")
        alld = [b["day"] for b in self.battles] + [int(r["dzien"]) for r in self.bal] + [int(r["dzien"]) for r in self.horse]
        self.d0 = min(alld) if alld else 0
        bd = [int(r["dzien"]) for r in self.budget if r.get("dzien", "").isdigit()]
        self.b0 = min(bd) if bd else self.d0

    def csv(self, name):
        p = find(self.root, name)
        if not p: return []
        with open(p, encoding="utf-8-sig", errors="replace") as f:
            return list(csv.DictReader(f, delimiter=";"))

    def lords(self, W):
        return [b for b in self.battles if b["lord"] and b["day"] < self.d0 + W]

    def doth_kills(self, W):
        return sum(v for d, v in self.h3_doth_kills.items() if d < self.d0 + W)

    def fit_k(self):
        rows = [b for b in self.battles if b["lord"] and b["pa"] > 0 and b["pd"] > 0]
        if not rows: return 12.6
        def ll(k):
            s = 0.0
            for b in rows:
                w, l = (b["pa"], b["pd"]) if b["win"] == "A" else (b["pd"], b["pa"])
                s += math.log(max(1 / (1 + math.exp(-k * math.log(w / l))), 1e-12))
            return s
        return max((ll(k / 10.0), k / 10.0) for k in range(5, 501))[1]   # siatka 0.5-50

    def table(self, W, k):
        S = collections.defaultdict(collections.Counter); wpo = collections.defaultdict(float)
        for b in self.lords(W):
            pA = 1 / (1 + math.exp(-k * math.log(b["pa"] / b["pd"])))
            aw = b["win"] == "A"
            close = max(b["pa"], b["pd"]) / min(b["pa"], b["pd"]) < 1.5
            for side, won, p in ((b["ka"], aw, pA), (b["kd"], not aw, 1 - pA)):
                S[side]["n"] += 1; S[side]["w"] += won; wpo[side] += (1 if won else 0) - p
                if close: S[side]["cn"] += 1; S[side]["cw"] += won
        return S, wpo

    def bal_at(self, W):
        """Ostatni wiersz balans-krolestw.csv nie pozniej niz d0+W (liczniki narastajaco)."""
        last = {}
        for r in self.bal:
            d = int(r["dzien"])
            if d < self.d0 + W: last[r["krolestwo"]] = r
        return last

    def horse_sum(self, W):
        s = collections.defaultdict(collections.Counter); days = collections.Counter()
        for r in self.horse:
            d = int(r["dzien"])
            if d >= self.d0 + W: continue
            k = r["krolestwo"]; days[k] += 1
            for c, v in r.items():
                if c in ("dzien", "krolestwo") or v is None: continue
                try: s[k][c] += float(v)
                except ValueError: pass
        return s, days

    def horse_rest(self, W):
        """Przeglad 175 (projekt 4.2 'reszta'): zmiana konnych doba do doby minus znane przeplywy - straty w bitwach, niewola,
        przeplywy zaloga-lord, odejscia. reszta = d(konni) - (naplyw ochotnicy+najemnicy+jency+inne + rot_plus - rot_minus
        - straz_pieszy + wykonane). Wedlug klucza (krolestwo / rod_gracza), suma w oknie."""
        by = collections.defaultdict(dict)
        for r in self.horse:
            d = int(r["dzien"])
            if d >= self.d0 + W: continue
            by[r["krolestwo"]][d] = r
        out = {}
        def f(r, c):
            try: return float(r.get(c) or 0)
            except ValueError: return 0.0
        for k, rows in by.items():
            ds = sorted(rows); tot = 0.0; n = 0
            for a, b in zip(ds, ds[1:]):
                if b != a + 1: continue
                ra, rb = rows[a], rows[b]
                flows = (f(rb, "naplyw_ochotnicy") + f(rb, "naplyw_najemnicy") + f(rb, "naplyw_jency") + f(rb, "naplyw_inne")
                         + f(rb, "rot_plus") - f(rb, "rot_minus") - f(rb, "straz_pieszy") + f(rb, "wykonane"))
                tot += (f(rb, "konni") - f(ra, "konni")) - flows; n += 1
            out[k] = (tot, n)
        return out

    def budget_at(self, W, lo=None):
        """Bankruci (ostatnia doba okna) i kiesy glow / saldo (srednia z dob lo..W) wedlug krolestwa (id)."""
        bank = collections.Counter(); poor = collections.Counter(); risk = collections.Counter()
        purse = collections.defaultdict(list); saldo = collections.defaultdict(list)
        days = sorted({int(r["dzien"]) for r in self.budget if r.get("dzien", "").isdigit() and int(r["dzien"]) < self.b0 + W})
        if not days: return bank, poor, risk, {}, {}
        lastd = days[-1]
        byday = collections.defaultdict(lambda: collections.defaultdict(lambda: [0.0, 0.0]))
        for r in self.budget:
            if not r.get("dzien", "").isdigit(): continue
            d = int(r["dzien"])
            if d >= self.b0 + W: continue
            k = self.byname.get(r.get("krolestwo", ""), r.get("krolestwo", "") or "-")
            try: kg = float(r.get("kiesa_glowy") or 0); sm = float(r.get("saldo_modelu") or 0)
            except ValueError: kg, sm = 0.0, 0.0
            if lo is not None and d >= self.b0 + lo:
                byday[d][k][0] += kg; byday[d][k][1] += sm
            if d == lastd:
                if (r.get("bankrut") or "").strip() not in ("", "0", "-", "nie"): bank[k] += 1
                if kg < 5000: poor[k] += 1
                if sm < 0 and kg / (-sm) < 60: risk[k] += 1
        for d, kk in byday.items():
            for k, (a, b) in kk.items(): purse[k].append(a); saldo[k].append(b)
        mp = {k: sum(v) / len(v) for k, v in purse.items() if v}; ms = {k: sum(v) / len(v) for k, v in saldo.items() if v}
        return bank, poor, risk, mp, ms

def pct(a, b): return "%.0f%%" % (100.0 * a / b) if b else "-"

def report(run, W, k):
    S, wpo = run.table(W, k)
    bal = run.bal_at(W); hs, hd = run.horse_sum(W)
    bank, poor, risk, _, _ = run.budget_at(W)
    print("\n== %s | okno %d dob (od doby %d) | zrodlo bitew: %s, bitew lordow %d" % (run.root, W, run.d0, run.source, len(run.lords(W))))
    print("%-14s %5s %6s %7s %8s %7s %9s %9s %6s %6s %6s %7s %7s %7s" % ("krolestwo", "bitwy", "wygr%", "wyrown", "wyr.wyg%", "WPO", "szt z/o/s",
          "twierdze", "+/-", "tierZ", "tierP", "bankr", "konni%", "czeka"))
    keys = sorted(set(S) | set(bal), key=lambda x: -S[x]["n"])
    for key in keys:
        s = S[key]; r = bal.get(key, {})
        h = hs.get(key, collections.Counter()); nd = max(1, hd.get(key, 0))
        storm = "%s/%s/%s" % (r.get("szturmy_zdobyte", "-"), r.get("szturmy_odparte", "-"), r.get("szturmy_stracone", "-")) if r else "-"
        forts = "%s(%s)" % (r.get("twierdze", "-"), r.get("twierdze_start", "-")) if r else "-"
        net = "%s/%s" % (r.get("twierdze_zdobyte", "-"), r.get("twierdze_stracone", "-")) if r else "-"
        mounted = pct(h["konni"] / nd, h["wszyscy"] / nd) if h else "-"
        print("%-14s %5d %6s %7d %8s %+7.1f %9s %9s %6s %6s %6s %7d %7s %7s" % (key[:14], s["n"], pct(s["w"], s["n"]), s["cn"], pct(s["cw"], s["cn"]), wpo[key],
              storm, forts, net, r.get("tier_zalog", "-"), r.get("tier_partii", "-"), bank.get(key, 0), mounted, "%.0f" % (h["czeka"] / nd) if h else "-"))
    if hs:
        t = collections.Counter()
        for c in hs.values(): t.update(c)
        g = lambda c: int(t[c])
        print("Konie AI (suma swiata, okno): wykonane %d (do zbrojowni %d, w tym nietrwalej %d, przepadlo %d, z wolnych %d), odrzucone %d (skret %d, czekaja %d),"
              " przyciete %d, zatrzymane %d; kupione %d za %d (nieudane: zloto %d, brak %d = pusto %d + za drogie %d + polka zarezerwowana %d;"
              " wizyty bez zakupu przy koniach innej kategorii %d); naplyw: ochotnicy %d, najemnicy %d, jency %d, ROT +%d/-%d (jeniec %d, najemnik %d, obcy %d,"
              " swoj %d), awanse %d; straz Dothrakow: zbrojownia %d, tabor %d, pieszy %d (footman %d, z elity %d), t6 %d, pieszego brak w puli %d;"
              " przecieki wolnych koni: zaloga %d, dezerterzy %d, echo ROT %d" % tuple(g(c) for c in (
              "wykonane", "do_zbrojowni", "do_zbrojowni_nietrwalej", "przepadlo", "awanse_z_wolnych", "odrzucone", "skret", "czekaja_proby",
              "przyciete", "zatrzymane", "kupione", "zloto", "nieudane_zloto", "nieudane_brak", "nieudane_pusto", "nieudane_za_drogie",
              "nieudane_polka_zarezerwowana", "wizyty_bez_zakupu_konie_innej_kategorii", "naplyw_ochotnicy", "naplyw_najemnicy", "naplyw_jency",
              "rot_plus", "rot_minus", "rot_plus_jeniec", "rot_plus_najemnik", "rot_plus_obcy", "rot_plus_swoj", "awanse", "straz_zbrojownia",
              "straz_tabor", "straz_pieszy", "straz_pieszy_footman", "straz_pieszy_z_elity", "straz_t6", "straz_pieszego_brak_w_puli",
              "wolne_po_jezdzcach_w_zalodze", "wolne_po_dezerterach", "konie_z_echa_rot")))
        rest = run.horse_rest(W)
        print("Konie AI wedlug krolestw (srednio na dobe: czeka / w tym mimo koni innej kategorii / wolne zwykle-bojowe-szlachetne; suma okna: czekaja (proby),"
              " nieudane pusto/drogie/zarezerwowana, reszta konnych = d(konni) - znane przeplywy, footman netto po strazy):")
        for key in sorted(hs, key=lambda x: -hs[x]["czeka"]):
            h = hs[key]; nd = max(1, hd.get(key, 0)); rs = rest.get(key, (0.0, 0))
            print("  %-14s czeka %6.1f / %6.1f | wolne %5.1f-%5.1f-%5.1f | czekaja %6d | nieudane %d/%d/%d | reszta %+8.0f (%d dob) | footman netto %+.0f"
                  % (key[:14], h["czeka"] / nd, h["czeka_mimo_koni_innej_kategorii"] / nd, h["wolne_zwykle"] / nd, h["wolne_bojowe"] / nd,
                     h["wolne_szlachetne"] / nd, h["czekaja_proby"], h["nieudane_pusto"], h["nieudane_za_drogie"], h["nieudane_polka_zarezerwowana"],
                     rs[0], rs[1], (h["footman_dothrakow"] / nd) - h["straz_pieszy_footman"]))
    print("Zabici przez Dothrakow w H3 (wsie i karawany 7 Wolnych Miast, okno): %d" % run.doth_kills(W))

def sig(p0, n): return math.sqrt(max(p0 * (1 - p0), 1e-9) / n) if n else 0.0

def verdict(run, base, W, k, kb, changed):
    print("\n== WERDYKT 6.2 (okno %d dob): %s wobec %s" % (W, run.root, base.root))
    S, _ = run.table(W, k); S0, _ = base.table(W, kb)
    bal, bal0 = run.bal_at(W), base.bal_at(W)
    bank, poor, risk, purse, saldo = run.budget_at(W, lo=max(0, W - 20))
    bank0, poor0, risk0, purse0, saldo0 = base.budget_at(W, lo=max(0, W - 20))
    hs, hd = run.horse_sum(W); hs0, hd0 = base.horse_sum(W)
    out = []
    def add(code, kind, key, txt): out.append((code, kind, key, txt))
    for key in sorted(set(S) | set(S0)):
        s, s0 = S[key], S0[key]
        # B1 - wyrownane
        if s["cn"] >= 8:
            p0 = s0["cw"] / s0["cn"] if s0["cn"] >= 8 else 0.5
            lim = p0 + 2 * sig(p0, s["cn"]); p = s["cw"] / s["cn"]
            if p > lim: add("B1", "sygnal", key, "wyrownane %.0f%% z %d > %.0f%% (p0 %.0f%%)" % (100 * p, s["cn"], 100 * lim, 100 * p0))
        elif s["cn"] > 0: add("B1", "brak danych", key, "wyrownanych %d < 8" % s["cn"])
        # B2 - wszystkie bitwy lordow
        if s["n"] >= 15:
            p0 = s0["w"] / s0["n"] if s0["n"] >= 8 else 0.5
            lim = max(0.85, p0 + 2 * sig(p0, s["n"])); p = s["w"] / s["n"]
            if p > lim: add("B2", "sygnal", key, "wygrane %.0f%% z %d > %.0f%%" % (100 * p, s["n"], 100 * lim))
    for key in sorted(set(bal) | set(bal0)):
        r, r0 = bal.get(key), bal0.get(key)
        if not r or not r0: continue
        net = int(r["twierdze_zdobyte"]) - int(r["twierdze_stracone"]); net0 = int(r0["twierdze_zdobyte"]) - int(r0["twierdze_stracone"])
        if net >= net0 + 3: add("B4", "TWARDY", key, "twierdze netto %+d wobec %+d w B0" % (net, net0))
        if int(r["twierdze"]) == 0 and int(r0["twierdze"]) > 0: add("B5", "TWARDY", key, "wyeliminowane (0 twierdz), w B0 przetrwalo")
        if key in changed:
            lost = int(r["twierdze_stracone"]) - int(r["twierdze_zdobyte"]); lost0 = int(r0["twierdze_stracone"]) - int(r0["twierdze_zdobyte"])
            if lost >= lost0 + 3: add("B6", "sygnal", key, "twierdze stracone netto %d wobec %d" % (lost, lost0))
            try:
                if float(r["tier_zalog"]) < float(r0["tier_zalog"]) - 0.3: add("B6", "sygnal", key, "tier zalog %s wobec %s" % (r["tier_zalog"], r0["tier_zalog"]))
            except (KeyError, ValueError): pass
    for key in changed:
        if bank[key] > bank0[key] + 2: add("D1", "TWARDY po T3'" if key == "khuzait" else "sygnal", key, "bankruci %d wobec %d" % (bank[key], bank0[key]))
        if poor[key] + risk[key] > poor0[key] + risk0[key] + 3: add("D3", "sygnal", key, "glowy < 5000 %d, zagrozone %d (B0 %d/%d)" % (poor[key], risk[key], poor0[key], risk0[key]))
    if "khuzait" in purse and "khuzait" in purse0 and purse0["khuzait"] > 0:
        if purse["khuzait"] < 0.5 * purse0["khuzait"]: add("D2", "TWARDY po T3'", "khuzait", "kiesy glow (srednia dob %d-%d) %.0f < 50%% B0 %.0f" % (W - 20, W, purse["khuzait"], purse0["khuzait"]))
        if saldo.get("khuzait", 0) < 0 and saldo0.get("khuzait", 0) < 0 and saldo["khuzait"] < 2 * saldo0["khuzait"]:
            add("D2", "info", "khuzait", "saldo_modelu (srednia) %.0f wobec %.0f" % (saldo["khuzait"], saldo0["khuzait"]))
    else: add("D2", "brak danych", "khuzait", "brak budzet-rodow.csv albo okna %d-%d" % (W - 20, W))
    r, r0 = bal.get("khuzait"), bal0.get("khuzait")
    try:
        if r and r0 and float(r["tier_partii"]) < float(r0["tier_partii"]) - 0.3: add("D4", "sygnal", "khuzait", "tier partii %s wobec %s" % (r["tier_partii"], r0["tier_partii"]))
    except (KeyError, ValueError): pass
    h, h0 = hs.get("khuzait"), hs0.get("khuzait")
    # D4 (przeglad 175): udzial "czeka na konia" (czeka / wszyscy) Dothrakow wobec B0 - zawsze w raporcie, sygnal przy > B0 + 10 pkt
    if h and h0 and h["wszyscy"] > 0 and h0["wszyscy"] > 0:
        c, c0 = h["czeka"] / h["wszyscy"], h0["czeka"] / h0["wszyscy"]
        add("D4", "sygnal" if c > c0 + 0.10 else "info", "khuzait", "czeka na konia %.1f%% ludzi wobec %.1f%% w B0" % (100 * c, 100 * c0))
    elif h is None or h0 is None: add("D4", "brak danych", "khuzait", "brak konie-krolestwa.csv w jednym z biegow")
    if h and h0 and h["konni"] > 0 and h0["konni"] > 0:
        a, a0 = h["bez_konia"] / h["konni"], h0["bez_konia"] / h0["konni"]
        if a > a0 + 0.10: add("D5", "sygnal", "khuzait", "konni bez konia %.0f%% wobec %.0f%%" % (100 * a, 100 * a0))
        rp, rp0 = h["rot_plus"] / max(1, hd["khuzait"]), h0["rot_plus"] / max(1, hd0["khuzait"])
        if rp > rp0 * 1.10 + 1: add("D5", "sygnal", "khuzait", "rot_plus/dobe %.1f wobec %.1f" % (rp, rp0))
    raid = sum(int(bal[k]["wsie_karawany_rozbite_przez_dothrakow"]) for k in FREE7 if k in bal)
    raid0 = sum(int(bal0[k]["wsie_karawany_rozbite_przez_dothrakow"]) for k in FREE7 if k in bal0)
    men = sum(int(bal[k]["ludzie_partie"]) for k in FREE7 if k in bal); men0 = sum(int(bal0[k]["ludzie_partie"]) for k in FREE7 if k in bal0)
    kill, kill0 = run.doth_kills(W), base.doth_kills(W)   # przeglad 175: zabici przez Dothrakow w H3 (czesc progu D6)
    if raid0 > 0 and raid > 2 * raid0: add("D6", "sygnal", "wolne miasta", "rozbite przez Dothrakow %d > 2 x %d" % (raid, raid0))
    if kill0 > 0 and kill > 2 * kill0: add("D6", "sygnal", "wolne miasta", "zabici przez Dothrakow w H3 %d > 2 x %d" % (kill, kill0))
    elif kill0 == 0 and kill > 0: add("D6", "info", "wolne miasta", "zabici przez Dothrakow w H3 %d (w B0 0)" % kill)
    if men0 > 0 and men < 0.8 * men0: add("D6", "sygnal", "wolne miasta", "ludzie w partiach %d < 80%% z %d" % (men, men0))
    if not out: print("  bez sygnalow (wszystkie progi 6.2 ponizej granic albo za malo danych - patrz tabela)")
    for code, kind, key, txt in sorted(out):
        print("  %-3s %-14s %-12s %s" % (code, kind, key, txt))
    hard = [o for o in out if o[1].startswith("TWARDY")]
    sigs = [o for o in out if o[1] == "sygnal"]
    print("  -> " + ("TWARDY prog: wylaczyc odpowiedzialna czesc i powtorzyc T3 (D1/D2 -> Army175DothrakiRide = NIE; B4/B5 zmienionego -> jego czesc)" if hard
                     else "sygnaly: drugi bieg T3'; ten sam sygnal dla tego samego krolestwa w obu biegach = jak TWARDY" if sigs
                     else "brak sygnalow (pozycje 'brak danych' - za mala proba, nie 'zaliczone')"))
    print("  (N1/N2 - tylko bieg celowany T5 z --t5; w zwyklym biegu bitew Polnocy o stosunku 0.9-1.1 jest za malo - brak danych)")

def north_side(b):
    return "A" if b["ka"] == "battania" else "O" if b["kd"] == "battania" else None

def t5(run, zero):
    print("\n== T5 (N1/N2): %s (+10) wobec %s (0)" % (run.root, zero.root))
    k = zero.fit_k()   # jedno k dla obu biegow - z biegu bez przewagi
    for name, r in (("+10", run), ("0", zero)):
        n = w = 0; own = foe = 0; nb = 0; res = 0.0; var = 0.0
        for b in r.battles:
            if not b["lord"]: continue
            side = north_side(b)
            if side is None: continue
            mine, other = (b["pa"], b["pd"]) if side == "A" else (b["pd"], b["pa"])
            won = b["win"] == side
            if 0.9 <= mine / other <= 1.1:
                n += 1; w += won
            # N1b (przeglad 175): wszystkie bitwy lordow Polnocy - wygrana minus p(wygranej) z mocy (WPO na bitwe)
            p = 1 / (1 + math.exp(-k * math.log(mine / other)))
            nb += 1; res += (1.0 if won else 0.0) - p; var += p * (1 - p)
        for b in r.battles:
            # ten sam warunek w obu biegach (w biegu 0 przewaga nie dziala, wiec nie "ciosy", tylko warunek NorthHomeEdge):
            # bitwa polowa, snieg wszedzie albo las w regionie Polnocy, po jednej stronie piechota Polnocy t3+ (liczba ludzi)
            if b["typ"] != "FieldBattle" or not (b["ctx"] == "SnowBattle" or (b["ctx"] == "ForestBattle" and b["rc"] == "battania")): continue
            if (b["na"] > 0) == (b["nd"] > 0): continue
            a = b["na"] > 0
            own += b["sa"] if a else b["sd"]; foe += b["sd"] if a else b["sa"]
        print("  bieg %-3s: Polnoc w bitwach lordow o stosunku 0.9-1.1: %d, wygrane %s; wszystkich bitew lordow Polnocy %d, WPO na bitwe %s;"
              " straty w bitwach z warunkiem przewagi %d/%d (wspolczynnik %s)"
              % (name, n, pct(w, n), nb, "%+.3f" % (res / nb) if nb else "-", own, foe, "%.2f" % (own / foe) if foe else "-"))
        r._n1 = (n, w); r._n2 = own / foe if foe else None; r._n1b = (nb, res, var)
    n, w = run._n1; n0, w0 = zero._n1
    if n >= 8:
        p0 = w0 / n0 if n0 >= 8 else 0.5
        p = w / n
        print("  N1: %.0f%% (n %d) wobec %.0f%% -> %s" % (100 * p, n, 100 * p0, "SYGNAL" if p > 0.75 or p - p0 > 2 * sig(p0, n) else "w normie"))
    else: print("  N1: brak danych (n %d < 8)" % n)
    (nb, res, var), (nb0, res0, var0) = run._n1b, zero._n1b
    if nb >= 8 and nb0 >= 8:
        m, m0 = res / nb, res0 / nb0
        s = math.sqrt(var / nb ** 2 + var0 / nb0 ** 2)
        print("  N1b: WPO na bitwe %+.3f (n %d) wobec %+.3f (n %d), roznica %+.3f, sigma %.3f (k %.1f) -> %s"
              % (m, nb, m0, nb0, m - m0, s, k, "SYGNAL" if s > 0 and m - m0 > 2 * s else "w normie"))
    else: print("  N1b: brak danych (bitew lordow Polnocy %d / %d, potrzeba po 8)" % (nb, nb0))
    if run._n2 is not None and zero._n2:
        drop = 1 - run._n2 / zero._n2
        print("  N2: wspolczynnik strat %.2f wobec %.2f (spadek %.0f%%, oczekiwane ok. 12%%) -> %s" % (run._n2, zero._n2, 100 * drop, "SYGNAL" if drop > 0.25 else "w normie"))
    else: print("  N2: brak danych")

def main(a):
    if not a or a[0] in ("-h", "--help"): print(__doc__ or open(__file__).read().split("\nimport")[0]); return
    root = a[0]; base = None; okna = [40, 120]; changed = ["battania", "khuzait", "sturgia", "pentos", "qarth"]; zero = None
    i = 1
    while i < len(a):
        if a[i] == "--baza": base = a[i + 1]; i += 2
        elif a[i] == "--t5": zero = a[i + 1]; i += 2
        elif a[i] == "--zmienione": changed = a[i + 1].split(","); i += 2
        elif a[i] == "--okna":
            okna = []; i += 1
            while i < len(a) and a[i].isdigit(): okna.append(int(a[i])); i += 1
        else: i += 1
    run = Run(root); k = run.fit_k()
    print("bieg %s: bitew B175/H3 %d (zrodlo %s), k dopasowane (siatka 0.5-50) = %.1f (w grze stale 12.6)" % (root, len(run.battles), run.source, k))
    for W in okna: report(run, W, k)
    if base:
        b = Run(base); kb = b.fit_k()
        print("\nbaza %s: bitew %d (zrodlo %s), k = %.1f" % (base, len(b.battles), b.source, kb))
        for W in okna: report(b, W, kb)
        for W in okna: verdict(run, b, W, k, kb, changed)
    if zero: t5(run, Run(zero))
    else: print("\nT5 (N1/N2): brak biegu --t5 - brak danych (tryb -ForceWar autotestu nie jest czescia paczki 175).")

if __name__ == "__main__":
    main(sys.argv[1:])
