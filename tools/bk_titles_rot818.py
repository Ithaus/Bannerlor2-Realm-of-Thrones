"""Plik tytulow BannerKings dla mapy Realm of Thrones 8.1.8 (audyt modow 14.10, AM-27 / AM-04; pakiet P1).

Uzycie (Python 3, gre tylko czyta):
    python -I tools/bk_titles_rot818.py [<katalog Modules gry>]
Pisze CrashScribe/res/bk_titles_rot818.xml (CRLF; zasob DLL CrashScribe, zapisywany w grze do Documents\\...\\CrashScribe)
i raport na ekran. Kod wyjscia 1, gdy kontrola koncowa nie przejdzie (plik wtedy NIE jest zapisywany).

Zrodla (tylko odczyt):
  BKROTPatch/ModuleData/titles_6_3.xml   - plik, ktory BKROTPatch wybiera dla map z ROT_town31 (baza; kolejnosc i prawa zostaja)
  ROT-Map/ModuleData/settlements.xml     - wszystkie twierdze mapy 8.1.8 (ROT-Map/settlements.xslt kasuje osady gry), wlasciciel i polozenie
  ROT-Content/ModuleData/spclans.xml     - rod -> krolestwo startowe (super_faction) i glowa rodu (owner); zapas SandBox/spclans.xml
  ROT-Content/ModuleData/ROT_spkingdoms.xml, spkingdoms.xslt, kingdoms.xslt - krolestwa ROT i ich wladcy (nord = Lorath); zapas SandBox
  ROT-Content/ModuleData/ROT_heroes.xml  - bohaterowie (heroes.xslt kasuje bohaterow gry) i ich rody
  BannerKings.Redux/ModuleData/BKData/bk_*.xml - dozwolone id praw (rzad, sukcesja, dziedziczenie, plec)

Reguly (kazda zmiana wobec titles_6_3 wypisana w raporcie):
  R1 krolestwo faction="lorath" -> "nord" (Lorath w ROT 8.1.8 to Kingdom nord, kingdoms.xslt; "lorath" nie istnieje -> BKROT robil 'Unknown Kingdom').
  R2 osady spoza mapy wypadaja (baronie-widma ROT_castle1-14, 43, 48), takze barony wprost pod krolestwem (BK czyta tylko duchy);
     osada wpisana drugi raz wypada (ROT_castle47 dwa razy w Tyrosh - BK robil dwa tytuly jednej twierdzy).
  R3 baronia dalej niz BARONY_FAR od swojego hrabstwa (ok. 3 x mediana 63 jednostek mapy) idzie do najblizszego hrabstwa krolestwa startowego
     zamku - stare numery ROT 6.x (np. Ironrath castle_EN1 wpisany pod Maidenpool, Deep Den pod Barrowton).
  R4 twierdza bez tytulu (14 miast, 11 zamkow): krolestwo = krolestwo startowe wlasciciela (settlements.xml -> spclans super_faction);
     miasto -> ksiestwo najblizszego hrabstwa tego krolestwa (nowe ksiestwo o nazwie krolestwa, gdy krolestwo nie ma ksiestwa albo hrabstwo
     jest dalej niz TOWN_FAR); zamek -> baronia najblizszego hrabstwa tego krolestwa (po dopisaniu miast).
  R5 krolestwo ROT bez wpisu (ibb - Ibben, qarth - Qarth) dostaje tytul krolewski: Feudal / Hereditary / Primogeniture / Cognatic - jak Sarnor
     w pliku BKROT (krolestwo Essos, wladca ROT "King"); posiadacz de jure = wladca z ROT.
  R6 deJure: hrabstwo / baronia - glowa rodu wlasciciela startowego, gdy bohatera z pliku nie ma albo jest z innego rodu niz wlasciciel
     (stare id ROT 6.x: np. ROTlord_1 to dzis Duram bar Emmon); ksiestwo - deJure jego pierwszego hrabstwa, gdy bohatera nie ma albo jest
     z innego krolestwa i nie trzyma zadnego hrabstwa tego ksiestwa; krolestwo - wladca z ROT, gdy bohatera nie ma albo jest z innego krolestwa.
  R7 id praw musza istniec w BK (GetById zwraca null -> kontrakt bez sukcesji, NRE w IsKingSelectionDecisionAllowed BK): sukcesja "Tribal"
     (Summer Isles) -> "TribalElective" jak pozostale krolestwa Tribal w pliku.
Kontrola koncowa: kazde z 97 miast dokladnie raz jako hrabstwo, kazdy ze 130 zamkow dokladnie raz jako baronia, zadnej innej osady;
kazde krolestwo ROT ma wpis; kazdy deJure istnieje; nazwy ksiestw niepowtarzalne (BK RetrofitOrphanedTitles szuka ksiestw po nazwie);
id praw istnieja w BK; deklaracja XML pierwsza, <base> drugi, <titles> pierwszy w <base> (BK: ChildNodes[1].ChildNodes[0]).
Wsie: BK zaklada tytul wsi (lordship) przy kazdej twierdzy z tytulu (TitleGenerator.CreateLandedTitle -> BoundVillages) - plik ich nie wymienia.
"""
import math
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.stdout.reconfigure(encoding="utf-8")

GAME = sys.argv[1] if len(sys.argv) > 1 else r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "CrashScribe", "res", "bk_titles_rot818.xml")
BARONY_FAR = 200.0
TOWN_FAR = 250.0
NEW_KINGDOM_LAWS = dict(government="Feudal", succession="Hereditary", inheritance="Primogeniture", genderLaw="Cognatic")
LAW_ATTR = (("government", "bk_governments.xml", "government"), ("succession", "bk_successions.xml", "succession"),
            ("inheritance", "bk_inheritances.xml", "inheritance"), ("genderLaw", "bk_gender_laws.xml", "gender_law"))
LAW_FIX = {("succession", "Tribal"): "TribalElective"}


def p(*parts):
    return os.path.join(GAME, *parts)


def plain(name):
    return re.sub(r"\{=[^}]*\}", "", name or "").strip()


# ---------------------------------------------------------------- dane ROT
sett = {}
for s in ET.parse(p("ROT-Map", "ModuleData", "settlements.xml")).getroot().iter("Settlement"):
    kind = None
    for x in s.iter():
        if x.tag == "Town":
            kind = "castle" if x.get("is_castle") == "true" else "town"
        elif x.tag == "Village" and kind is None:
            kind = "village"
    sett[s.get("id")] = dict(kind=kind, owner=(s.get("owner") or "").replace("Faction.", ""), name=plain(s.get("name")),
                             x=float(s.get("posX") or 0), y=float(s.get("posY") or 0))
towns = sorted(k for k, v in sett.items() if v["kind"] == "town")
castles = sorted(k for k, v in sett.items() if v["kind"] == "castle")

clans = {}
for fn in (p("SandBox", "ModuleData", "spclans.xml"), p("ROT-Content", "ModuleData", "spclans.xml")):
    for f in ET.parse(fn).getroot().iter("Faction"):
        clans[f.get("id")] = dict(kingdom=(f.get("super_faction") or "").replace("Kingdom.", ""), leader=(f.get("owner") or "").replace("Hero.", ""))

kingdoms = {}
for fn in (p("SandBox", "ModuleData", "spkingdoms.xml"), p("ROT-Content", "ModuleData", "spkingdoms.xslt"),
           p("ROT-Content", "ModuleData", "kingdoms.xslt"), p("ROT-Content", "ModuleData", "ROT_spkingdoms.xml")):
    txt = open(fn, encoding="utf-8-sig").read()
    for m in re.finditer(r"<Kingdom\s+id=\"([^\"]+)\"([^>]*)>", txt):
        attrs = m.group(2)
        o = re.search(r"owner=\"Hero\.([^\"]+)\"", attrs)
        n = re.search(r"\sname=\"([^\"]+)\"", attrs)
        kingdoms[m.group(1)] = dict(owner=o.group(1) if o else None, name=plain(n.group(1).replace("{{", "{").replace("}}", "}")) if n else m.group(1))
# krolestwa w grze = te, do ktorych naleza rody (ROT kasuje czesc krolestw gry przez xslt? - nie, nadpisuje; licze z rodow i z ROT)
rot_kingdoms = sorted(set(c["kingdom"] for c in clans.values() if c["kingdom"]) & set(kingdoms))

heroes = {}
for h in ET.parse(p("ROT-Content", "ModuleData", "ROT_heroes.xml")).getroot().iter("Hero"):
    heroes[h.get("id")] = dict(clan=(h.get("faction") or "").replace("Faction.", ""), alive=h.get("alive") != "false")

laws = {}
for attr, fn, tag in LAW_ATTR:
    txt = open(p("BannerKings.Redux", "ModuleData", "BKData", fn), encoding="utf-8-sig").read()
    laws[attr] = set(m.group(1) for m in re.finditer(r"<" + tag + r"\s+id=\"([A-Za-z]\w*)\"", txt))


def start_kingdom(sid):
    c = clans.get(sett[sid]["owner"])
    return c["kingdom"] if c else None


def owner_leader(sid):
    c = clans.get(sett[sid]["owner"])
    return c["leader"] if c else None


def hero_kingdom(hid):
    h = heroes.get(hid)
    if not h:
        return None
    c = clans.get(h["clan"])
    return c["kingdom"] if c else None


def dist(a, b):
    return math.hypot(sett[a]["x"] - sett[b]["x"], sett[a]["y"] - sett[b]["y"])


# ---------------------------------------------------------------- baza: titles_6_3.xml
src = p("BKROTPatch", "ModuleData", "titles_6_3.xml")
root = ET.parse(src).getroot()
titles = root.find("titles")
log = []


def kid(k):
    return k.get("faction")


# R1
for k in titles.findall("kingdom"):
    if k.get("faction") == "lorath":
        k.set("faction", "nord")
        log.append("R1 krolestwo faction lorath -> nord (Lorath)")

# R2
seen = set()
for k in titles.findall("kingdom"):
    for b in list(k.findall("barony")):
        k.remove(b)
        log.append(f"R2 barony {b.get('settlement')} wprost pod krolestwem {kid(k)} - usunieta (BK jej nie czyta)")
    for d in k.findall("duchy"):
        for c in list(d.findall("county")):
            s = c.get("settlement")
            if s not in sett or sett[s]["kind"] != "town" or s in seen:
                d.remove(c)
                log.append(f"R2 county {s} usuniete ({'brak na mapie' if s not in sett else ('nie miasto' if sett[s]['kind'] != 'town' else 'drugi wpis')})")
                continue
            seen.add(s)
            for b in list(c.findall("barony")):
                bs = b.get("settlement")
                if bs not in sett or sett[bs]["kind"] != "castle" or bs in seen:
                    c.remove(b)
                    log.append(f"R2 barony {bs} w {s} usunieta ({'brak na mapie' if bs not in sett else ('nie zamek' if sett[bs]['kind'] != 'castle' else 'drugi wpis')})")
                    continue
                seen.add(bs)


def counties(kingdom=None):
    for k in titles.findall("kingdom"):
        if kingdom is not None and kid(k) != kingdom:
            continue
        for d in k.findall("duchy"):
            for c in d.findall("county"):
                yield k, d, c


def nearest_county(sid, kingdom):
    best = None
    for k, d, c in counties(kingdom):
        dd = dist(sid, c.get("settlement"))
        if best is None or dd < best[0]:
            best = (dd, k, d, c)
    return best


# R3
for k, d, c in list(counties()):
    for b in list(c.findall("barony")):
        bs = b.get("settlement")
        far = dist(bs, c.get("settlement"))
        if far <= BARONY_FAR:
            continue
        sk = start_kingdom(bs)
        best = nearest_county(bs, sk)
        if best is None or best[3] is c:
            continue
        c.remove(b)
        best[3].append(b)
        log.append(f"R3 barony {bs} {sett[bs]['name']}: {far:.0f} od {c.get('settlement')} {sett[c.get('settlement')]['name']} ({kid(k)}/{d.get('name')})"
                   f" -> {best[3].get('settlement')} {sett[best[3].get('settlement')]['name']} ({kid(best[1])}/{best[2].get('name')}, {best[0]:.0f})")

# R5 (przed R4 - nowe krolestwa musza istniec, zanim dopniemy ich miasta)
present = set(kid(k) for k in titles.findall("kingdom"))
for kk in rot_kingdoms:
    if kk in present:
        continue
    ke = ET.SubElement(titles, "kingdom")
    ke.set("faction", kk)
    for a, v in NEW_KINGDOM_LAWS.items():
        ke.set(a, v)
    ke.set("deJure", kingdoms[kk]["owner"] or "")
    log.append(f"R5 nowe krolestwo {kk} ({kingdoms[kk]['name']}): {'/'.join(NEW_KINGDOM_LAWS.values())}, deJure {kingdoms[kk]['owner']}")


def kingdom_el(kk):
    for k in titles.findall("kingdom"):
        if kid(k) == kk:
            return k
    return None


def new_county(sid):
    c = ET.Element("county")
    c.set("settlement", sid)
    c.set("deJure", owner_leader(sid) or "")
    return c


# R4: najpierw miasta, potem zamki
seen = set(c.get("settlement") for _, _, c in counties()) | set(b.get("settlement") for _, _, c in counties() for b in c.findall("barony"))
for sid in [t for t in towns if t not in seen]:
    sk = start_kingdom(sid)
    ke = kingdom_el(sk)
    if ke is None:
        print(f"BLAD: miasto {sid} - krolestwo startowe {sk} bez wpisu")
        sys.exit(1)
    best = nearest_county(sid, sk)
    if best is not None and best[0] <= TOWN_FAR:
        best[2].append(new_county(sid))
        log.append(f"R4 miasto {sid} {sett[sid]['name']} ({sk}) -> ksiestwo {best[2].get('name')} (najblizsze hrabstwo {best[3].get('settlement')}"
                   f" {sett[best[3].get('settlement')]['name']}, {best[0]:.0f})")
    else:
        dn = kingdoms[sk]["name"]
        d = None
        for dd in ke.findall("duchy"):
            if dd.get("name") == dn:
                d = dd
        if d is None:
            d = ET.SubElement(ke, "duchy")
            d.set("name", dn)
            d.set("deJure", owner_leader(sid) or "")
            log.append(f"R4 nowe ksiestwo {dn} w {sk} (deJure {owner_leader(sid)})")
        d.append(new_county(sid))
        log.append(f"R4 miasto {sid} {sett[sid]['name']} ({sk}) -> ksiestwo {dn}" + (f" (najblizsze hrabstwo krolestwa {best[0]:.0f})" if best else " (krolestwo bez hrabstw)"))
    seen.add(sid)
for sid in [c for c in castles if c not in seen]:
    sk = start_kingdom(sid)
    best = nearest_county(sid, sk)
    if best is None:
        print(f"BLAD: zamek {sid} - krolestwo startowe {sk} bez hrabstwa")
        sys.exit(1)
    b = ET.SubElement(best[3], "barony")
    b.set("settlement", sid)
    b.set("deJure", owner_leader(sid) or "")
    log.append(f"R4 zamek {sid} {sett[sid]['name']} ({sk}) -> hrabstwo {best[3].get('settlement')} {sett[best[3].get('settlement')]['name']}"
               f" (ksiestwo {best[2].get('name')}, {best[0]:.0f})")
    seen.add(sid)

# R6
for k in titles.findall("kingdom"):
    kk = kid(k)
    for d in k.findall("duchy"):
        for c in d.findall("county"):
            for el in [c] + c.findall("barony"):
                s, h = el.get("settlement"), el.get("deJure")
                want = owner_leader(s)
                hh = heroes.get(h)
                if want and (hh is None or hh["clan"] != sett[s]["owner"]) and want != h:
                    el.set("deJure", want)
                    log.append(f"R6 {el.tag} {s} {sett[s]['name']}: deJure {h} ({'brak' if hh is None else hh['clan']}) -> {want} (glowa {sett[s]['owner']})")
        cs = d.findall("county")
        h = d.get("deJure")
        holders = set(c.get("deJure") for c in cs)
        if cs and (h not in heroes or (hero_kingdom(h) != kk and h not in holders)):
            want = cs[0].get("deJure")
            d.set("deJure", want)
            log.append(f"R6 duchy {d.get('name')}: deJure {h} ({'brak' if h not in heroes else hero_kingdom(h)}) -> {want} (pierwsze hrabstwo {cs[0].get('settlement')})")
    h = k.get("deJure")
    want = kingdoms.get(kk, {}).get("owner")
    if want and (h not in heroes or hero_kingdom(h) != kk):
        k.set("deJure", want)
        log.append(f"R6 kingdom {kk}: deJure {h} ({'brak' if h not in heroes else hero_kingdom(h)}) -> {want} (wladca ROT)")

# R7
for k in titles.findall("kingdom"):
    for attr, _, _ in LAW_ATTR:
        v = k.get(attr)
        if v not in laws[attr] and (attr, v) in LAW_FIX:
            k.set(attr, LAW_FIX[(attr, v)])
            log.append(f"R7 kingdom {kid(k)}: {attr} {v} (brak w BK) -> {LAW_FIX[(attr, v)]}")

# ---------------------------------------------------------------- kontrola koncowa
err = []
cnt = {}
for k, d, c in counties():
    cnt.setdefault(c.get("settlement"), []).append("county")
    for b in c.findall("barony"):
        cnt.setdefault(b.get("settlement"), []).append("barony")
for t in towns:
    if cnt.get(t) != ["county"]:
        err.append(f"miasto {t}: {cnt.get(t)}")
for c in castles:
    if cnt.get(c) != ["barony"]:
        err.append(f"zamek {c}: {cnt.get(c)}")
for s in cnt:
    if s not in sett or sett[s]["kind"] not in ("town", "castle"):
        err.append(f"osada spoza mapy {s}")
kset = [kid(k) for k in titles.findall("kingdom")]
for kk in rot_kingdoms:
    if kset.count(kk) != 1:
        err.append(f"krolestwo {kk}: wpisow {kset.count(kk)}")
for kk in kset:
    if kk not in rot_kingdoms:
        err.append(f"krolestwo z pliku spoza ROT {kk}")
dn = [d.get("name") for k in titles.findall("kingdom") for d in k.findall("duchy")]
for n in set(dn):
    if dn.count(n) > 1:
        err.append(f"ksiestwo {n} {dn.count(n)} razy")
for el in titles.iter():
    if el.tag in ("kingdom", "duchy", "county", "barony") and el.get("deJure") not in heroes:
        err.append(f"{el.tag} {el.get('settlement') or el.get('faction') or el.get('name')}: deJure {el.get('deJure')} nie istnieje")
    if el.tag == "duchy" and not el.findall("county"):
        err.append(f"ksiestwo {el.get('name')} bez hrabstw")
for k in titles.findall("kingdom"):
    for attr, _, _ in LAW_ATTR:
        if k.get(attr) not in laws[attr]:
            err.append(f"krolestwo {kid(k)}: {attr} {k.get(attr)} nie istnieje w BK")
    if not k.findall("duchy"):
        err.append(f"krolestwo {kid(k)} bez ksiestw")


# ---------------------------------------------------------------- zapis (recznie: kolejnosc atrybutow i wciecia jak w pliku BKROT)
def esc(v):
    return v.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


def attrs(el, order):
    keys = [a for a in order if a in el.attrib] + [a for a in el.attrib if a not in order]
    return " ".join(f'{a}="{esc(el.get(a))}"' for a in keys)


lines = ['<?xml version="1.0" encoding="utf-8"?>',
         '<base xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" type="string">',
         '\t<titles autoGenerate="false">',
         "\t\t<!-- CrashScribe: BannerKings titles for the Realm of Thrones 8.1.8 map. Generated by tools/bk_titles_rot818.py from"
         " BKROTPatch titles_6_3.xml - see the rules in that script. Every town is a county and every castle a barony, exactly once."
         " Do not edit by hand. -->"]
KORD = ("faction", "name", "government", "succession", "inheritance", "genderLaw", "deJure")
for k in titles.findall("kingdom"):
    lines.append(f"\t\t<kingdom {attrs(k, KORD)}>")
    for d in k.findall("duchy"):
        lines.append(f"\t\t\t<duchy {attrs(d, ('name', 'deJure'))}>")
        for c in d.findall("county"):
            bs = c.findall("barony")
            if not bs:
                lines.append(f"\t\t\t\t<county {attrs(c, ('settlement', 'deJure'))} />")
                continue
            lines.append(f"\t\t\t\t<county {attrs(c, ('settlement', 'deJure'))}>")
            for b in bs:
                lines.append(f"\t\t\t\t\t<barony {attrs(b, ('settlement', 'deJure'))} />")
            lines.append("\t\t\t\t</county>")
        lines.append("\t\t\t</duchy>")
    lines.append("\t\t</kingdom>")
lines += ["\t</titles>", "</base>", ""]
text = "\r\n".join(lines)

# ta sama droga co BK: deklaracja, <base>, <titles> pierwszy
try:
    chk = ET.fromstring(text.encode("utf-8"))
    if chk.tag != "base" or chk[0].tag != "titles" or chk[0].get("autoGenerate") != "false":
        err.append("uklad pliku: <base>/<titles> nie na miejscu")
except ET.ParseError as e:
    err.append(f"XML: {e}")
if not text.startswith("<?xml"):
    err.append("brak deklaracji XML (BK czyta ChildNodes[1])")

print(f"gra: {GAME}")
print(f"mapa ROT: miast {len(towns)}, zamkow {len(castles)}; krolestw ROT {len(rot_kingdoms)}; bohaterow {len(heroes)}")
for line in log:
    print("  " + line)
n_dukes = sum(1 for k in titles.findall("kingdom") for d in k.findall("duchy"))
print(f"wynik: krolestw {len(kset)}, ksiestw {n_dukes}, hrabstw {sum(1 for _ in counties())}, baronii "
      f"{sum(len(c.findall('barony')) for _, _, c in counties())}; zmian {len(log)}")
if err:
    print("KONTROLA NIE PRZESZLA:")
    for e in err:
        print("  " + e)
    sys.exit(1)
os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "wb") as f:
    f.write(text.encode("utf-8"))
print(f"kontrola: TAK (twierdz bez tytulu 0 z {len(towns) + len(castles)}, krolestw bez tytulu 0 z {len(rot_kingdoms)}); zapisano {OUT} ({len(text.encode('utf-8'))} B)")
