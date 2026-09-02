# -*- coding: utf-8 -*-
# army_report.py - pelny przeglad armii ROT do PDF (Jeff 02.09: "pdf z tabelka
# i opisami kazdej armii: pancerz, skille, uzbrojenie, obrazenia; na ostatniej
# stronie top 10 najlepszych jednostek kazdego typu").
# Zrodlo: DANE z XML (ROT-Troops, items, nadpisania RBM, War Sails) - czyli
# stan PRZED naszymi prawami (ArmorSanity, Prawo Wagi/Tieru) i przed RBM
# w locie. Uzycie: py -3 tools/army_report.py [wyjscie.pdf]
import os, re, sys, glob
import xml.etree.ElementTree as ET
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib import colors
from reportlab.lib.units import mm
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.platypus import (SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle,
                                PageBreak, KeepTogether)

MODS = r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "docs", "ROT-armie-przeglad.pdf")

def clean(name):
    s = re.sub(r"^\{=[^}]*\}", "", name or "")
    s = re.sub(r"\{@Plural\}.*?\{\@\}", "", s)     # {@Plural}...{\@} to warianty liczby mnogiej
    return s.strip()

def parse_xml(path):
    try:
        return ET.parse(path).getroot()
    except Exception as e:
        print("  pomijam (XML):", os.path.basename(path), "-", str(e)[:60])
        return None

# ---------------- ITEMS ----------------
items = {}   # id -> dict(name, type, armor{h,b,a,l}, weapon{...}, horse, crafted)
def load_items(path):
    root = parse_xml(path)
    if root is None: return 0
    n = 0
    for el in root.iter():
        if el.tag == "Item":
            iid = el.get("id");
            if not iid: continue
            d = items.get(iid, {})
            d["name"] = clean(el.get("name")) or d.get("name", iid)
            d["type"] = el.get("Type") or d.get("type", "")
            d["weight"] = float(el.get("weight") or d.get("weight", 0) or 0)
            d["tier"] = None
            comp = el.find("ItemComponent")
            if comp is not None:
                arm = comp.find("Armor")
                if arm is not None:
                    d["armor"] = {k: int(float(arm.get(k) or 0)) for k in ("head_armor", "body_armor", "arm_armor", "leg_armor")}
                wp = comp.find("Weapon")
                if wp is not None:
                    d["weapon"] = {
                        "class": wp.get("weapon_class", ""),
                        "swing": int(float(wp.get("swing_damage") or 0)), "swing_t": (wp.get("swing_damage_type") or "")[:1],
                        "thrust": int(float(wp.get("thrust_damage") or 0)), "thrust_t": (wp.get("thrust_damage_type") or "")[:1],
                        "len": int(float(wp.get("weapon_length") or 0)), "missile": int(float(wp.get("missile_speed") or 0)),
                    }
                if comp.find("Horse") is not None:
                    d["horse"] = True
            items[iid] = d; n += 1
        elif el.tag == "CraftedItem":
            iid = el.get("id")
            if not iid: continue
            items[iid] = {"name": clean(el.get("name")) or iid, "type": "Crafted", "crafted": el.get("crafting_template", "?"), "weight": 0}
            n += 1
    return n

item_files = []
item_files += sorted(glob.glob(os.path.join(MODS, "SandBoxCore", "ModuleData", "items", "*.xml")))   # vanilla bronie/pancerze/konie
item_files += [os.path.join(MODS, "NavalDLC", "ModuleData", "items.xml")]
item_files += [os.path.join(MODS, "ROT-Content", "ModuleData", "items.xml"),
               os.path.join(MODS, "ROT-Content", "ModuleData", "ROTassets.xml")]
item_files += sorted(glob.glob(os.path.join(MODS, "RBM", "ModuleData", "RBMCombat_*.xml")))   # nadpisania RBM (pancerze, luki, strzaly); pliki bez <Item> sa pomijane
item_files += [os.path.join(MODS, "RBM_WS", "ModuleData", "RBMCombat_WS_items.xml")]
print("Przedmioty:")
for f in item_files:
    if os.path.isfile(f):
        print("  %-45s %d" % (os.path.relpath(f, MODS)[:45], load_items(f)))
print("  razem:", len(items))
DUMP = os.path.join(os.path.expanduser("~"), "Documents", "Mount and Blade II Bannerlord", "CrashScribe", "items-dump.csv")
DUMP_USED = False
if os.path.isfile(DUMP):
    import csv
    n = 0
    with open(DUMP, encoding="utf-8-sig") as fh:
        for row in csv.DictReader(fh, delimiter=";"):
            d = items.get(row["id"], {})
            d["name"] = row["name"] or d.get("name", row["id"])
            d["type"] = row["type"]; d["weight"] = float(row["weight"]); d["difficulty"] = int(row["difficulty"]); d["tier"] = int(row["tier"])
            if any(int(row[k]) for k in ("head", "body", "arm", "leg")):
                d["armor"] = {"head_armor": int(row["head"]), "body_armor": int(row["body"]), "arm_armor": int(row["arm"]), "leg_armor": int(row["leg"])}
            if row["wclass"]:
                d["weapon"] = {"class": row["wclass"], "swing": int(row["swing"]), "swing_t": row["swing_t"][:1], "thrust": int(row["thrust"]),
                               "thrust_t": row["thrust_t"][:1], "len": int(row["length"]), "missile": int(row["missile_speed"])}
                d.pop("crafted", None)
            if row["type"] == "Horse": d["horse"] = True
            items[row["id"]] = d; n += 1
    DUMP_USED = True
    print("  ZRZUT Z GRY:", DUMP, "-", n, "przedmiotow (obrazenia i pancerz jak w grze, po RBM i naszych prawach)")
else:
    print("  brak zrzutu z gry (uruchom gre raz z CrashScribe) - bron kuta bez obrazen")

# ---------------- CULTURES ----------------
cultures = {}
for f in [os.path.join(MODS, "ROT-Content", "ModuleData", "spcultures.xml")]:
    root = parse_xml(f)
    if root is None: continue
    for c in root.iter("Culture"):
        cultures[c.get("id")] = {
            "name": clean(c.get("name")) or c.get("id"),
            "basic": (c.get("basic_troop") or "").replace("NPCCharacter.", ""),
            "elite": (c.get("elite_basic_troop") or "").replace("NPCCharacter.", ""),
            "bandit": c.get("is_bandit") == "true",
        }
CULTURE_PL = {"battania": "The North (Polnoc)", "vlandia": "Westerlands", "river": "Riverlands",
              "sturgia": "Iron Islands", "aserai": "Dorne", "khuzait": "Dothraki", "empire": "Crownlands"}

# ---------------- TROOPS ----------------
troops = {}
SKILLS = ["OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing", "Riding", "Athletics"]
troop_files = [os.path.join(MODS, "SandBoxCore", "ModuleData", "spnpccharacters.xml"),
               os.path.join(MODS, "ROT-Content", "ModuleData", "ROT-Troops.xml")]
troop_files += [f for f in glob.glob(os.path.join(MODS, "ROT-Content", "ModuleData", "*.xml"))
                if "troop" in os.path.basename(f).lower() and f not in troop_files]
for f in troop_files:
    root = parse_xml(f)
    if root is None: continue
    for t in root.iter("NPCCharacter"):
        tid = t.get("id")
        if not tid or t.get("occupation") not in ("Soldier", "Mercenary", "Bandit", "Gangster"): continue
        d = {"id": tid, "name": clean(t.get("name")) or tid, "culture": (t.get("culture") or "").replace("Culture.", ""),
             "level": int(t.get("level") or 1), "group": t.get("default_group") or "?",
             "basic": t.get("is_basic_troop") == "true", "occupation": t.get("occupation"),
             "skills": {s: 0 for s in SKILLS}, "upgrades": [], "eq": {}}
        sk = t.find("skills")
        if sk is not None:
            for s in sk.findall("skill"):
                if s.get("id") in d["skills"]: d["skills"][s.get("id")] = int(s.get("value") or 0)
        ut = t.find("upgrade_targets")
        if ut is not None:
            d["upgrades"] = [u.get("id", "").replace("NPCCharacter.", "") for u in ut.findall("upgrade_target")]
        eqs = t.find("Equipments")
        if eqs is not None:
            for r in eqs.findall("EquipmentRoster"):
                if r.get("civilian") == "true": continue
                for e in r.findall("equipment"):
                    d["eq"].setdefault(e.get("slot"), e.get("id", "").replace("Item.", ""))
                if d["eq"]: break
        if d["culture"]: troops[tid] = d
print("Jednostki:", len(troops))

def tier(level): return max(0, min(7, (level - 1) // 5))

# pancerz per slot (sumy jak w grze: kazdy element dodaje swoje wartosci)
def armor_of(d):
    tot = {"head_armor": 0, "body_armor": 0, "arm_armor": 0, "leg_armor": 0}
    for slot in ("Head", "Body", "Gloves", "Leg", "Cape"):
        it = items.get(d["eq"].get(slot, ""))
        if it and "armor" in it:
            for k in tot: tot[k] += it["armor"][k]
    return tot

def weapons_of(d):
    out = []
    for slot in ("Item0", "Item1", "Item2", "Item3"):
        iid = d["eq"].get(slot)
        if not iid: continue
        it = items.get(iid)
        if it is None: out.append(iid); continue
        if "crafted" in it: out.append("%s [kuty %s]" % (it["name"], it["crafted"])); continue
        w = it.get("weapon")
        if w is None: out.append(it["name"]); continue
        cls = w["class"]
        if cls in ("Arrow", "Bolt", "Stone", "SlingStone"):
            out.append("%s (pocisk +%d%s)" % (it["name"], w["thrust"], w["thrust_t"]))
        elif cls in ("Bow", "Crossbow"):
            out.append("%s (%s %d%s)" % (it["name"], "luk" if cls == "Bow" else "kusza", w["thrust"], w["thrust_t"]))
        elif cls in ("Javelin", "ThrowingAxe", "ThrowingKnife", "Stone"):
            out.append("%s (rzut %d%s)" % (it["name"], w["thrust"], w["thrust_t"]))
        elif cls in ("SmallShield", "LargeShield"):
            out.append("%s (tarcza)" % it["name"])
        else:
            parts = []
            if w["swing"]: parts.append("c%d%s" % (w["swing"], w["swing_t"]))
            if w["thrust"]: parts.append("p%d%s" % (w["thrust"], w["thrust_t"]))
            out.append("%s (%s, %dcm)" % (it["name"], "/".join(parts) or "-", w["len"]))
    return out

def horse_of(d):
    iid = d["eq"].get("Horse")
    if not iid: return ""
    it = items.get(iid)
    return it["name"] if it else iid

# korzenie drzew (skad brac jednostke)
parent = {}
for tid, d in troops.items():
    for u in d["upgrades"]:
        parent.setdefault(u, tid)
def root_of(tid):
    seen = set()
    while tid in parent and tid not in seen:
        seen.add(tid); tid = parent[tid]
    return tid
village_roots = set()
for c in cultures.values():
    if c["basic"]: village_roots.add(c["basic"])
    if c["elite"]: village_roots.add(c["elite"])
def source_of(tid):
    r = root_of(tid)
    if r in village_roots: return "wies/miasto (%s)" % troops.get(r, {}).get("name", r)
    return "rod/lordowie (%s)" % troops.get(r, {}).get("name", r)

# ---------------- SCORING ----------------
def melee(d): return max(d["skills"]["OneHanded"], d["skills"]["TwoHanded"], d["skills"]["Polearm"])
def ranged(d): return max(d["skills"]["Bow"], d["skills"]["Crossbow"])
def armor_sum(d): a = armor_of(d); return sum(a.values())
def score_inf(d): return melee(d) + d["skills"]["Athletics"] * 0.5 + armor_sum(d) * 0.25
def score_rng(d): return ranged(d) + melee(d) * 0.25 + armor_sum(d) * 0.25
def score_cav(d): return d["skills"]["Riding"] + melee(d) + armor_sum(d) * 0.25
def score_ha(d): return d["skills"]["Riding"] + ranged(d) + armor_sum(d) * 0.25

# ---------------- PDF ----------------
styles = getSampleStyleSheet()
H1 = ParagraphStyle("h1", parent=styles["Heading1"], fontSize=15, spaceAfter=4)
H2 = ParagraphStyle("h2", parent=styles["Heading2"], fontSize=11, spaceAfter=3)
P = ParagraphStyle("p", parent=styles["Normal"], fontSize=7.5, leading=9.5)
C = ParagraphStyle("c", parent=styles["Normal"], fontSize=6, leading=7)
doc = SimpleDocTemplate(OUT, pagesize=landscape(A4), leftMargin=8*mm, rightMargin=8*mm, topMargin=8*mm, bottomMargin=8*mm,
                        title="Realm of Thrones - przeglad armii", author="CrashScribe/Armoury tools")
story = []
story.append(Paragraph("Realm of Thrones 8.1.8 - pelny przeglad armii", H1))
story.append(Paragraph(("Zrodlo: ZRZUT Z GRY (items-dump.csv z CrashScribe - wartosci jak w grze, po RBM i naszych prawach) + drzewa jednostek z XML. " if DUMP_USED else "Zrodlo: dane XML modow (ROT-Troops, items ROT/Native/War Sails, nadpisania pancerzy RBM). ")
                       "Wartosci pancerza i obrazen sa Z DANYCH - w grze RBM przelicza je w locie, a nasze prawa "
                       "(Prawo Rozsadku Pancerza, Prawo Wagi, Prawo Tieru) podnosza wymagania i tna odstajace sztuki. "
                       "Tier = (poziom-1)/5. Obrazenia broni: c = ciecie (swing), p = pchniecie (thrust); "
                       "C/P/B = Cut/Pierce/Blunt. 'Skad': wies/miasto = werbunek z osad tej kultury; rod/lordowie = "
                       "linia rodowa - tylko w partiach lordow i garnizonach (zdobywasz jencow albo awansujesz zdobytych).", P))
story.append(Spacer(1, 4))

# spis kultur
by_cult = {}
for d in troops.values(): by_cult.setdefault(d["culture"], []).append(d)
def cult_name(cid):
    base = cultures.get(cid, {}).get("name", cid)
    return CULTURE_PL.get(cid, base) if cid in CULTURE_PL else base
order = sorted(by_cult.keys(), key=lambda c: (cultures.get(c, {}).get("bandit", False), cult_name(c)))
summary = [["Kultura", "id", "Jednostek", "Piechota", "Strzelcy", "Jazda", "Konni lucznicy", "Najwyzszy tier", "Werbunek podstawowy / szlachecki"]]
for cid in order:
    lst = by_cult[cid]
    g = lambda k: sum(1 for d in lst if d["group"] == k)
    c = cultures.get(cid, {})
    summary.append([cult_name(cid), cid, len(lst), g("Infantry"), g("Ranged"), g("Cavalry"), g("HorseArcher"),
                    "T%d" % max(tier(d["level"]) for d in lst),
                    "%s / %s" % (troops.get(c.get("basic"), {}).get("name", c.get("basic") or "-"),
                                 troops.get(c.get("elite"), {}).get("name", c.get("elite") or "-"))])
t = Table(summary, repeatRows=1)
t.setStyle(TableStyle([("FONTSIZE", (0, 0), (-1, -1), 6.5), ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#333333")),
                       ("TEXTCOLOR", (0, 0), (-1, 0), colors.white), ("GRID", (0, 0), (-1, -1), 0.25, colors.grey),
                       ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.whitesmoke, colors.white])]))
story.append(Paragraph("Spis armii", H2)); story.append(t); story.append(PageBreak())

# sekcje per kultura
hdr = ["Jednostka", "T", "Typ", "1H", "2H", "Pol", "Luk", "Kus", "Rzut", "Jazda", "Atl", "Glowa", "Korpus", "Rece", "Nogi", "Kon", "Uzbrojenie (obrazenia z danych)", "Skad"]
colw = [38*mm, 6*mm, 14*mm, 7*mm, 7*mm, 7*mm, 7*mm, 7*mm, 7*mm, 8*mm, 7*mm, 9*mm, 10*mm, 8*mm, 8*mm, 20*mm, 83*mm, 30*mm]
for cid in order:
    lst = sorted(by_cult[cid], key=lambda d: (-d["level"], d["group"], d["name"]))
    c = cultures.get(cid, {})
    story.append(Paragraph("%s  (%s%s)" % (cult_name(cid), cid, ", bandyci" if c.get("bandit") else ""), H1))
    best_i = max(lst, key=score_inf); best_r = max(lst, key=score_rng)
    cav = [d for d in lst if d["eq"].get("Horse")]
    desc = ("Jednostek: %d, najwyzszy tier T%d. Najlepsza piechota: %s (walka %d, pancerz %d). Najlepszy strzelec: %s (%s %d). "
            % (len(lst), max(tier(d["level"]) for d in lst), best_i["name"], melee(best_i), armor_sum(best_i),
               best_r["name"], "luk" if best_r["skills"]["Bow"] >= best_r["skills"]["Crossbow"] else "kusza", ranged(best_r)))
    if cav:
        best_c = max(cav, key=score_cav)
        desc += "Najlepsza jazda: %s (jazda %d, walka %d, kon: %s). " % (best_c["name"], best_c["skills"]["Riding"], melee(best_c), horse_of(best_c))
    desc += "Werbunek z osad: %s / %s." % (troops.get(c.get("basic"), {}).get("name", "-"), troops.get(c.get("elite"), {}).get("name", "-"))
    story.append(Paragraph(desc, P))
    rows = [hdr]
    for d in lst:
        a = armor_of(d); s = d["skills"]
        rows.append([Paragraph(d["name"], C), "T%d" % tier(d["level"]), d["group"][:9],
                     s["OneHanded"], s["TwoHanded"], s["Polearm"], s["Bow"], s["Crossbow"], s["Throwing"], s["Riding"], s["Athletics"],
                     a["head_armor"], a["body_armor"], a["arm_armor"], a["leg_armor"],
                     Paragraph(horse_of(d) or "-", C), Paragraph("; ".join(weapons_of(d)) or "-", C), Paragraph(source_of(d["id"]), C)])
    tb = Table(rows, colWidths=colw, repeatRows=1)
    tb.setStyle(TableStyle([("FONTSIZE", (0, 0), (-1, -1), 6), ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#333333")),
                            ("TEXTCOLOR", (0, 0), (-1, 0), colors.white), ("GRID", (0, 0), (-1, -1), 0.2, colors.grey),
                            ("VALIGN", (0, 0), (-1, -1), "TOP"), ("ALIGN", (3, 1), (14, -1), "RIGHT"),
                            ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.whitesmoke, colors.white]),
                            ("LEFTPADDING", (0, 0), (-1, -1), 2), ("RIGHTPADDING", (0, 0), (-1, -1), 2),
                            ("TOPPADDING", (0, 0), (-1, -1), 1), ("BOTTOMPADDING", (0, 0), (-1, -1), 1)]))
    story.append(tb); story.append(PageBreak())

# TOP 10
story.append(Paragraph("TOP 10 kazdego typu (wszystkie armie, bez bandytow)", H1))
story.append(Paragraph("Wzory (jawne, do dyskusji): PIECHOTA = max(1H,2H,Pol) + 0.5 x Atletyka + 0.25 x suma pancerza; "
                       "STRZELCY = max(Luk,Kusza) + 0.25 x walka wrecz + 0.25 x pancerz; JAZDA (jednostki z koniem) = Jazda + walka wrecz + 0.25 x pancerz; "
                       "KONNI LUCZNICY (kon + luk/kusza >= 80) = Jazda + max(Luk,Kusza) + 0.25 x pancerz. Suma pancerza = glowa+korpus+rece+nogi z danych.", P))
regular = [d for d in troops.values() if not cultures.get(d["culture"], {}).get("bandit") and d["occupation"] in ("Soldier", "Mercenary")]
def top_table(title, cands, fn, extra):
    top = sorted(cands, key=fn, reverse=True)[:10]
    rows = [["#", "Jednostka", "Armia", "T", "Wynik", "1H", "2H", "Pol", "Luk", "Kus", "Jazda", "Atl", "Pancerz", extra[0], "Skad"]]
    for i, d in enumerate(top, 1):
        s = d["skills"]
        rows.append([i, Paragraph(d["name"], C), cult_name(d["culture"]), "T%d" % tier(d["level"]), "%.0f" % fn(d),
                     s["OneHanded"], s["TwoHanded"], s["Polearm"], s["Bow"], s["Crossbow"], s["Riding"], s["Athletics"], armor_sum(d),
                     Paragraph(extra[1](d), C), Paragraph(source_of(d["id"]), C)])
    tb = Table(rows, colWidths=[6*mm, 42*mm, 30*mm, 7*mm, 12*mm, 8*mm, 8*mm, 8*mm, 8*mm, 8*mm, 10*mm, 8*mm, 13*mm, 70*mm, 38*mm], repeatRows=1)
    tb.setStyle(TableStyle([("FONTSIZE", (0, 0), (-1, -1), 6.5), ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#5a1e1e")),
                            ("TEXTCOLOR", (0, 0), (-1, 0), colors.white), ("GRID", (0, 0), (-1, -1), 0.2, colors.grey),
                            ("VALIGN", (0, 0), (-1, -1), "TOP"), ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.whitesmoke, colors.white]),
                            ("TOPPADDING", (0, 0), (-1, -1), 1), ("BOTTOMPADDING", (0, 0), (-1, -1), 1)]))
    return KeepTogether([Paragraph(title, H2), tb, Spacer(1, 5)])
story.append(top_table("Piechota", [d for d in regular if not d["eq"].get("Horse")], score_inf, ("Uzbrojenie", lambda d: "; ".join(weapons_of(d)))))
story.append(top_table("Strzelcy (piesi)", [d for d in regular if not d["eq"].get("Horse") and ranged(d) >= 80], score_rng, ("Uzbrojenie", lambda d: "; ".join(weapons_of(d)))))
story.append(top_table("Jazda", [d for d in regular if d["eq"].get("Horse")], score_cav, ("Kon / uzbrojenie", lambda d: horse_of(d) + " | " + "; ".join(weapons_of(d)))))
story.append(top_table("Konni lucznicy", [d for d in regular if d["eq"].get("Horse") and ranged(d) >= 80], score_ha, ("Kon / uzbrojenie", lambda d: horse_of(d) + " | " + "; ".join(weapons_of(d)))))

doc.build(story)
print("PDF:", os.path.abspath(OUT))
