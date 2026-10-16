# Generator rodow i rycerzy wiosek w stylu krainy (deterministyczny, bez kolizji z rodami lore / klanami ROT / osadami ROT)
# + podglad przydzialu: kazda wioska z arm_map_villages.tsv dostaje trzymajacego (rod lore albo wymyslony).
# Do przeniesienia do C#: hash FNV-1a 32 bit na bajtach UTF-8, wszystkie listy w tej samej kolejnosci.
# E7/L (12.10): P1 imie rycerza rodu lore z listy krainy (bez zestawow z lore i bez glownych postaci), P3 herb wymyslony unikalny
# w krainie (jeden hash na pole / barwe / godlo), P4 herb Essos (barwy kultury ROT + godlo) i kod herbu gry tylko dla godel
# sprawdzonych w danych gry, P5 linia "Kept for them by {KNIGHT}" (kod moda), P12 plik ukladu z argumentu.
# Wynik: rycerze.tsv (posredni, czyta go l0.py). Uzycie: python generator.py <arm_map_villages.tsv> <katalog modulow gry>
import csv, json, math, os, re, sys
import xml.etree.ElementTree as ET
from collections import defaultdict, Counter

B = os.path.dirname(os.path.abspath(__file__))
L = os.path.dirname(B)
VILLAGES_TSV = sys.argv[1] if len(sys.argv) > 1 else os.path.join(L, "..", "..", "Armoury", "ModuleData", "arm_map_villages_v4.tsv")   # Jeff 16.10: lore z ukladu v4 (naglowek l0.py)
GAME = sys.argv[2] if len(sys.argv) > 2 else r"C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules"

def fnv1a(s):
    h = 2166136261
    for b in s.encode("utf-8"):
        h ^= b
        h = (h * 16777619) & 0xFFFFFFFF
    return h

def pick(lst, seed, salt):
    return lst[fnv1a(seed + "|" + salt) % len(lst)]

# ---------------- style krain ----------------
# pre + suf = nazwa rodu (sklejana: "Frost"+"holt" = "Frostholt"); given = imiona (z lore krainy, bez glownych postaci)
ST = {
 "north": dict(pre=["Frost", "Stone", "Grey", "Black", "Pine", "Moss", "Cairn", "Barrow", "Snow", "Ice", "Hollow", "Thorn", "Bark",
                    "Elk", "Raven", "Wolf", "Rime", "Birch", "Heath", "Flint", "Crag", "Bleak", "Hoar", "Fell"],
               suf=["holt", "wood", "ford", "hill", "ley", "barrow", "mere", "stead", "crag", "fell", "shaw", "brook", "moor", "beck"],
               given=["Harwood", "Torrhen", "Cregan", "Rodrik", "Edric", "Wylis", "Alaric", "Beron", "Torghen", "Hother", "Medger",
                      "Cley", "Harlon", "Brandon", "Donnel", "Ronnel", "Halys", "Daryn", "Galbart", "Robett", "Ondrew", "Mallador",
                      "Wynton", "Benfred", "Leobald", "Helman", "Artos", "Osric", "Rickard", "Jonnel", "Willam", "Theon", "Lothor", "Hugo"],
               lady=["Maege", "Alysane", "Barbrey", "Lyessa", "Eddara", "Berena", "Donella", "Jonelle", "Sybelle"],
               title="", sworn="sworn to House {lord}",
               charge=["a wolf's head", "a pine tree", "an elk", "a raven", "a bear's paw", "a broken axe", "a cairn", "a star",
                       "a stag's skull", "a hunting horn", "a crossed pair of spears", "a tower"]),
 "river": dict(pre=["Rush", "Willow", "Reed", "Heron", "Trout", "Mill", "Weir", "Kettle", "Bridge", "Fen", "Ash", "Elder",
                    "Otter", "Pike", "Marsh", "Oxen", "Barley", "Holly", "Moat", "Wheat", "Mud", "Cress"],
               suf=["ford", "wood", "ley", "brook", "well", "more", "ton", "stone", "wick", "field", "bank", "mere", "ham", "hall"],
               given=["Lymond", "Patrek", "Jonos", "Tytos", "Desmond", "Lyle", "Wendel", "Jared", "Willem", "Damon", "Raymun", "Theomar",
                      "Karyl", "Norbert", "Ronnel", "Armistead", "Clement", "Marq", "Lothar", "Humfrey", "Otho", "Hendry", "Joseth",
                      "Jason", "Lucas", "Richard", "Garse", "Oswald", "Willis", "Harbert", "Garrett", "Halmon", "Quincy", "Tristan"],
               lady=["Jeyne", "Bethany", "Ravella", "Carellen", "Liane", "Melony", "Shella", "Minisa", "Alysanne"],
               title="Ser ", sworn="sworn to House {lord}",
               charge=["a leaping fish", "a heron", "a mill wheel", "a willow", "a plowshare", "a crossed pair of oars",
                       "a bridge of three arches", "an otter", "a sheaf of wheat", "a kettle", "a hedgehog", "a toad"]),
 "vale": dict(pre=["High", "Hawk", "Moon", "Crag", "Eagle", "Rune", "Gull", "Cloud", "Ridge", "Falcon", "Grey", "Sky", "Bronze",
                   "Snow", "Goat", "Rock", "Wind", "Bell"],
              suf=["crest", "cliff", "more", "holt", "horn", "ridge", "stone", "fall", "gate", "hall", "dale", "peak", "spire", "vale"],
              given=["Andar", "Yohn", "Lyonel", "Morton", "Harlan", "Eon", "Symond", "Jasper", "Uthor", "Mychel", "Robar", "Gilwood",
                     "Benedar", "Godric", "Gerold", "Gyles", "Horton", "Creighton", "Edmund", "Damon", "Vardis", "Lyn", "Wallace",
                     "Donnel", "Roland", "Eustace", "Marwyn", "Triston", "Rolland", "Dunstan", "Alesandor", "Uther", "Ossifer"],
              lady=["Anya", "Rhea", "Ryella", "Jessamyn", "Teora", "Janyce", "Myranda", "Marsella"],
              title="Ser ", sworn="sworn to House {lord}",
              charge=["a falcon", "a crescent moon", "a mountain goat", "a bell", "a tower on a crag", "a crab",
                      "an eagle's wing", "a rune-stone", "a gull", "a pair of crossed arrows", "a mountain", "a star"]),
 "west": dict(pre=["Gold", "Red", "Copper", "Brass", "Garnet", "Lion", "Ore", "Crimson", "Amber", "Rock", "Iron", "Silver",
                   "Tin", "Flint", "Bright", "Deep", "Hill", "Lantern"],
              suf=["rock", "crag", "mark", "field", "ford", "spear", "stone", "hall", "vale", "den", "mount", "pike", "brook", "cliff"],
              given=["Tybolt", "Lyle", "Merlon", "Addam", "Damon", "Lorent", "Flement", "Garth", "Lymond", "Daven", "Aubrey", "Clarent",
                     "Sumner", "Burton", "Gawen", "Rollam", "Raynald", "Harys", "Steffon", "Humfrey", "Andros", "Rupert", "Quenten",
                     "Morgon", "Sebaston", "Gylbert", "Androw", "Regenard", "Antario", "Benedict", "Lewys", "Selmond", "Philip", "Forley"],
              lady=["Jeyne", "Johanna", "Eleyna", "Cerissa", "Shierle", "Dorna", "Melesa", "Elissa", "Darlessa"],
              title="Ser ", sworn="sworn to House {lord}",
              charge=["a lion's paw", "a pick", "a mountain", "a coin", "a boar", "a helm", "a sunburst",
                      "a crossed pair of picks", "a seashell", "a badger", "a round tower", "a spear"]),
 "reach": dict(pre=["Rose", "Green", "Apple", "Honey", "Vine", "Meadow", "Bee", "Oak", "Plum", "Flower", "Golden", "Bramble",
                    "Pear", "Clover", "Barley", "Lark", "Hart", "Briar", "Sweet", "Fair"],
               suf=["field", "wood", "grove", "bury", "ford", "worth", "dale", "vale", "garden", "ley", "mead", "orchard", "bank", "well"],
               given=["Alester", "Garlan", "Willas", "Mathis", "Leyton", "Baelor", "Owen", "Raymun", "Steffon", "Orton", "Lorent",
                      "Talbert", "Osbert", "Arwyn", "Edgerran", "Torgen", "Arthor", "Gwayne", "Thaddeus", "Nyles", "Moribald", "Guthor",
                      "Glendon", "Elwood", "Warryn", "Braxton", "Lyman", "Hugh", "Martyn", "Buford", "Branston", "Igon", "Reynard", "Wendell"],
               lady=["Alerie", "Lynesse", "Rhonda", "Melara", "Elinor", "Leonette", "Janna", "Rohanne", "Beony"],
               title="Ser ", sworn="sworn to House {lord}",
               charge=["a rose", "an apple", "a beehive", "a sheaf of wheat", "a bunch of grapes", "an oak leaf", "a lark",
                       "a plough", "a pear", "a hart", "a flower", "a hunting horn"]),
 "storm": dict(pre=["Storm", "Rain", "Gale", "Thunder", "Crow", "Wind", "Cliff", "Elder", "Hawk", "Black", "Mist", "Gull", "Wrath",
                    "Tide", "Stag", "Grey", "Dusk", "Ember"],
               suf=["wood", "end", "cliff", "hall", "nest", "ford", "haven", "watch", "mont", "holt", "stone", "brake", "point", "march"],
               given=["Lester", "Bryce", "Ronnet", "Gulian", "Selwyn", "Harmon", "Cortnay", "Ralph", "Brus", "Morton", "Pearse", "Bryen",
                      "Royce", "Simon", "Manfred", "Sebastion", "Eldon", "Lomas", "Willis", "Thurgood", "Harwood", "Casper", "Gladden",
                      "Ormund", "Lorent", "Narbert", "Robin", "Jon", "Clifford", "Donnel", "Byron", "Bonifer", "Herbert", "Michael"],
               lady=["Shyra", "Cassana", "Coryanne", "Tyana", "Jena", "Mylenda", "Ellyn", "Johanna", "Cyrenna"],
               title="Ser ", sworn="sworn to House {lord}",
               charge=["a stag's head", "a crow", "a lightning bolt", "an owl", "a turtle", "a storm cloud", "a crossed pair of swords",
                       "a tower in a gale", "a seahorse", "a nightingale", "a broken anchor", "a crescent"]),
 "crown": dict(pre=["Crown", "King", "Black", "Wend", "Rook", "Dusk", "Salt", "Gull", "Hart", "Briar", "Wood", "Hollow",
                    "Bay", "River", "Wheat", "Fox", "Rush", "Heron"],
               suf=["water", "well", "ford", "wood", "hollow", "point", "haven", "brook", "field", "wick", "mere", "dell", "cross", "row"],
               given=["Bennard", "Jacelyn", "Rennifer", "Renfred", "Rayford", "Gyles", "Symon", "Dontos", "Raymund", "Lothar", "Qarlton",
                      "Balman", "Denys", "Elwood", "Perkin", "Gwayne", "Harrold", "Howard", "Herman", "Lucos", "Jarman", "Hubard",
                      "Clarence", "Clement", "Mortimer", "Eustace", "Bernarr", "Lothor", "Maldon", "Triston", "Sefton", "Duram", "Monford"],
               lady=["Ermesande", "Lyra", "Falyse", "Tanda", "Lollys", "Hazel", "Ysabel", "Cassella", "Priscella"],
               title="Ser ", sworn="sworn to House {lord}",
               charge=["a crown", "a boar's head", "a heron", "a fox", "a sheaf of wheat", "a crossed pair of hammers", "a lamb",
                       "a stag's antler", "a fish", "a tower", "a rook", "a pair of keys"]),
 "dorne": dict(pre=["Sand", "Sun", "Dune", "Spear", "Salt", "Lemon", "Scorpion", "Vulture", "Red", "Stone", "Date", "Olive",
                    "Viper", "Hawk", "Bone", "Thorn", "Orange", "Fig"],
               suf=["stone", "wood", "well", "grace", "shore", "hill", "water", "spring", "gate", "pass", "mont", "fall", "dell", "rock"],
               given=["Andrey", "Deziel", "Gerris", "Archibald", "Ryon", "Symon", "Harmen", "Uthor", "Dagos", "Mors", "Perros", "Cletus",
                      "Trebor", "Tremond", "Daeron", "Wyland", "Franklyn", "Ferris", "Garrison", "Albin", "Dickon", "Yoren", "Olyvar",
                      "Gulian", "Arron", "Ulwyck", "Edric", "Ulrick", "Vorian", "Joffrey", "Theodan", "Aron", "Anders", "Ormond"],
               lady=["Larra", "Jynessa", "Nymella", "Valena", "Teora", "Delonne", "Myria", "Cassella", "Sylva", "Alyse"],
               title="Ser ", sworn="sworn to House {lord}",
               charge=["a sun", "a scorpion", "a spear", "a vulture", "a lemon", "a viper", "a sand cat", "a falling star",
                       "a dune", "a hawk", "a skull", "an olive branch"]),
 "iron": dict(pre=["Salt", "Drowned", "Kraken", "Stone", "Iron", "Wave", "Grey", "Sea", "Storm", "Tide", "Black", "Wreck",
                   "Shark", "Brine", "Rock", "Gull", "Cliff", "Reef"],
              suf=["cliff", "house", "tree", "harbor", "wyk", "holt", "wave", "rock", "sound", "fell", "point", "keel", "spit", "skerry"],
              given=["Harras", "Rodrik", "Dagmer", "Qarl", "Ralf", "Eldred", "Torwold", "Harrag", "Gorold", "Gormond", "Dunstan",
                     "Lucas", "Maron", "Germund", "Gylbert", "Ygon", "Tristifer", "Hotho", "Sigfryd", "Dagon", "Norne", "Waldon",
                     "Alester", "Lenwood", "Donnor", "Baelor", "Beron", "Meldred", "Boremund", "Regnar", "Hilmar", "Roryn", "Adrack"],
              lady=["Gwynesse", "Gysella", "Alannys", "Sawane"],
              title="", sworn="sworn to House {lord}",
              charge=["a longship", "a kraken's arm", "a drowned man", "an axe", "a scythe", "a warhorn", "a shark", "a gull",
                      "a crossed pair of oars", "a wave", "an anchor", "a seal"]),
 "gift": dict(pre=[], suf=[], given=["Dywen", "Hake", "Bedwyck", "Dareon", "Halder", "Todder", "Ulmer", "Garth", "Mully", "Owen",
                                     "Pate", "Clydas", "Emmett", "Kedge", "Satin", "Leathers", "Hobb", "Iron", "Rast", "Spare"],
              lady=[], title="Brother ", sworn="of the Night's Watch", charge=[]),
 "freefolk": dict(pre=[], suf=[], given=["Errok", "Brogg", "Harle", "Gerrick", "Morna", "Varamyr", "Halleck", "Ragwyle", "Orell",
                                         "Soren", "Toregg", "Torwynd", "Munda", "Harma", "Styr", "Rattle", "Alfyn", "Grigg", "Ryk"],
                  lady=[], title="", sworn="who answers to {lord}", charge=[],
                  epi=["the Horn", "Shieldbreaker", "the Elk", "Bonebelt", "Frostfinger", "the Hunter", "Longspear",
                       "Wolfskin", "Ironhand", "the Pike", "Snowbeard", "Crowfoot"]),
}
# Essos: rodzina + imie z sylab (styl walyrianski / ghiscarski / dothracki)
ES = {
 "freecity": dict(a=["Mae", "Vha", "Pae", "Rho", "Lo", "Ty", "Sae", "Vo", "Nya", "Tre", "Qha", "Dha", "Ala", "Ora", "Ze", "Bel",
                     "Mo", "Fe", "Ry", "Sa"], b=["la", "ga", "re", "the", "va", "do", "ma", "ri", "no", "te", "lo", "phy"],
                  c=["ion", "aros", "arys", "is", "os", "eon", "ys", "anys", "oris", "enys"],
                  given=["Malaquo", "Nyessos", "Doniphos", "Belicho", "Moredo", "Ferrego", "Tormo", "Uthero", "Bessaro",
                         "Luco", "Sharako", "Racallio", "Collio", "Alequo", "Moreo", "Silvario", "Pereno", "Matteno", "Torreo",
                         "Bambarro", "Byan", "Doro", "Qavo", "Parquello", "Alios", "Marqelo", "Lysandro", "Drazenko",
                         "Roggerio", "Lotho", "Gessio", "Nevio", "Rego"]),   # E7/L P1: bez Illyrio, Tobho, Tycho
 "ghis": dict(a=["Gh", "Kh", "Hz", "Z", "Q", "R", "Dh", "Yh", "M", "N", "P", "Y"], b=["a", "e", "o", "aa", "ae"],
              c=["zak", "rak", "qar", "khaz", "naq", "ghaz", "raq", "lez", "zhar", "dhaz", "ryq", "zen"],
              # E7/L P1: bez Hizdahr, Oznak, Skahaz, Reznak, Grazdan, Kraznys, Yurkhaz, Yezzan (glowne postacie Zatoki)
              given=["Paezhar", "Chezdhar",
                     "Maezon", "Ghazdor", "Morghaz", "Faezhar", "Gorzhak", "Grazdar", "Grazdhan", "Marghaz", "Zhaq", "Draqaz"]),
 "qarth": dict(a=["Xa", "Xo", "Qa", "Pya", "Wen", "Ma", "Que", "Vo", "Ega", "Da"], b=["ro", "an", "re", "llo", "tho", "dra"],
               c=["xos", "xhore", "deeth", "votar", "rawan", "meros", "pree", "xhan"],
               given=["Xandarro", "Colloquo", "Egon", "Wendello", "Mathos", "Xenno", "Qoraso", "Wenthro", "Pyrello", "Daxo"]),   # E7/L P1: bez Xaro, Pyat, Quaithe, Jaqen, Sallor
 "ibben": dict(a=["Gh", "Th", "K", "G", "J", "Br", "Tr", "D"], b=["a", "o", "u"], c=["ssot", "sso", "thuk", "rok", "rag", "dok", "gor"],
               given=["Gasso", "Thuk", "Joth", "Casat", "Togg", "Grunn", "Brog", "Dusk"]),
 "yiti": dict(a=["L", "Ch", "Bu", "H", "W", "M", "T", "Y"], b=["o", "a", "u", "ei", "ao"], c=["", "n", "ng", "an", "u"],
              given=["Lo", "Lin Wei", "Shu Lan", "Hei Lun", "Bao Ren", "Mei Ling", "Tsai", "Wen Bo"]),   # E7/L P1: bez cesarzy (Bu Gai, Chai Duq, Jar Han)
 "summer": dict(a=["X", "K", "Q", "J", "Ch", "M"], b=["ho", "o", "ua", "a", "e"], c=["", "ndo", "ja", "ru", "taya", "lu"],
                given=["Xhalo", "Kezza", "Quho", "Jhala", "Mollo", "Tezza", "Bhoru", "Zhenna"]),   # E7/L P1: dawne imiona to postacie z ksiazek
 "sarnor": dict(a=["Ma", "Hu", "Ty", "Lo", "Su", "Al", "A", "Ka"], b=["z", "zh", "m", "l", "r"], c=["ai", "or", "exi", "ar", "an"],
                given=["Mazaro", "Hozai", "Tymor", "Lozar", "Sumai", "Kasar", "Mardosh", "Alzor"]),   # E7/L P1: bez krolow (Mazor Alexi, Huzhor Amai)
 "dothraki": dict(a=[], b=[], c=[], # E7/L P1: dawna lista to sami khalowie i krwawi jezdzcy z ksiazek - imiona w stylu dothrackim
                                 given=["Zhako", "Mhaqo", "Rhozzo", "Qhorro", "Jhezzo", "Hozzo", "Vhaggo", "Khorro",
                                          "Zhoqqo", "Rhazzo", "Mhezzo", "Qhaggo"]),
}
CULT_STYLE = {"battania": "north", "skagosi": "north", "river": "river", "vale": "vale", "vlandia": "west", "reach": "reach",
              "stormlands": "storm", "crownlands": "crown", "dragonstone": "crown", "aserai": "dorne", "sturgia": "iron",
              "nightswatch": "gift", "freefolk": "freefolk", "ghiscari": "ghis", "qartheen": "qarth", "khuzait": "dothraki",
              "ibbenese": "ibben", "yiti": "yiti", "summer": "summer", "sarnor": "sarnor"}
REGION_OF_CULT = {"battania": "The North", "skagosi": "The North", "river": "The Riverlands", "vale": "The Vale", "vlandia": "The Westerlands",
                  "reach": "The Reach", "stormlands": "The Stormlands", "crownlands": "The Crownlands", "dragonstone": "The Crownlands (Dragonstone)",
                  "aserai": "Dorne", "sturgia": "The Iron Islands", "nightswatch": "The Gift", "freefolk": "Beyond the Wall"}
TINCT_F = ["white", "black", "green", "blue", "red", "grey", "purple", "brown", "gold", "silver", "orange", "sky blue"]
METAL = {"gold", "silver", "white"}

def style_of(cult):
    return CULT_STYLE.get(cult, "freecity")

# ---------------- zbior zakazany (bez kolizji) ----------------
sets = json.load(open(os.path.join(B, "rot_settlements.json"), encoding="utf-8"))
clans = json.load(open(os.path.join(B, "rot_clans.json"), encoding="utf-8"))
byid = {s["id"]: s for s in sets}
cl = {c["id"]: c for c in clans}
rody = list(csv.DictReader(open(os.path.join(L, "rody.csv"), encoding="utf-8-sig"), delimiter=";"))
def key(n):
    return re.sub(r"[^a-z]", "", n.lower())
FORBID = set()
for r in rody:
    n = re.sub(r"^House (of )?", "", r["rod"]); n = re.sub(r"\s*\(.*?\)", "", n).split(" of ")[0]
    FORBID.add(key(n))
    for s_ in r["siedziba"].split(";"):
        FORBID.add(key(s_))
for c in clans:
    FORBID.add(key(c["name"].split(",")[0].split("-")[0]))
for s in sets:
    FORBID.add(key(s["name"]))
FORBID.discard("")
used_names = set()

def _combo(style, i):
    if style in ST:
        st = ST[style]; P, S_ = st["pre"], st["suf"]
        return P[i % len(P)] + S_[(i // len(P)) % len(S_)], len(P) * len(S_)
    e = ES[style]; A_, B_, C_ = e["a"], e["b"], e["c"]
    n = A_[i % len(A_)] + B_[(i // len(A_)) % len(B_)] + C_[(i // (len(A_) * len(B_))) % len(C_)]
    return n[0].upper() + n[1:].lower(), len(A_) * len(B_) * len(C_)

def gen_house(style, seed):
    """Nazwa wymyslonego rodu: start z hasha, potem kolejne kombinacje (sondowanie liniowe) - unikalna i poza FORBID."""
    if style not in ST and not ES[style]["a"]:
        return None
    if style in ST and not ST[style]["pre"]:
        return None
    _, N = _combo(style, 0)
    start = fnv1a(seed + "|house") % N
    for k in range(N):
        n, _ = _combo(style, (start + k * 7919) % N)
        kk = key(n)
        if kk in FORBID or kk in used_names:
            continue
        used_names.add(kk)
        return n
    return None

# ---------------- E7/L P1: imiona (bez osob z lore) ----------------
from lore_osoby import FAMOUS, MAIN_GIVEN, surname_of, lore_persons   # E7/L P1: wspolne z l0.py (kontrola)
LORE_PERSONS = lore_persons(rody)

def person_ok(given, sur):
    g = given.split()[0].lower()
    return (g not in MAIN_GIVEN and (g, sur.lower()) not in LORE_PERSONS
            and (given + " " + sur).lower() not in FAMOUS)

def pick_given(lst, seed, salt, sur):
    """Imie z listy krainy (hash z uid), z odrzuceniem zestawu z lore - kolejne pozycje listy (deterministycznie)."""
    if not lst:
        return None
    i0 = fnv1a(seed + "|" + salt) % len(lst)
    for k in range(len(lst)):
        g = lst[(i0 + k) % len(lst)]
        if person_ok(g, sur):
            return g
    return None

GHIS_PREFIX = {}   # nazwisko rodu Zatoki lore -> "zo" / "mo" (z nawiasu w rody.csv: "House of Loraq (zo Loraq)")
for r in rody:
    m = re.search(r"\((zo|mo) ", r["rod"])
    if m:
        GHIS_PREFIX[surname_of(r["rod"])] = m.group(1)

def gen_person(style, seed, house):
    """Rycerz rodu (wymyslonego albo lore - te same listy imion krainy). Zwraca (osoba, forma linii)."""
    if style in ST:
        st = ST[style]
        lady = st["lady"] and fnv1a(seed + "|g") % 9 == 0
        given = pick_given(st["lady"] if lady else st["given"], seed, "n", house)
        if style == "freefolk":
            return given + " " + pick(st["epi"], seed, "e"), "free"
        if style == "gift":
            return st["title"] + given, "watch"
        title = ("Lady " if lady else st["title"])
        return (title + given + " " + house).strip(), "kept"
    e = ES[style]
    given = pick_given(e["given"], seed, "n", house)
    if style == "ghis":
        return given + " " + (GHIS_PREFIX.get(house) or "zo") + " " + house, "kept"
    if style == "dothraki":
        return "Ko " + given, "tribute"
    return given + " " + house, "kept"

# ---------------- E7/L P3 + P4: herby ----------------
TINCT_HEX = {"gold": "#D4A017", "silver": "#C0C0C0", "white": "#F2F2EE", "red": "#B22222", "burgundy": "#7B1E3A", "blue": "#2F5DA8",
             "green": "#2E7D32", "black": "#1A1A1A", "purple": "#6A3D9A", "orange": "#E07B24", "brown": "#7A5230", "bronze": "#A97142",
             "sand": "#D8C38A", "grey": "#808080", "pink": "#E8A0B4", "sky blue": "#87CEEB"}   # jak build_rody.py HEX + "sky blue"

def nearest_tinct(hexrgb, allowed):
    r, g, b = int(hexrgb[0:2], 16), int(hexrgb[2:4], 16), int(hexrgb[4:6], 16)
    def d(t):
        h = TINCT_HEX[t].lstrip("#")
        return (int(h[0:2], 16) - r) ** 2 + (int(h[2:4], 16) - g) ** 2 + (int(h[4:6], 16) - b) ** 2
    return min(allowed, key=d)

# Essos: lore nie zna herbow; barwy = dwie barwy kultury ROT (spcultures.xml color / color2 -> najblizsza nazwa barwy), godlo z listy stylu
CULT_COLORS = {}
try:
    _raw = open(os.path.join(GAME, "ROT-Content", "ModuleData", "spcultures.xml"), encoding="utf-8").read()
    for _m in re.finditer(r"<Culture\s([^>]*)>", _raw):
        _a = dict(re.findall(r'(\w+)="([^"]*)"', _m.group(1)))
        cs = []
        for k in ("color", "color2"):
            v = _a.get(k) or ""
            if re.fullmatch(r"[0-9A-Fa-f]{8}", v):
                t = nearest_tinct(v[2:], [x for x in TINCT_F if x not in METAL])
                if t not in cs:
                    cs.append(t)
        if cs:
            CULT_COLORS[_a.get("id")] = cs
except OSError:
    pass
# godla Essos: (pojedyncze, mnogie) - P4
ES_CHARGE = {
    "freecity": [("a galley", "galleys"), ("a tower", "towers"), ("a key", "keys"), ("a coin", "coins"), ("an elephant", "elephants"),
                 ("a lantern", "lanterns"), ("a crescent moon", "crescent moons"), ("a mask", "masks"), ("a dolphin", "dolphins"),
                 ("a pair of scales", "pairs of scales"), ("a cat", "cats"), ("an amphora", "amphorae"), ("a bridge", "bridges"),
                 ("a trident", "tridents"), ("a seashell", "seashells"), ("a sea serpent", "sea serpents")],
    "ghis": [("a pyramid", "pyramids"), ("a brazier", "braziers"), ("a bull", "bulls"), ("a lion", "lions"), ("a sphinx", "sphinxes"),
             ("a chain", "chains"), ("a scorpion", "scorpions"), ("a palm tree", "palm trees"), ("a stepped tower", "stepped towers"),
             ("a sun", "suns"), ("a crescent", "crescents"), ("a jackal", "jackals"), ("a wasp", "wasps"), ("a ram", "rams")],
    "qarth": [("a gate", "gates"), ("a sphinx", "sphinxes"), ("a peacock", "peacocks"), ("a tower", "towers"), ("a ship", "ships"),
              ("a lamp", "lamps"), ("a camel", "camels"), ("a key", "keys")],
    "ibben": [("a whale", "whales"), ("a harpoon", "harpoons"), ("a bear", "bears"), ("an axe", "axes"), ("a ship", "ships"), ("a seal", "seals")],
    "yiti": [("a lion", "lions"), ("a sun", "suns"), ("a crane", "cranes"), ("a lotus", "lotuses"), ("a tiger", "tigers"), ("a pagoda", "pagodas")],
    "summer": [("a parrot", "parrots"), ("a swan ship", "swan ships"), ("a palm tree", "palm trees"), ("a sun", "suns"), ("a bow", "bows")],
    "sarnor": [("a chariot", "chariots"), ("a horse", "horses"), ("a spear", "spears"), ("a tower", "towers")],
}
COUNTS = ["", "two", "three"]

def _art(w):
    return "an " if w[0] in "aeiou" else "a "

USED_ARMS = defaultdict(set)   # kraina (Westeros: nazwa krainy; Essos: kultura miasta) -> herby (male litery)

def arms_key(reg, cult):
    return reg if reg != "Essos" else "Essos:" + (cult or "?")

def gen_arms(style, seed, akey, cult):
    """P3: jeden hash -> indeks w przestrzeni (barwy, godlo[, ile]); sondowanie do pierwszego herbu wolnego w krainie.
    Zwraca (tekst, pole, barwa godla, godlo) albo ("", None, None, None)."""
    if style in ST:
        charges = [(c, None) for c in ST[style]["charge"]]
        counts = [""]
        combos = [(f, t) for f in TINCT_F for t in TINCT_F if (t in METAL) != (f in METAL) and t != f]   # metal na kolorze, kolor na metalu
    elif style in ES_CHARGE:
        charges = ES_CHARGE[style]
        counts = COUNTS
        cc = CULT_COLORS.get(cult) or ["blue", "red"]
        # pole z barwy miasta i godlo z metalu albo pole z metalu i godlo w barwie miasta
        combos = [(f, t) for f in cc for t in ("gold", "silver", "white")] + [(m, c) for m in ("gold", "silver", "white") for c in cc]
    else:
        return "", None, None, None
    if not charges:
        return "", None, None, None
    N = len(combos) * len(charges) * len(counts)
    start = fnv1a(seed + "|arms") % N
    step = 7919 if N % 7919 else 7907
    for k in range(N):
        i = (start + k * step) % N
        f, t = combos[i % len(combos)]
        ch, chp = charges[(i // len(combos)) % len(charges)]
        cnt = counts[(i // (len(combos) * len(charges))) % len(counts)]
        if ch.startswith("a crossed pair of "):
            txt = f"two crossed {t} {ch[len('a crossed pair of '):]} on {f}"
        elif cnt:
            txt = f"{cnt} {t} {chp} on {f}"
        else:
            txt = f"{_art(t)}{t} {re.sub(r'^(a|an) ', '', ch)} on {f}"
        if txt.lower() in USED_ARMS[akey]:
            continue
        USED_ARMS[akey].add(txt.lower())
        return txt, f, t, ch
    return "", None, None, None

# P4: kod herbu gry. Godlo TYLKO z ikon, ktorych znaczenie potwierdzaja dane gry (ikony to tekstury w tpac bez nazw - slowa z herbu
# nie da sie dopasowac do reszty ikon bez ich obejrzenia). Zrodla: banery wiar BKROTPatch (dekompilacja 12.10) i klanow ROT spclans.xml.
ICON_VERIFIED = [
    (r"\bhorses?\b|\bstallions?\b|\bmares?\b", 125),   # GreatStallionFaith (jedyna ikona) + klan Pono (Dothrakowie, jedyna ikona)
    (r"\bgoats?\b", 132),                              # BlackGoatFaith - glowna ikona
    (r"\btrees?\b", 213),                              # OldGodsFaith - czardrzewo (grupa Flora)
    (r"\bharp(y|ies)\b", 104),                         # HarpyFaith - jedyna ikona
    (r"\bsand cats?\b|\bcats?\b", 142),                # golden_company "Company of the Cat" - jedyna ikona
    (r"\bfox(es)?\b|\bfox's\b", 119),                  # Florent (glowa lisa) - najwieksza ikona herbu
    (r"\bcrabs?\b", 156),                              # Celtigar (czerwone kraby) - jedyna ikona poza pasem
    (r"\bturtles?\b", 158),                            # Estermont (zolw morski) - glowna ikona zwierzeca
]
PALETTE = {}   # id koloru gry -> (r, g, b); tylko Native (zawsze wczytane)
try:
    for _c in ET.parse(os.path.join(GAME, "Native", "ModuleData", "banner_icons.xml")).getroot().iter("Color"):
        _h = _c.get("hex")[-6:]
        PALETTE[int(_c.get("id"))] = (int(_h[0:2], 16), int(_h[2:4], 16), int(_h[4:6], 16))
except (OSError, ET.ParseError):
    pass
ICON_IDS = set()
try:
    for _mod in ("Native", "ROT-Content"):
        for _i in ET.parse(os.path.join(GAME, _mod, "ModuleData", "banner_icons.xml")).getroot().iter("Icon"):
            ICON_IDS.add(int(_i.get("id")))
except (OSError, ET.ParseError):
    pass

def palette_id(tinct):
    h = TINCT_HEX.get(tinct)
    if not h or not PALETTE:
        return None
    h = h.lstrip("#"); r, g, b = int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16)
    return min(PALETTE, key=lambda k: ((PALETTE[k][0] - r) ** 2 + (PALETTE[k][1] - g) ** 2 + (PALETTE[k][2] - b) ** 2, k))

def banner_code(field, tinct, charge_text):
    """Tlo 11 (podstawowe) w barwie pola + jedna ikona w barwie godla, uklad jak herby klanow gry; brak = "" (mod pokaze herb pana)."""
    if not field or not tinct or not charge_text:
        return ""
    icon = next((i for rx, i in ICON_VERIFIED if re.search(rx, charge_text.lower())), None)
    if icon is None or icon not in ICON_IDS:
        return ""
    fc, tc = palette_id(field), palette_id(tinct)
    if fc is None or tc is None:
        return ""
    return f"11.{fc}.{fc}.1528.1528.764.764.1.0.0.{icon}.{tc}.{tc}.483.483.764.764.0.0.0"

def lore_banner(arms, kolory):
    cols = [c for c in kolory.split(";") if c in TINCT_HEX]
    if not arms or not cols:
        return ""
    field = cols[0]
    tinct = cols[1] if len(cols) > 1 else ("black" if field in METAL else "white")
    return banner_code(field, tinct, arms)

# ---------------- podglad przydzialu ----------------
AREA = {"northern mountains", "Northern mountains", "wolfswood", "Neck", "Wolf's Den", "Great Wyk", "Harlaw"}
vil = []
_hdr = None
for line in open(VILLAGES_TSV, encoding="utf-8-sig"):
    line = line.rstrip("\r\n")
    if line.startswith("#id\t"):
        _hdr = [h.strip() for h in line[1:].split("\t")]
        continue
    if line.startswith("#") or not line.strip():
        continue
    c = dict(zip(_hdr, [x.strip() for x in line.split("\t")]))
    uid = c.get("uid") or ""
    if uid in ("", "-"):
        uid = c["village_id"] + "#" + c["id"]   # jak MapVillageData.Fill (wiersz bez uid)
    vil.append(dict(id=c["id"], village_id=c["village_id"], name=c["name"], x=float(c["x"]), y=float(c["y"]), kind=c.get("kind", ""),
                    uid=uid, cls=c.get("class", "")))

def district(v):
    gv = byid.get(v["village_id"]); b = byid.get(gv["bound"]) if gv else None
    return gv, b

ELIG = {"pomniejszy_rod_lord", "pomniejszy_rod_master", "rycerz_z_ziemia", "pomniejszy_rod", "rod_essos"}
lore = [r for r in rody if r["rola"] in ELIG and r["x"]]
by_region = defaultdict(list)
for r in lore:
    by_region[r["kraina"]].append(r)
free = {v["uid"]: v for v in vil}
assign = {}
def region_of(v):
    gv, b = district(v)
    cult = b["culture"] if b else (gv["culture"] if gv else "")
    if cult in REGION_OF_CULT:
        return REGION_OF_CULT[cult], cult, b
    city = None
    if b:
        city = b["name"]
    return "Essos", cult, b
vreg = {v["uid"]: region_of(v) for v in vil}

def holder_count(r):
    return 2 if r["rola"] in ("pomniejszy_rod_lord", "pomniejszy_rod_master") else 1

# 1) rody o znanym polozeniu (A, A-, B, C) - najblizsze wolne wioski tej samej krainy (do 70 j.)
fixed = [r for r in lore if r["pewnosc"] in ("A", "A-", "B", "C")]
fixed.sort(key=lambda r: ({"pomniejszy_rod_lord": 0, "pomniejszy_rod_master": 0, "rycerz_z_ziemia": 1}.get(r["rola"], 2), r["rod"]))
def same_reg(r, v):
    rg = vreg[v["uid"]][0]
    if r["kraina"].startswith("Essos"):
        return rg == "Essos"
    return rg == r["kraina"] or (rg.startswith("The Crownlands") and r["kraina"].startswith("The Crownlands"))
for r in fixed:
    x, y = float(r["x"]), float(r["y"])
    cand = sorted((math.hypot(v["x"] - x, v["y"] - y), v["uid"]) for v in free.values() if same_reg(r, v))
    cand = [c for c in cand if c[0] <= (40 if r["kraina"].startswith("Essos") else 70)][:holder_count(r)]
    seat0 = r["siedziba"].split(";")[0]
    if cand and (not seat0 or seat0[:1].islower() or seat0 in AREA or r["pewnosc"] == "C"):
        seat0 = free[cand[0][1]]["name"]
    for i, (d, u) in enumerate(cand):
        assign[u] = dict(src="lore", house=r["rod"], rola=r["rola"], arms=r["herb"], names=r["imiona"], seat=seat0,
                         liege=r["pan"], dist=d, first=(i == 0))
        del free[u]
# 2) rody swobodne (D): wolna wioska w okregu pana (klan ROT o nazwie pana), inaczej dowolna w krainie, kolejnosc z hasha
floating = sorted([r for r in lore if r["pewnosc"] == "D"], key=lambda r: fnv1a(r["rod"]))
for r in floating:
    liege = (r["pan"] or "").split(" of ")[0].lower()
    pool = [v for v in free.values() if same_reg(r, v)]
    pref = [v for v in pool if vreg[v["uid"]][2] and cl.get(vreg[v["uid"]][2]["owner"], {}).get("name", "").split(",")[0].lower() == liege]
    pool = pref or pool
    if not pool:
        continue
    v0 = min(pool, key=lambda v: fnv1a(r["rod"] + "|" + v["uid"]))
    near = sorted(pool, key=lambda v: math.hypot(v["x"] - v0["x"], v["y"] - v0["y"]))[:holder_count(r)]
    for i, v in enumerate(near):
        assign[v["uid"]] = dict(src="lore-swobodny", house=r["rod"], rola=r["rola"], arms=r["herb"], names=r["imiona"],
                                seat=v0["name"], liege=r["pan"], dist=0, first=(i == 0))
        del free[v["uid"]]
# herby rodow lore zajmuja swoje miejsce w krainie, zanim dobierzemy wymyslone (P3: 0 powtorzen w krainie)
for u, a_ in sorted(assign.items()):
    if a_["arms"]:
        reg_, cult_, _b = vreg[u]
        USED_ARMS[arms_key(reg_, cult_)].add(a_["arms"].lower())
# 3) reszta: rod wymyslony w stylu krainy, 1-2 wioski tego samego okregu na rod
by_d = defaultdict(list)
for v in free.values():
    by_d[v["village_id"]].append(v)
for did, vs in sorted(by_d.items()):
    vs.sort(key=lambda v: fnv1a(v["uid"]))
    while vs:
        v0 = vs.pop(0)
        group = [v0]
        if vs and fnv1a(v0["uid"] + "|pair") % 2 == 0:
            nb = min(vs, key=lambda v: math.hypot(v["x"] - v0["x"], v["y"] - v0["y"]))
            if math.hypot(nb["x"] - v0["x"], nb["y"] - v0["y"]) < 15:
                vs.remove(nb); group.append(nb)
        reg, cult, b = vreg[v0["uid"]]
        style = style_of(cult)
        h = gen_house(style, v0["uid"]) if style not in ("gift", "freefolk", "dothraki") else None
        person, form = gen_person(style, v0["uid"], h or "")
        arms, af, at, ach = gen_arms(style, v0["uid"], arms_key(reg, cult), cult) if h else ("", None, None, None)
        banner = banner_code(af, at, ach)
        for i, v in enumerate(group):
            assign[v["uid"]] = dict(src="gen", house=("House " + h) if h else "", rola="wymyslony", arms=arms, banner=banner,
                                    names=person, form=form, seat=v0["name"], liege="", dist=0, first=(i == 0))

# ---------------- zapis rycerzy (E7/L: wynik posredni dla l0.py) ----------------
ANDAL = ("river", "vale", "west", "reach", "storm", "crown", "dorne")
KOLORY = {r["rod"]: r["kolory"] for r in rody}
rows = []
lore_person = {}   # rod lore -> (osoba, forma): jeden rycerz na rod (2 wioski pomniejszego lorda = ten sam czlowiek)
lore_arms = {}     # rod lore Essos -> (herb, kod) dobrany raz na rod
for v in vil:
    a = assign[v["uid"]]
    reg, cult, b = vreg[v["uid"]]
    style = style_of(cult)
    if a["src"] == "gen":
        person, form, house, arms, banner = a["names"], a["form"], a["house"], a["arms"], a["banner"]
    else:
        sur = surname_of(a["house"])
        if a["house"] not in lore_person:
            # P1: rod prawdziwy, imie z listy krainy (hash z pierwszej wioski rodu w kolejnosci pliku), bez zestawow z lore;
            # "Ser" takze dla rodu pomniejszego lorda na ziemiach Andalow (WL-P 3.3), Polnoc i Wyspy bez tytulu
            seed = v["uid"]
            if style in ANDAL:
                st = ST[style]
                lady = st["lady"] and fnv1a(seed + "|g") % 9 == 0
                given = pick_given(st["lady"] if lady else st["given"], seed, "n", sur)
                person = ("Lady " if lady else "Ser ") + given + " " + sur
                form = "kept"
            else:
                person, form = gen_person(style, seed, sur)
            lore_person[a["house"]] = (person, form)
        person, form = lore_person[a["house"]]
        house = re.sub(r"\s*\(.*?\)", "", a["house"]).strip()
        arms = a["arms"]
        banner = lore_banner(arms, KOLORY.get(a["house"], ""))
        if not arms and style in ES_CHARGE:
            # P4: rod lore Essos - lore nie zna jego herbu, gra potrzebuje obrazka: barwy kultury miasta + godlo (jak rody wymyslone)
            if a["house"] not in lore_arms:
                t_, f_, c_, ch_ = gen_arms(style, a["house"], arms_key(reg, cult), cult)
                lore_arms[a["house"]] = (t_, banner_code(f_, c_, ch_))
            arms, banner = lore_arms[a["house"]]
    knight = person if form == "tribute" else person + " of " + a["seat"]
    rows.append(dict(uid=v["uid"], name=v["name"], region=reg, culture=cult, style=style, src=a["src"], house=house,
                     knight=knight, knight_form=form, seat=a["seat"], arms=arms, banner=banner,
                     district=(byid.get(v["village_id"]) or {}).get("name", "")))
with open(os.path.join(B, "rycerze.tsv"), "w", encoding="utf-8", newline="") as f:
    w = csv.DictWriter(f, list(rows[0].keys()), delimiter="\t", lineterminator="\n")
    w.writeheader(); w.writerows(rows)
src = Counter(r["src"] for r in rows)
print("wiosek:", len(rows), dict(src))
print("rody lore uzyte:", len({r["house"] for r in rows if r["src"] != "gen"}), "z", len(lore))
print("unikalnych rodow wymyslonych:", len({r["house"] for r in rows if r["src"] == "gen" and r["house"]}))
print("kody herbow (P4, godla potwierdzone):", len({r["house"] for r in rows if r["banner"]}), "rodow,", sum(1 for r in rows if r["banner"]), "wiosek")
per = defaultdict(Counter)
for r in rows:
    per[r["region"]][r["src"]] += 1
for k, c_ in sorted(per.items()):
    print(f"  {k}: {dict(c_)}")
