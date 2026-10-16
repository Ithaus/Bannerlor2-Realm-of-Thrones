# Badanie B: zlaczenie wiosek z mapy (arm_map_villages.tsv) z osadami ROT (settlements.xml) i klanami.
# Tylko odczyt plikow gry. Wynik: b-skrypty/wioski_b.tsv + statystyki na ekran.
import re, json, math, csv, os, sys, zlib, xml.etree.ElementTree as ET
from collections import Counter, defaultdict

GAME = r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
OUT = os.path.dirname(os.path.abspath(__file__))   # E7/L: wyniki posrednie obok skryptu (bylo: Temp sesji 3cf3e0ac)
# plik ukladu z argumentu (E7/L: l0.py podaje Armoury\ModuleData\arm_map_villages_v4.tsv z repo - v4, 2 446 wiosek; Jeff 16.10:
# arm_map_villages.tsv to juz uklad v5, a lore powstaje z v4 - kolejnosc potoku w naglowku l0.py)
TSV = sys.argv[1] if len(sys.argv) > 1 else os.path.join(OUT, "..", "..", "..", "Armoury", "ModuleData", "arm_map_villages_v4.tsv")
TAG = sys.argv[2] if len(sys.argv) > 2 else ""    # np. v4-proba: wyniki b_dymki.py w wioski-lore/<TAG>
SUF = ("_" + TAG) if TAG else ""
PLACES = {"road", "river", "coast", "crossroad", "bridge", "field", "lake", "ford", "mouth", "fork", "bay", "harbour"}
KINDS_V4 = {"village", "mill", "windmill", "farm", "granary", "fishing"}
RIVERS_JSON = os.path.join(OUT, "rzeki.json")   # E7/L: kopia wioski-na-mapie\a-teren\rzeki.json (07.10, sciezki rzek sceny ROT)

CULTURE_REGION = {
    "battania": "north", "nightswatch": "gift", "freefolk": "beyond", "wildlings": "beyond",
    "skagosi": "skagos", "river": "riverlands", "vale": "vale", "vlandia": "westerlands",
    "reach": "reach", "stormlands": "stormlands", "crownlands": "crownlands",
    "dragonstone": "dragonstone", "sturgia": "iron", "aserai": "dorne",
    "empire": "braavos", "pentoshi": "pentos", "norvos": "norvos", "qohorik": "qohor",
    "nord": "lorath", "myrish": "myr", "tyroshi": "tyrosh", "lyseni": "lys",
    "volantine": "volantis", "valyrian": "valyria", "ghiscari": "ghis", "qartheen": "qarth",
    "khuzait": "dothraki", "sarnor": "sarnor", "ibbenese": "ibben", "yiti": "yiti", "summer": "summer",
}

# nazwa sciezki rzeki w scenie ROT -> nazwa w tekscie (z rodzajnikiem); brak = rzeka bez nazwy w lore
RIVER_TEXT = {
    "trident": "the Trident", "red fork": "the Red Fork", "red fork2": "the Red Fork", "blue fork": "the Blue Fork",
    "blackwater": "the Blackwater Rush", "wendwater": "the Wendwater", "slayne": "the Slayne",
    "mander": "the Mander", "lesser mander": "the Mander", "cockleswhent": "the Cockleswhent",
    "blueburn": "the Blueburn", "honeywine": "the Honeywine", "honeywine2": "the Honeywine",
    "torentine": "the Torrentine", "torentine2": "the Torrentine", "greenblood": "the Greenblood",
    "scourge": "the Scourge", "brimstone": "the Brimstone", "upper rhoyne": "the Rhoyne", "rhoyne": "the Rhoyne",
    "little rhyone": "the Little Rhoyne", "noyne": "the Noyne", "qhoyne": "the Qhoyne", "sarne": "the Sarne",
    "sarne2": "the Sarne", "milk water": "the Milkwater", "white knife": "the White Knife",
    "last river": "the Last River", "broken branch": "the Broken Branch", "broken branch2": "the Broken Branch",
    "acorn water": "the Acorn Water", "dread": "the Weeping Water", "meereen": "the Skahazadhan",
}
RIVER_MAX = 6.0   # jedn. mapy: rzeka "przy wiosce" (wioski nad rzeka stoja 1.5-5.2 jedn. od sciezki)

def nm(s):
    return re.sub(r"\{=[^}]*\}", "", s or "").strip()

def load_settlements():
    t = ET.parse(GAME + r"\ROT-Map\ModuleData\settlements.xml").getroot()
    S = {}
    for s in t.iter("Settlement"):
        comp = s.find("Components")
        kind, bound, vtype, hearth = "other", "", "", ""
        if comp is not None:
            for c in comp:
                if c.tag == "Town":
                    kind = "castle" if c.get("is_castle") == "true" else "town"
                elif c.tag == "Village":
                    kind = "village"; bound = (c.get("bound") or "").replace("Settlement.", "")
                    vtype = (c.get("village_type") or "").replace("VillageType.", ""); hearth = c.get("hearth") or ""
                elif c.tag == "Hideout":
                    kind = "hideout"
        S[s.get("id")] = dict(id=s.get("id"), name=nm(s.get("name")), x=float(s.get("posX") or 0), y=float(s.get("posY") or 0),
                              culture=(s.get("culture") or "").replace("Culture.", ""), owner=(s.get("owner") or "").replace("Faction.", ""),
                              kind=kind, bound=bound, vtype=vtype, hearth=hearth, text=nm(s.get("text")))
    return S

def load_clans():
    raw = open(GAME + r"\ROT-Content\ModuleData\spclans.xml", encoding="utf-8").read()
    C = {}
    for m in re.finditer(r"<Faction\s([^>]*)>", raw):
        a = dict(re.findall(r'(\w+)="([^"]*)"', m.group(1)))
        C[a.get("id")] = dict(name=nm(a.get("name")), culture=a.get("culture", "").replace("Culture.", ""))
    return C

def load_rivers():
    R = []
    for r in json.load(open(RIVERS_JSON, encoding="utf-8")):
        if r.get("klasa") != "rzeka": continue
        t = RIVER_TEXT.get(r["name"])
        if not t: continue
        R.append((t, r["pts"]))
    return R

def nearest_river(R, x, y):
    best, bd = "", 1e9
    for t, pts in R:
        for p in pts:
            d = (p[0] - x) ** 2 + (p[1] - y) ** 2
            if d < bd: bd, best = d, t
    bd = math.sqrt(bd)
    return (best, bd) if bd <= RIVER_MAX else ("", bd)

def load_villages():
    rows = []
    with open(TSV, encoding="utf-8") as f:
        hdr = None
        for line in f:
            line = line.rstrip("\n")
            if line.startswith("#id"): hdr = line[1:].split("\t"); continue
            if line.startswith("#") or not line.strip(): continue
            rows.append(dict(zip(hdr, line.split("\t"))))
    return rows

def main():
    S = load_settlements(); C = load_clans(); R = load_rivers(); V = load_villages()
    out = []
    for v in V:
        gv = S.get(v["village_id"], {})
        seat = S.get(gv.get("bound", ""), {})
        cult = gv.get("culture", "")
        reg = CULTURE_REGION.get(cult, "?")
        x, y = float(v["x"]), float(v["y"])
        river, rd = nearest_river(R, x, y)
        owner = C.get(seat.get("owner", ""), {}).get("name", "")
        # v4: rodzaj obrazka w kolumnie kind albo type, miejsce w kolumnie place albo kind (TECHNIKA.md rozdz. 5)
        place = v.get("place") or (v.get("kind") if v.get("kind") in PLACES else "")
        kind4 = next((v.get(c) for c in ("model", "type", "kind") if v.get(c) in KINDS_V4), "")
        out.append(dict(uid=v["uid"], name=v["name"], village_id=v["village_id"], district=gv.get("name", ""),
                        vtype=gv.get("vtype", ""), culture=cult, region=reg, place=place, kind4=kind4,
                        order=v.get("order", "0"), cls=v["class"],
                        seat_id=gv.get("bound", ""), seat=seat.get("name", ""), seat_kind=seat.get("kind", ""),
                        seat_owner=owner, river=river, river_d=round(rd, 1), x=x, y=y, settlements=v["settlements"]))
    keys = list(out[0].keys())
    with open(os.path.join(OUT, "wioski_b%s.tsv" % SUF), "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, keys, delimiter="\t"); w.writeheader(); w.writerows(out)
    crc = zlib.crc32(open(TSV, "rb").read()) & 0xFFFFFFFF
    open(os.path.join(OUT, "layout_crc32%s.txt" % SUF), "w").write("%08x %s\n" % (crc, TSV))
    print("plik ukladu", TSV, "crc32 %08x" % crc)
    print("wiosek", len(out), "z rodzajem v4:", sum(1 for o in out if o["kind4"]))
    print("regiony", Counter(o["region"] for o in out).most_common())
    print("miejsca", Counter(o["place"] for o in out).most_common())
    print("klasy", Counter(o["cls"] for o in out).most_common())
    print("typy wsi", Counter(o["vtype"] for o in out).most_common())
    print("z rzeka", sum(1 for o in out if o["river"]), Counter(o["river"] for o in out if o["river"]).most_common())
    rv = defaultdict(Counter)
    for o in out: rv[o["region"]][o["vtype"]] += 1
    for r in sorted(rv): print(" ", r, dict(rv[r].most_common()))
    # osady gry wg regionu i typu (tez wsie bez wiosek)
    gv = Counter((CULTURE_REGION.get(s["culture"], "?"), s["vtype"]) for s in S.values() if s["kind"] == "village")
    json.dump({f"{a}|{b}": n for (a, b), n in gv.items()}, open(os.path.join(OUT, "wsie_gry_region_typ.json"), "w"), indent=0)
    # siedziby (zamki/miasta) z opisem - do historii
    seats = {s["id"]: dict(name=s["name"], kind=s["kind"], culture=s["culture"], owner=C.get(s["owner"], {}).get("name", ""),
                           text=s["text"][:400]) for s in S.values() if s["kind"] in ("castle", "town")}
    json.dump(seats, open(os.path.join(OUT, "siedziby.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)

if __name__ == "__main__":
    main()
