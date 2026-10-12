# Zrzut osad i klanow ROT (tylko odczyt plikow gry)
import os, re, json, xml.etree.ElementTree as ET
M = r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
OUT = os.path.dirname(os.path.abspath(__file__))   # E7/L: obok skryptu (bylo: Temp sesji 3cf3e0ac)
def nm(s):
    return re.sub(r"\{=[^}]*\}", "", s or "").strip()
t = ET.parse(M + r"\ROT-Map\ModuleData\settlements.xml").getroot()
sets = []
for s in t.iter("Settlement"):
    comp = s.find("Components")
    kind = "other"; bound = ""
    if comp is not None:
        for c in comp:
            if c.tag == "Town":
                kind = "castle" if c.get("is_castle") == "true" else "town"
            elif c.tag == "Village":
                kind = "village"; bound = (c.get("bound") or "").replace("Settlement.", "")
            elif c.tag == "Hideout":
                kind = "hideout"
            else:
                kind = c.tag
    sets.append(dict(id=s.get("id"), name=nm(s.get("name")), x=float(s.get("posX") or 0), y=float(s.get("posY") or 0),
                     culture=(s.get("culture") or "").replace("Culture.", ""), owner=(s.get("owner") or "").replace("Faction.", ""),
                     kind=kind, bound=bound, text=nm(s.get("text"))[:300]))
json.dump(sets, open(os.path.join(OUT, "rot_settlements.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
raw = open(M + r"\ROT-Content\ModuleData\spclans.xml", encoding="utf-8").read()
clans = []
for m in re.finditer(r"<Faction\s([^>]*)>", raw):
    a = dict(re.findall(r'(\w+)="([^"]*)"', m.group(1)))
    clans.append(dict(id=a.get("id"), name=nm(a.get("name")), culture=a.get("culture", "").replace("Culture.", ""),
                      home=a.get("initial_home_settlement", "").replace("Settlement.", ""), super=a.get("super_faction", "").replace("Kingdom.", ""),
                      minor=a.get("is_minor_faction", ""), tier=a.get("tier", ""), owner=a.get("owner", "")))
json.dump(clans, open(os.path.join(OUT, "rot_clans.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=0)
from collections import Counter
print(len(sets), Counter(s["kind"] for s in sets))
print(len(clans))
print(Counter(s["culture"] for s in sets if s["kind"] in ("town","castle")))
