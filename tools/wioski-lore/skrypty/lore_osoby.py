# E7/L P1: osoby z ksiazek, ktorych imie nie moze trafic do rycerza wioski - wspolne dla generator.py (dobor) i l0.py (kontrola).
import csv, re

# osoby, ktore nie moga trzymac wioski w 299 AC (WL-P 3.2: martwe, Nocna Straz, Gwardia Krolewska, glowne postacie)
FAMOUS = {"martyn cassel", "rodrik cassel", "jory cassel", "ambrose butterwell", "petyr baelish", "raymun fossoway", "symon hollard",
          "dontos hollard", "luthor largent", "jarman buckwell", "oswald wode", "rupert falwell", "bowen marsh", "othell yarwyck",
          "ottyn wythers", "meryn trant", "boros blount", "ilyn payne", "podrick payne", "jeyne poole", "vayon poole", "areo hotah",
          "tobho mott", "hizdahr loraq", "skahaz kandaq", "galazza galare", "vardis egen", "jeyne arryn", "lymond goodbrook",
          "shella whent", "illyrio mopatis"}
# imiona glownych postaci Essos (i khalow / krwawych jezdzcow) - nigdzie jako imie rycerza
MAIN_GIVEN = {"hizdahr", "skahaz", "kraznys", "galazza", "reznak", "oznak", "yurkhaz", "yezzan", "grazdan", "illyrio", "xaro",
              "pyat", "quaithe", "jaqen", "drogo", "qotho", "haggo", "cohollo", "rakharo", "aggo", "jhogo", "tycho", "tobho"}


def surname_of(rod):
    n = re.sub(r"^House (of )?", "", rod)
    return re.sub(r"\s*\(.*?\)", "", n).split(" of ")[0].strip()


def lore_persons(rody):
    """Zestawy (imie, nazwisko) male litery z list czlonkow rodow lore (rody.csv: "imiona"; Straz: "Jeor_Mormont(...)")."""
    out = set()
    for r in rody:
        sur = surname_of(r["rod"]).lower()
        for tok in r["imiona"].split():
            if "_" in tok:
                g, rest = tok.split("_", 1)
                out.add((g.lower(), re.sub(r"\(.*", "", rest).lower()))
            elif "(" not in tok:
                out.add((tok.lower(), sur))
    return out


def read_rody(path):
    return list(csv.DictReader(open(path, encoding="utf-8-sig"), delimiter=";"))
