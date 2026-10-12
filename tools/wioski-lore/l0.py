# E7/L (PLAN 7.8, projekt etapu 7 rozdz. 3): dane lore 2 446 wiosek na mapie -> jeden plik Armoury\ModuleData\arm_map_village_lore.tsv
# z bramka ukladu (R11) + kontrole L0 + probka 20 dymkow dla Jeffa. Tylko Python poza gra; mod czyta plik dopiero od E7/W2.
#
# Uzycie (z katalogu repo):
#   python tools/wioski-lore/l0.py                      - caly przebieg: rody (A) -> rycerze (A) -> zdania (B) -> zlaczenie -> kontrole
#   python tools/wioski-lore/l0.py --bez-generacji      - tylko zlaczenie i kontrole z wynikow posrednich
# Opcje: --uklad <arm_map_villages.tsv> (domyslnie Armoury\ModuleData z repo), --gra <katalog Modules gry, tylko odczyt>,
#        --oczekiwany <crc ukladu z logu gry, hex> (domyslnie 5A8C2735 - VillageBook w LOG120).
# Kod wyjscia 0 = wszystkie kontrole L0 przeszly; 1 = ktoras nie (plik i tak zapisany - do obejrzenia).
#
# Kolejnosc krokow (kazdy skrypt mozna puscic osobno):
#   skrypty/build_rody.py   -> rody.csv (P2: wymarle przed 298 AC tylko do historii; wymaga numpy - bez niego zostaje rody.csv z repo)
#   skrypty/generator.py    -> skrypty/rycerze.tsv (P1 imiona, P3 herby unikalne w krainie, P4 herb Essos + kody herbow, P12 uklad z argumentu)
#   b-skrypty/b_wiara_zajecia.py, b_historia.py -> wiara.csv, zajecia.csv, historia.csv (P8-P10 zdania, P9 bez Haerega)
#   b-skrypty/b_dane.py     -> b-skrypty/wioski_b.tsv (wioska + osada ROT + rzeka)
#   b-skrypty/b_dymki.py    -> wioski-lore-przypis.tsv (P7 historia bez powtorki tematu zajecia / wiary)
#   l0.py                   -> Armoury/ModuleData/arm_map_village_lore.tsv (P11 war_lore, P13 jeden plik, tabela wiar BK) + probka-dymkow.txt
import argparse, csv, os, re, subprocess, sys, zlib
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(HERE, "skrypty"))
from lore_osoby import FAMOUS, MAIN_GIVEN, lore_persons, read_rody   # noqa: E402

OUT_FILE = os.path.join(REPO, "Armoury", "ModuleData", "arm_map_village_lore.tsv")
SAMPLE_FILE = os.path.join(HERE, "probka-dymkow.txt")
COLUMNS = ["uid", "knight", "knight_form", "knight_house", "knight_arms", "knight_banner",
           "livelihood", "faith", "faith_key", "faith_bk", "history", "war_lore"]
MAXLEN = {"livelihood": 125, "history": 125, "war_lore": 125, "faith": 110}

# R12: klucz wiary lore (wiara.csv, kolumna faith) -> id religii Banner Kings w ROT (BKROTPatch ROTReligions, dekompilacja 12.10).
# Kilka id rozdzielonych "|" = zdanie lore pasuje do kazdej z nich; puste = BK nie ma tej wiary (dymek pokaze tylko wiare BK).
FAITH_BK = {
    "seven": "seven", "seven_weirwood": "seven", "seven_north": "seven", "seven_dorne": "seven", "seven_red": "seven",
    "gift_seven": "seven", "mother_rhoyne": "seven|mother_rhoyne",
    "old_gods": "old_gods", "old_gods_river": "old_gods", "old_gods_free": "old_gods", "old_gods_skagos": "old_gods",
    "drowned_god": "drowned_god",
    "braavos_many": "many_faced_god|vhugyahar",   # kultura empire (Braavos): Vhugyahar i Many-Faced God
    "pentos_many": "princes_mercies",
    "norvos_bearded": "bearded_god",
    "qohor_goat": "black_goat",
    "lorath_mixed": "pattern|boash",              # lorathi: Pattern; Boash = Slepy Bog z Lorath
    "myr_mixed": "golden_gardens",
    "lys_mixed": "pearl_divinities",
    "tyrosh_mixed": "hues_of_trios",
    "volantis_mixed": "heritory|rhllor",
    "valyria_ruins": "fourteen_flames",
    "ghis_gods": "harpy",
    "lhazar_shepherd": "",                        # Wielki Pasterz - BK go nie ma
    "qarth_many": "pearl_divinities",             # qartheen: Pearl Divinities
    "dothraki_horse": "great_stallion",
    "sarnor_hundred": "josimakazma",
    "ibben_gods": "",                             # ROT daje kulturze ibbenese Starych Bogow - zdanie o bogach Ibbenu by im przeczylo
    "yiti_gods": "yiti",
    "summer_love": "summer",
}
# P11: pamiec z ksiazek tuz przed startem ROT (299 AC) - Raid on Sherrer, koniec 298 AC (okregi = wsie gry)
WAR_LORE_DISTRICTS = {"Sherrer", "Mummer's Ford", "Wendish Town"}
WAR_LORE_TEXT = "In the last days of 298 AC men without banners burned {DISTRICT} and the villages near it."


def fnv1a(s):
    h = 0x811C9DC5
    for b in s.encode("utf-8"):
        h ^= b
        h = (h * 0x01000193) & 0xFFFFFFFF
    return h


def read_layout(path):
    """Jak MapVillageData.Parse + VillageBook.LoadFile: wiersze w kolejnosci pliku, uid (albo village_id#id), crc "uid:osady;"."""
    hdr, rows = None, []
    for line in open(path, encoding="utf-8-sig"):
        line = line.rstrip("\r\n")
        if not line.strip():
            continue
        if line[0] == "#":
            if line[1:].startswith("id\t"):
                hdr = [h.strip() for h in line[1:].split("\t")]
            continue
        c = dict(zip(hdr, [x.strip() for x in line.split("\t")]))
        uid = c.get("uid") or ""
        if uid in ("", "-"):
            uid = c["village_id"] + "#" + c["id"]
        rows.append(dict(uid=uid, settlements=int(c["settlements"]), village_id=c["village_id"], name=c["name"]))
    crc = zlib.crc32("".join("%s:%d;" % (r["uid"], r["settlements"]) for r in rows).encode("utf-8")) & 0xFFFFFFFF
    return rows, "%08X" % crc


def run(script, *args):
    cmd = [sys.executable, os.path.join(HERE, script)] + list(args)
    print(">>", " ".join(os.path.relpath(x, REPO) if x.startswith(REPO) else x for x in cmd[1:]))
    p = subprocess.run(cmd, cwd=os.path.dirname(os.path.join(HERE, script)), capture_output=True, text=True)
    tail = (p.stdout or "").strip().splitlines()[-3:]
    for t in tail:
        print("   ", t[:160])
    if p.returncode != 0:
        print((p.stderr or "")[-2000:])
        raise SystemExit("blad w " + script)


def generate(uklad, gra):
    try:
        import numpy  # noqa: F401  (build_rody.py - kalibracja map AWOIAF)
        run("skrypty/build_rody.py")
    except ImportError:
        print(">> build_rody.py pominiety (brak numpy) - zostaje rody.csv z repo")
    run("skrypty/generator.py", uklad, gra)
    run("b-skrypty/b_wiara_zajecia.py")
    run("b-skrypty/b_historia.py")
    run("b-skrypty/b_dane.py", uklad)
    run("b-skrypty/b_dymki.py")


def tsv(path):
    return list(csv.DictReader(open(path, encoding="utf-8"), delimiter="\t"))


def banner_ok(code, icon_ids, palette):
    p = code.split(".")
    if len(p) < 10 or len(p) % 10:
        return False
    try:
        v = [int(x) for x in p]
    except ValueError:
        return False
    for i in range(0, len(v), 10):
        if i > 0 and v[i] not in icon_ids:
            return False
        if v[i + 1] not in palette or v[i + 2] not in palette:
            return False
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--uklad", default=os.path.join(REPO, "Armoury", "ModuleData", "arm_map_villages.tsv"))
    ap.add_argument("--gra", default=r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules")
    ap.add_argument("--oczekiwany", default="5A8C2735")
    ap.add_argument("--bez-generacji", action="store_true")
    a = ap.parse_args()
    uklad = os.path.abspath(a.uklad)
    if not a.bez_generacji:
        generate(uklad, a.gra)

    lay, crc = read_layout(uklad)
    A = {r["uid"]: r for r in tsv(os.path.join(HERE, "skrypty", "rycerze.tsv"))}
    Bz = {r["uid"]: r for r in tsv(os.path.join(HERE, "wioski-lore-przypis.tsv"))}
    VB = {r["uid"]: r for r in tsv(os.path.join(HERE, "b-skrypty", "wioski_b.tsv"))}

    out, fails, notes = [], [], []
    for v in lay:
        u = v["uid"]
        ra, rb, vb = A.get(u), Bz.get(u), VB.get(u)
        if ra is None or rb is None or vb is None:
            fails.append("brak danych dla " + u + (" (A)" if ra is None else "") + (" (B)" if rb is None else ""))
            continue
        district = vb["district"]
        out.append(dict(uid=u, knight=ra["knight"], knight_form=ra["knight_form"], knight_house=ra["house"],
                        knight_arms=ra["arms"][:1].upper() + ra["arms"][1:] if ra["arms"] else "",
                        knight_banner=ra["banner"], livelihood=rb["work_text"], faith=rb["faith_text"], faith_key=rb["faith"],
                        faith_bk=FAITH_BK.get(rb["faith"], "?"), history=rb["hist_text"],
                        war_lore=WAR_LORE_TEXT.replace("{DISTRICT}", district) if district in WAR_LORE_DISTRICTS else "",
                        _region=rb["region"], _akey=ra["region"] if ra["region"] != "Essos" else "Essos:" + ra["culture"],
                        _src=ra["src"], _name=v["name"], _district=district, _seat=vb["seat"], _owner=vb["seat_owner"],
                        _sett=int(vb["settlements"])))

    with open(OUT_FILE, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Armoury map village lore (A: knights, B: livelihood, faith, history). Generated by tools/wioski-lore/l0.py - do not edit.\n")
        f.write("# layout_crc: %s\n" % crc)
        f.write("# layout_crc = crc32 of \"uid:settlements;\" over the rows of arm_map_villages.tsv in file order (as VillageBook); "
                "other layout = lore off.\n")
        f.write("# knight_form: kept = \"Kept for them by {knight}\", tribute = \"{knight} takes its tribute\". "
                "knight_banner empty = use the lord's banner.\n")
        f.write("# faith_bk: Banner Kings religion ids (BKROTPatch) the lore faith matches, '|' = any; empty = none.\n")
        f.write("#" + "\t".join(COLUMNS) + "\n")
        for r in out:
            f.write("\t".join(r[c] for c in COLUMNS) + "\n")
    size = os.path.getsize(OUT_FILE)

    # ---------------- kontrole L0 ----------------
    def check(ok, what):
        print(("  OK    " if ok else "  BLAD  ") + what)
        if not ok:
            fails.append(what)

    print("\nKontrole L0 (%s, %d B):" % (os.path.relpath(OUT_FILE, REPO), size))
    uids = [r["uid"] for r in out]
    check(len(set(uids)) == len(lay) == len(uids), "pokrycie uid: %d / %d (powtorzen %d)" % (len(set(uids)), len(lay), len(uids) - len(set(uids))))
    check(crc == a.oczekiwany.upper(), "bramka ukladu %s (oczekiwany z logu gry %s)" % (crc, a.oczekiwany.upper()))
    # P1: 0 osob z lore
    rody = read_rody(os.path.join(HERE, "rody.csv"))
    LP = lore_persons(rody)
    bad_p = []
    for r in out:
        person = r["knight"].split(" of ")[0]
        w = [x for x in person.split() if x not in ("Ser", "Lady", "Brother", "Ko")]
        low = [x.lower() for x in w]
        hit = (low and low[0] in MAIN_GIVEN) or any(f in person.lower() for f in FAMOUS)
        for i in range(len(low)):
            for j in (i + 1, i + 2):   # "Imie Nazwisko" i "Imie zo Nazwisko"
                if j < len(low) and (low[i], low[j]) in LP:
                    hit = True
        if hit:
            bad_p.append(r["knight"])
    check(not bad_p, "imiona rycerzy: %d z list postaci lore / glownych postaci %s" % (len(bad_p), bad_p[:5]))
    check(all(r["knight"] for r in out), "kazda wioska ma rycerza")
    # P3: 0 powtorzen herbu w krainie (rozne rody, ten sam herb)
    arms_by = defaultdict(lambda: defaultdict(set))
    for r in out:
        if r["knight_arms"]:
            arms_by[r["_akey"]][r["knight_arms"].lower()].add(r["knight_house"])
    dup = [(k, h) for k, d in arms_by.items() for h, hs in d.items() if len(hs) > 1]
    check(not dup, "herby: %d powtorzen w krainie %s" % (len(dup), dup[:3]))
    houses_arms = {r["knight_house"]: bool(r["knight_arms"]) for r in out if r["knight_house"]}
    notes.append("rody z herbem tekstem: %d / %d (bez herbu: rody lore bez blazonu na AWOIAF)"
                 % (sum(houses_arms.values()), len(houses_arms)))
    # P4: kody herbow
    import xml.etree.ElementTree as ET
    icon_ids, palette = set(), set()
    try:
        for mod in ("Native", "ROT-Content"):
            root = ET.parse(os.path.join(a.gra, mod, "ModuleData", "banner_icons.xml")).getroot()
            icon_ids |= {int(i.get("id")) for i in root.iter("Icon")} | {int(i.get("id")) for i in root.iter("Background")}
            if mod == "Native":
                palette |= {int(c.get("id")) for c in root.iter("Color")}
    except (OSError, ET.ParseError) as e:
        notes.append("banner_icons.xml niedostepny (%s) - kody herbow niesprawdzone" % e)
    codes = [r["knight_banner"] for r in out if r["knight_banner"]]
    if icon_ids:
        badc = [c for c in codes if not banner_ok(c, icon_ids, palette)]
        check(not badc, "kody herbow: %d z %d bez ikony / koloru gry %s" % (len(badc), len(codes), badc[:2]))
    notes.append("kody herbow (godla potwierdzone w danych gry): %d wiosek, %d rodow; reszta pusta = herb pana"
                 % (len(codes), len({r["knight_house"] for r in out if r["knight_banner"]})))
    # tekst: ASCII, bez tabulatorow, bez wstawek, dlugosci, bez "hamlet"
    nonascii = [r["uid"] for r in out if any(any(ord(ch) > 127 for ch in r[c]) for c in COLUMNS)]
    check(not nonascii, "ASCII: %d wierszy z innymi znakami %s" % (len(nonascii), nonascii[:3]))
    unres = [r["uid"] for r in out if any(("{" in r[c] or "}" in r[c] or "\t" in r[c]) for c in COLUMNS)]
    check(not unres, "wstawki / tabulatory: %d" % len(unres))
    for c, mx in MAXLEN.items():
        m = max(len(r[c]) for r in out)
        check(m <= mx, "dlugosc %s: max %d (prog %d)" % (c, m, mx))
    check(not any("hamlet" in r[c].lower() for r in out for c in COLUMNS), "bez slowa 'hamlet'")
    unk = sorted({r["faith_key"] for r in out if r["faith_bk"] == "?"})
    check(not unk, "tabela wiar BK: klucze bez wpisu %s" % unk)
    wl = [r for r in out if r["war_lore"]]
    check(sorted({r["_district"] for r in wl}) == sorted(WAR_LORE_DISTRICTS), "war_lore (P11): %d wiosek w okregach %s"
          % (len(wl), sorted({r["_district"] for r in wl})))
    notes.append("rycerze: %s; forma linii %s" % (dict(Counter(r["_src"] for r in out)), dict(Counter(r["knight_form"] for r in out))))
    notes.append("wiary BK w pliku: %s" % dict(Counter(r["faith_bk"] or "-" for r in out).most_common()))
    for n in notes:
        print("  info  " + n)

    write_sample(out)
    print("\nPlik: %s (%d wierszy, bramka %s)" % (os.path.relpath(OUT_FILE, REPO), len(out), crc))
    print("Probka: %s" % os.path.relpath(SAMPLE_FILE, REPO))
    if fails:
        print("\nKONTROLE L0 NIE PRZESZLY: %d" % len(fails))
        return 1
    print("\nKontrole L0: wszystkie przeszly.")
    return 0


COMPANIES = {"Second Sons", "Stormcrows", "Stalwart Shields", "Golden Company"}


def write_sample(out):
    """20 dymkow z 5 krain (po 4) - linia pana i liczba ludzi to tylko przyblizenie (w grze na zywo), reszta = plik."""
    regs = ["north", "riverlands", "reach", "dorne", "braavos"]
    picked = []
    for reg in regs:
        cand = sorted([r for r in out if r["_region"] == reg], key=lambda r: fnv1a(r["uid"] + "|probka"))
        take, kinds = [], set()
        if reg == "riverlands":   # jedna wioska z pamiecia wojny z ksiazek (P11)
            take += [r for r in cand if r["war_lore"]][:1]
        for r in cand:
            if len(take) >= 4:
                break
            k = (r["_src"] != "gen", bool(r["knight_banner"]))
            if r not in take and (k not in kinds or len(take) >= 2):
                take.append(r); kinds.add(k)
        picked += take[:4]
    blocks = []
    for r in picked:
        d = r["_district"][4:] if r["_district"].startswith("The ") else r["_district"]
        o = r["_owner"].split(",")[0].strip()
        lord = ("the %s from %s" % (o, r["_seat"])) if o in COMPANIES else ("House %s of %s" % (o, r["_seat"]))
        knight = ("%s takes its tribute" % r["knight"]) if r["knight_form"] == "tribute" else "Kept for them by " + r["knight"]
        lines = ["[%s | %s | %s%s]" % (r["_region"], r["uid"], "rod lore" if r["_src"] != "gen" else "rod wymyslony",
                                       " | herb: kod" if r["knight_banner"] else ""),
                 r["_name"].upper(),
                 "A village of the %s lands, held by %s" % (d, lord),
                 knight,
                 "{:,} souls in {} settlements".format(r["_sett"] * 250, r["_sett"]),
                 "Hold Alt for more info",
                 "  " + r["livelihood"], "  " + r["faith"], "  " + r["history"]]
        if r["war_lore"]:
            lines.append("  " + r["war_lore"])
        lines.append("  (arms of %s: %s)" % (r["knight_house"] or "-", r["knight_arms"] or "none"))
        blocks.append("\n".join(lines))
    with open(SAMPLE_FILE, "w", encoding="utf-8", newline="\n") as f:
        f.write("# E7/L: 20 przykladowych dymkow wioski (po angielsku, jak w grze po E7/W2) - do wgladu Jeffa, nie blokuje.\n")
        f.write("# Linia pana i liczba ludzi: przyblizenie z settlements.xml (w grze na zywo, z herbem pana); linie po 'Hold Alt' = plik lore.\n")
        f.write("# Herb rycerza (ostatnia linia) pokaze menu okregu (E7/W3).\n\n")
        f.write("\n\n".join(blocks) + "\n")


if __name__ == "__main__":
    sys.exit(main())
