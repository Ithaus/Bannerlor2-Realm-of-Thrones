# Buduje rody.csv: rody pomniejsze i rycerskie Westeros z AWOIAF (surowe\awoiaf_*.tsv) polaczone z mapa ROT.
# Tylko odczyt plikow gry. Wynik: ..\rody.csv + ..\skrypty\kalibracja.txt
import csv, json, math, re, os
from collections import defaultdict, Counter
import numpy as np

B = os.path.dirname(os.path.abspath(__file__))
L = os.path.dirname(B)
S = os.path.join(L, "surowe")

sets = json.load(open(os.path.join(B, "rot_settlements.json"), encoding="utf-8"))
clans = json.load(open(os.path.join(B, "rot_clans.json"), encoding="utf-8"))
byid = {s["id"]: s for s in sets}
clanbyid = {c["id"]: c for c in clans}
TC = [s for s in sets if s["kind"] in ("town", "castle")]
VIL = [s for s in sets if s["kind"] == "village"]

def norm(n):
    n = n.lower().replace("’", "'")
    n = re.sub(r"\(.*?\)", "", n)
    n = re.sub(r"^(the|castle) ", "", n.strip())
    return re.sub(r"[^a-z']", "", n)

name_tc = defaultdict(list)
for s in TC:
    name_tc[norm(s["name"])].append(s)
name_v = defaultdict(list)
for s in VIL:
    name_v[norm(s["name"])].append(s)

# Kraina ROT wedlug kultury osady
CULT2REG = {"battania": "The North", "skagosi": "The North", "river": "The Riverlands", "vale": "The Vale",
            "vlandia": "The Westerlands", "reach": "The Reach", "stormlands": "The Stormlands", "crownlands": "The Crownlands",
            "dragonstone": "The Crownlands", "aserai": "Dorne", "sturgia": "The Iron Islands", "nightswatch": "The Gift",
            "freefolk": "Beyond the Wall"}
TPL2REG = {"North": "The North", "Riverlands": "The Riverlands", "Vale": "The Vale", "Westerlands": "The Westerlands",
           "Reach": "The Reach", "Stormlands": "The Stormlands", "Crownlands": "The Crownlands", "Blackwater": "The Crownlands",
           "Dorne": "Dorne", "Islands": "The Iron Islands"}
PARAMOUNT = {"The North": "Stark", "The Riverlands": "Tully", "The Vale": "Arryn", "The Westerlands": "Lannister",
             "The Reach": "Tyrell", "The Stormlands": "Baratheon", "The Crownlands": "Baratheon of King's Landing",
             "Dorne": "Martell", "The Iron Islands": "Greyjoy"}

# Seat AWOIAF -> nazwa osady ROT (gdy nazwy sie roznia albo seat to obszar). Wpis "" = celowo bez osady (uzyj mapy / pana).
ALIAS = {
    "Twins": "The Twins", "Crag": "The Crag", "Eyrie": "The Eyrie", "Pinkmaiden Castle": "Pinkmaiden",
    "Castle Cerwyn": "Cerwin", "Hall of the Mormonts": "Mormont Keep", "Bear Island": "Mormont Keep",
    "Barrow Hall": "Barrowton", "Golden Tooth": "Goldentooth", "Salt Shore": "Saltshore", "Tor": "The Tor",
    "Wyl (castle)": "Wyl", "Rills": "The Rills", "Lord Hewett's castle": "Lord Hewett's Town",
    "Oakenshield": "Lord Hewett's Town", "Castle Driftmark": "Hull", "High Tide": "Hightide", "Claw Isle": "Celtigar Keep",
    "Sweetport Sound": "Sweetport", "Felwood": "Fellwood", "Dun Fort": "Duskendale", "Lord Harroway's Town": "Harroway",
    "Blacktyde (castle)": "Blacktyde", "Horn Hill": "Hornhill", "Fair Isle": "Faircastle", "Tarbeck Hall": "Tarbeck Village",
    "Longtable": "Long Table", "Spottswood": "Spottswood Village", "Old Palace": "Sunspear", "Hightower": "Oldtown",
    "Neck": "Greywater Watch", "wolfswood": "Ironrath", "northern mountains": "Norrey Keep", "Northern mountains": "Norrey Keep",
    "Wolf's Den": "White Harbor", "New Castle": "White Harbor", "Stony Shore": "Stoney Shore", "Orkmont": "Orkmont",
    "Great Wyk": "Hammerhorn", "Crow Spike Keep": "Hammerhorn", "Corpse Lake": "Hammerhorn", "Harlaw": "Ten Towers",
    "Harlaw Hall": "Ten Towers", "Tower of Glimmering": "Ten Towers", "Harridan Hill": "Harridan Hall", "Volmark": "Ten Towers",
    "Shatterstone": "Old Wyk", "Whispers": "Whisper Village", "Red Keep": "Red Keep", "Greyshield": "Grimston",
    "Southshield": "Lord Hewett's Town", "Greenshield": "Greenshield", "Pyke": "Pyke", "Winterfell": "Winterfell",
    "White Harbor": "White Harbor", "Eyrie;Gates of the Moon": "The Eyrie", "Driftwood Hall": "Driftwood Hall",
    "Crackclaw Point": "Crackclaw", "Cape Wrath": "Cape Wrath", "Riverrun": "Riverrun", "Casterly Rock": "Casterly Rock",
    "Sunspear": "Sunspear", "Storm's End": "Storm's End", "Highgarden": "Highgarden", "Lannisport": "Lannisport",
    "Dragonstone": "Dragonstone", "King's Landing": "King's Landing", "Duskendale": "Duskendale", "Maidenpool": "Maidenpool",
    "Harrenhal": "Harrenhal", "Gods Eye": "Harrenhal", "Gods Eye river": "Harrenhal", "Trident": "Saltpans",
    "Red Fork": "Stone Hedge", "Blue Fork": "Seagard", "Green Fork": "The Twins", "Ironoaks": "Ironoaks",
    "Gates of the Moon": "The Eyrie", "Old Wyk": "Old Wyk", "Saltcliffe": "Saltcliffe", "Sow's Horn": "",
    "Blackwater Bay": "King's Landing", "Blackwater Rush": "King's Landing", "Fingers": "Coldwater Burn",
    "Greenblood": "Planky Town", "Horseshoe Hills": "Red Lake", "Chequy Water": "", "Leafy Lake": "",
    "Castle Lychester": "", "Sallydance": "", "Whitewalls": "", "Blazewater Bay": "The Rills",
    "Sheepshead Hills": "Oldcastle", "White Knife": "White Harbor", "Bay of Crabs": "Maidenpool",
    "Shieldhall": "", "Plowman's Keep": "Darry", "Widow's Ford": "Saltpans", "Storm's End;siege": "Storm's End",
}
# ROT ma wioske o tej nazwie, ale w lore siedziba lezy gdzie indziej - nie dopasowuj po nazwie
NO_NAME_MATCH = {"Volmark", "Shatterstone", "Parchments", "Myre", "Kenning", "Sunderly", "Massey", "Hook", "Ryder", "Tarth",
                 "Hayford", "Barrow"}

def find_rot(seat):
    """Zwraca (osada, sposob) albo (None, None)."""
    if seat in ALIAS:
        a = ALIAS[seat]
        if a == "":
            return None, None
        k = norm(a)
        if name_tc.get(k):
            return name_tc[k][0], "alias->" + name_tc[k][0]["kind"]
        if name_v.get(k):
            return name_v[k][0], "alias->village"
    if seat in NO_NAME_MATCH:
        return None, None
    k = norm(seat)
    if name_tc.get(k):
        return name_tc[k][0], "name=" + name_tc[k][0]["kind"]
    if name_v.get(k):
        return name_v[k][0], "name=village"
    return None, None

# ---------- dane AWOIAF ----------
houses = []
for fn in ("awoiaf_houses_1.tsv", "awoiaf_houses_2.tsv", "awoiaf_houses_3.tsv"):
    for line in open(os.path.join(S, fn), encoding="utf-8"):
        line = line.rstrip("\n")
        if not line.strip():
            continue
        p = line.split("\t") + [""] * 10
        houses.append(dict(tpl=p[0], group=p[1], mark=p[2], house=p[3], seat=p[4], ov=p[5], arms=p[6],
                           names=p[7], died=p[8], places=p[9]))
seats = {}
for i, line in enumerate(open(os.path.join(S, "awoiaf_seats.tsv"), encoding="utf-8")):
    if i == 0 or not line.strip():
        continue
    p = line.rstrip("\n").split("\t") + [""] * 5
    lat = float(p[2]) if p[2] not in ("", None) else None
    lon = float(p[3]) if p[3] not in ("", None) else None
    seats[p[0]] = dict(map=p[1], lat=lat, lon=lon, places=p[4])

def first_seat(h):
    ss = [s for s in h["seat"].split(";") if s and not s.startswith("w:") and s not in ("narrow sea", "The Princess and the Queen", "Fire & Blood")]
    return ss

# ---------- kalibracja map AWOIAF (lat/long na mapie krainy) -> x,y ROT ----------
pairs = defaultdict(list)
for sname, sd in seats.items():
    if sd["lat"] is None or sd["lon"] is None or not sd["map"]:
        continue
    st, how = find_rot(sname)
    if st is None or st["kind"] == "village" or not how.startswith(("name", "alias")):
        continue
    pairs[sd["map"]].append((sd["lon"], sd["lat"], st["x"], st["y"], sname, st["name"]))
calib = {}
klog = []
for mp, pp in pairs.items():
    if len(pp) < 4:
        klog.append(f"{mp}: tylko {len(pp)} par - bez kalibracji")
        continue
    A = np.array([[a, b, 1.0] for a, b, _, _, _, _ in pp])
    X = np.array([x for _, _, x, _, _, _ in pp]); Y = np.array([y for _, _, _, y, _, _ in pp])
    # odporna kalibracja: 2 przebiegi, odrzuc pary z bledem > 2.5x mediany
    keep = np.ones(len(pp), bool)
    for _ in range(2):
        cx = np.linalg.lstsq(A[keep], X[keep], rcond=None)[0]; cy = np.linalg.lstsq(A[keep], Y[keep], rcond=None)[0]
        err = np.hypot(A @ cx - X, A @ cy - Y)
        med = np.median(err[keep])
        keep = err <= max(2.5 * med, 12.0)
    calib[mp] = (cx, cy)
    rms = float(np.sqrt(np.mean(err[keep] ** 2)))
    klog.append(f"{mp}: {int(keep.sum())}/{len(pp)} par, RMS {rms:.1f} j. mapy ROT; odrzucone: " +
                ", ".join(f"{pp[i][4]}->{pp[i][5]} ({err[i]:.0f})" for i in range(len(pp)) if not keep[i]))
calib["North.png"] = calib.get("North")  # ta sama mapa pod inna nazwa pliku

def from_map(sd):
    c = calib.get(sd["map"])
    if not c or sd["lat"] is None or sd["lon"] is None:
        return None
    v = np.array([sd["lon"], sd["lat"], 1.0])
    return float(v @ c[0]), float(v @ c[1])

def nearest(x, y, pool):
    best = None
    for s in pool:
        d = math.hypot(s["x"] - x, s["y"] - y)
        if best is None or d < best[0]:
            best = (d, s)
    return best

# ---------- klany ROT ----------
def clan_key(n):
    n = n.split(",")[0].split("-")[0].strip()
    return n.lower()
rot_clan_by_key = defaultdict(list)
for c in clans:
    if c["id"].startswith(("clan_", "ROTclan_")):
        rot_clan_by_key[clan_key(c["name"])].append(c)

def surname(h):
    n = re.sub(r"\s*\(.*?\)", "", h)
    return n.split(" of ")[0].strip()

# ---------- przebieg 1: polozenie kazdego rodu ----------
rows = []
house_pos = {}
for h in houses:
    tpl = h["tpl"]
    region = TPL2REG.get(tpl, tpl)
    if h["house"] == "Manderly":
        region = "The North"
    seat_list = first_seat(h)
    pos = None; how = ""; rot = None; seat_used = ""
    for sname in seat_list:
        st, m = find_rot(sname)
        if st is not None:
            pos = (st["x"], st["y"]); how = m; rot = st; seat_used = sname
            break
    if pos is None:
        for sname in seat_list:
            sd = seats.get(sname)
            if sd:
                xy = from_map(sd)
                if xy and nearest(xy[0], xy[1], TC)[0] > 45:
                    xy = None
                if xy:
                    pos = xy; how = "mapa AWOIAF (" + sd["map"] + ")"; seat_used = sname
                    break
    if pos is None:
        cand = []
        for sname in seat_list:
            sd = seats.get(sname)
            if sd and sd["places"]:
                cand += sd["places"].split(";")
        cand += [x for x in h["places"].split(";") if x]
        for pl in cand:
            st, m = find_rot(pl)
            if st is not None:
                pos = (st["x"], st["y"]); how = "opis: " + pl; rot = None; seat_used = seat_list[0] if seat_list else ""
                break
    h.update(region=region, pos=pos, how=how, rot=rot, seat_used=seat_used or (seat_list[0] if seat_list else ""))
    house_pos[h["house"]] = h

# ---------- przebieg 2: brak polozenia -> przy panu (rod zaprzysiezony siedzi na ziemi pana) ----------
def liege_pos(name, depth=0):
    if depth > 4:
        return None
    hh = house_pos.get(name)
    if hh is None:
        return None
    if hh["pos"] is not None:
        return hh["pos"]
    return liege_pos(hh["ov"], depth + 1)
for h in houses:
    if h["pos"] is None:
        lp = liege_pos(h["ov"]) if h["ov"] else None
        if lp is None and h["region"] in PARAMOUNT:
            lp = liege_pos(PARAMOUNT[h["region"]])
        if lp is not None:
            h["pos"] = lp
            h["how"] = "przy panu (" + (h["ov"] or PARAMOUNT.get(h["region"], "")) + "), siedziba nieopublikowana"

# ---------- kolory z blazonu ----------
TINCT = [("gold", "gold"), ("golden", "gold"), ("yellow", "gold"), ("saffron", "gold"), ("or", "gold"), ("brass", "gold"),
         ("silver", "silver"), ("white", "white"), ("ivory", "white"), ("cream", "white"), ("bone", "white"), ("ermine", "white"),
         ("argent", "silver"), ("red", "red"), ("crimson", "red"), ("scarlet", "red"), ("gules", "red"), ("blood", "red"),
         ("flame", "red"), ("burgundy", "burgundy"), ("blue", "blue"), ("azure", "blue"), ("indigo", "blue"), ("sky", "blue"),
         ("turquoise", "blue"), ("green", "green"), ("vert", "green"), ("olive", "green"), ("sea green", "green"),
         ("black", "black"), ("sable", "black"), ("dark", ""), ("purple", "purple"), ("violet", "purple"), ("lilac", "purple"),
         ("purpure", "purple"), ("orange", "orange"), ("brown", "brown"), ("russet", "brown"), ("bronze", "bronze"),
         ("copper", "bronze"), ("rust", "brown"), ("mud", "brown"), ("oak", "brown"), ("sand", "sand"), ("grey", "grey"),
         ("gray", "grey"), ("smoke", "grey"), ("stone", "grey"), ("iron", "grey"), ("pink", "pink"), ("rose", "pink"),
         ("carnation", "pink"), ("pean", "black")]
HEX = {"gold": "#D4A017", "silver": "#C0C0C0", "white": "#F2F2EE", "red": "#B22222", "burgundy": "#7B1E3A", "blue": "#2F5DA8",
       "green": "#2E7D32", "black": "#1A1A1A", "purple": "#6A3D9A", "orange": "#E07B24", "brown": "#7A5230", "bronze": "#A97142",
       "sand": "#D8C38A", "grey": "#808080", "pink": "#E8A0B4"}
def colours(arms):
    a = " " + arms.lower().replace(",", " ").replace(";", " ").replace(":", " ") + " "
    found = []
    # pole (tlo) zwykle po ostatnim " on "
    for word, col in sorted(TINCT, key=lambda t: -len(t[0])):
        if not col:
            continue
        for m in re.finditer(r"(?<![a-z])" + re.escape(word) + r"(?![a-z])", a):
            found.append((m.start(), col))
    found.sort()
    seq = []
    for _, c in found:
        if c not in seq:
            seq.append(c)
    field = None
    m = re.search(r" on (?:a |an |the )?(?:field of )?([a-z \-]+?)(?: within| beneath| a | with|$|  )", a)
    if m:
        for word, col in sorted(TINCT, key=lambda t: -len(t[0])):
            if col and re.search(r"(?<![a-z])" + re.escape(word) + r"(?![a-z])", m.group(1)):
                field = col; break
    if field and field in seq:
        seq.remove(field); seq = [field] + seq
    return seq[:3]

# ---------- klany ROT i rola ----------
AREA_ALIAS = {"Great Wyk", "Crow Spike Keep", "Corpse Lake", "Harlaw", "Harlaw Hall", "Tower of Glimmering", "Volmark",
              "wolfswood", "northern mountains", "Northern mountains", "Neck", "Wolf's Den", "Shatterstone", "Southshield"}
def rot_clan_for(h):
    key = surname(h["house"]).lower()
    cs = rot_clan_by_key.get(key, [])
    if not cs:
        return None
    branch = (" of " in h["house"]) or ("(" in h["house"])
    if branch and h["seat_used"] in AREA_ALIAS:
        return None
    for c in cs:
        home = byid.get(c["home"])
        if not home or home["kind"] not in ("town", "castle"):
            continue
        # siedziba rodu = siedziba klanu (ta sama osada ROT albo do 5 j.)
        if h.get("rot") is not None and h["rot"]["id"] == home["id"]:
            return c
        if h["pos"] and math.hypot(home["x"] - h["pos"][0], home["y"] - h["pos"][1]) < (5 if branch else 25):
            return c
    if not branch:
        for c in cs:
            home = byid.get(c["home"])
            if home and CULT2REG.get(home["culture"]) == h["region"]:
                return c
    return None

exec(open(os.path.join(B, "build_out.py"), encoding="utf-8").read())
