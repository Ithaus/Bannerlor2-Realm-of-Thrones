# -*- coding: utf-8 -*-
"""Rody kanonu (ASOIAF) dla gospodarzy wiosek pobocznych - Jeff 16.10 (D2, klucz MCM VillageCanonHouses, VillageHolders.cs).

Uzycie (z katalogu repo, Python 3, bez zaleznosci):
    python -I tools/herby/herby_kanon.py "<Mount & Blade II Bannerlord>/Modules" [--out Armoury/ModuleData/arm_houses_canon.tsv] [--report]

Wejscie (tools/herby/dane): kanon_rody.tsv (536 rodow z awoiaf: region, siedziba, pan, herb slowami, dewiza, klan ROT),
ikony_godla.tsv (993 ikony herbow nazwane z tekstur), kolory.tsv (barwy gry -> tynktury), tla.tsv (tla = podzialy pola).
Z gry: ROT-Content spclans.xml (banner_key klanow ROT), ROT-Map settlements.xml (kultura wsi gry -> kraina okregu),
banner_icons.xml Native / NavalDLC / ROT-Content / Bannerlord.CCsBanners (sprawdzenie, ze kazda ikona, tlo i barwa istnieja),
Armoury/ModuleData/arm_map_village_lore*.tsv (imiona rycerzy krainy - zapas imion dla wioski bez lore).

Wyjscie: Armoury/ModuleData/arm_houses_canon.tsv - trzy czesci (naglowek "#house", "#district", "#names"):
 - rod: kraina (area), kultura ROT, siedziby, pan (liege) i jego rdzen ("House Tully"), ranga, herb slowami, dewiza, klany ROT,
   banner_code (rod z klanem ROT = jego banner_key; inaczej herb zlozony z opisu), banner_code_ccs (ten sam herb z ikonami CCsBanners,
   tylko gdy ten modul jest wczytany; puste = jak banner_code), source rot|generated, holder yes|no (czy moze gospodarzyc wioska), parse;
 - okreg (wies gry): kraina z KULTURY Z PLIKU MAPY (BK zmienia Settlement.Culture w grze - kraina okregu ma byc stala);
 - imiona: poczatki imion rycerzy z lore danej krainy ("Ser Howard", "Lady Barbrey", "Harwood", "Gorzhak zo").
Deterministyczny (bez losowania; wybory z crc32 nazwy rodu). Kazdy kod sprawdzony jak Banner.TryGetBannerDataFromCode
(TaleWorlds.Core 1.4.8: grupy po 10 liczb calkowitych) + ostrzej: liczba pol podzielna przez 10, tlo z grup is_pattern, ikony i barwy
istnieja w banner_icons.xml, herb zlozony bez barw 194-251 (CCsBanners nadpisuje je inna paleta) i najwyzej 33 warstwy (Banner.AddIconData).

Kadr gry: BannerTextureCreator.CreateDefaultBannerCamera widzi srodkowa jedna trzecia plotna 1528 (509..1019) - tam idzie cale godlo.
Obrot warstwy w kodzie = stopnie przeciwnie do zegara (ROT Sarsfield: pionowy pas obrocony o 45 = skos w prawo w dol).
"""
import sys, os, re, io, csv, zlib, collections
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
DANE = os.path.join(HERE, 'dane')

# ------------------------------------------------------------------------------------------------ kod herbu
FULL, MID = 1528, 764
V0, V1, HALF = 509, 1019, 255          # widoczny kadr (srodkowa 1/3)
SQ = 0.88                              # ikona 505 (kwadrat) wypelnia 88% swojego pola - prostokaty pasow liczone z tym
FORBID_COLORS = set(range(194, 252))   # ROT 194-251; CCsBanners nadpisalby 194-241 inna paleta


def L(mesh, c1, c2, sx, sy, px, py, stroke=0, mirror=0, rot=0):
    return [int(mesh), int(c1), int(c2), int(round(sx)), int(round(sy)), int(round(px)), int(round(py)), int(stroke), int(mirror), int(round(rot)) % 360]


def code_of(layers):
    return '.'.join(str(v) for lay in layers for v in lay)


def _cs_int(s):
    """int.TryParse (NumberStyles.Integer, kultura niezmienna): spacje na brzegach, opcjonalny znak, same cyfry, zakres Int32."""
    t = s.strip(' \t\n\r\x0b\x0c')
    if not re.fullmatch(r'[+-]?[0-9]+', t):
        return None
    v = int(t)
    return v if -2 ** 31 <= v < 2 ** 31 else None


def try_get_banner_data(code):
    """Port Banner.TryGetBannerDataFromCode: lista warstw albo None (blad = cala lista pusta i false)."""
    a = code.split('.')
    out = []
    i = 0
    while i + 10 <= len(a):
        vals = [_cs_int(x) for x in a[i:i + 10]]
        if any(v is None for v in vals):
            return None
        out.append(vals)
        i += 10
    return out


def problems(code, bgs, icons, colors, generated):
    """Lista bledow kodu ([] = wazny). Banner.IsValidBannerCode = niepusty + TryGetBannerDataFromCode; reszta - nasze ostrzejsze zasady."""
    p = []
    if not code:
        return ['pusty kod']
    layers = try_get_banner_data(code)
    if layers is None:
        return ['nie przechodzi TryGetBannerDataFromCode']
    if len(code.split('.')) % 10 != 0:
        p.append('liczba pol nie dzieli sie przez 10')
    if len(layers) < 2:
        p.append('brak warstwy 1 (Banner.GetFirstIconColor i podobne czytaja indeks 1)')
    if generated and len(layers) > 33:
        p.append('warstw %d > 33' % len(layers))
    for k, lay in enumerate(layers):
        mesh, c1, c2, sx, sy = lay[0], lay[1], lay[2], lay[3], lay[4]
        if k == 0 and mesh not in bgs:
            p.append('tlo %d nie istnieje' % mesh)
        if k > 0 and mesh not in icons:
            p.append('ikona %d nie istnieje' % mesh)
        for c in (c1, c2):
            if c not in colors:
                p.append('barwa %d nie istnieje' % c)
            elif generated and c in FORBID_COLORS:
                p.append('barwa %d z puli 194-251' % c)
        if generated and (sx == 0 or sy == 0):
            p.append('zerowy rozmiar warstwy %d' % k)
    return p


# ------------------------------------------------------------------------------------------------ dane gry
def read_xml(path):
    t = open(path, encoding='utf-8-sig').read()
    t = re.sub(r'<!--.*?-->', '', t, flags=re.S)
    return ET.fromstring(t)


def load_banner_icons(modules):
    """modul -> (tla, ikony, barwy). Kolejnosc wczytania: Native, NavalDLC, ROT-Content, (CCsBanners - tylko gdy wlaczony)."""
    res = {}
    for mod in ('Native', 'NavalDLC', 'ROT-Content', 'Bannerlord.CCsBanners'):
        path = os.path.join(modules, mod, 'ModuleData', 'banner_icons.xml')
        bgs, icons, colors = set(), set(), set()
        if os.path.exists(path):
            root = read_xml(path)
            for g in root.iter('BannerIconGroup'):
                pat = (g.get('is_pattern') or '').lower() == 'true'
                for b in g.iter('Background'):
                    if pat:
                        bgs.add(int(b.get('id')))
                for ic in g.iter('Icon'):
                    icons.add(int(ic.get('id')))
            for c in root.iter('Color'):
                colors.add(int(c.get('id')))
        res[mod] = (bgs, icons, colors)
    return res


def load_spclans(modules):
    out = {}
    for mod in ('ROT-Content',):
        path = os.path.join(modules, mod, 'ModuleData', 'spclans.xml')
        root = read_xml(path)
        for f in root.iter('Faction'):
            if f.get('id') and f.get('banner_key'):
                out[f.get('id')] = f.get('banner_key')
    return out


def load_villages(modules):
    """id wsi gry -> (kultura, nazwa) z ROT-Map settlements.xml."""
    root = read_xml(os.path.join(modules, 'ROT-Map', 'ModuleData', 'settlements.xml'))
    out, alls = {}, {}
    for s in root.iter('Settlement'):
        sid = s.get('id')
        comp = s.find('Components')
        kind = comp[0].tag if comp is not None and len(comp) else ''
        cul = (s.get('culture') or '').replace('Culture.', '')
        name = re.sub(r'\{=[^}]*\}', '', s.get('name') or '')
        alls[sid] = kind
        if kind == 'Village':
            out[sid] = (cul, name)
    return out, alls


# ------------------------------------------------------------------------------------------------ krainy
CULTURE_AREA = {
    'battania': 'North', 'skagosi': 'North', 'river': 'Riverlands', 'vale': 'Vale', 'vlandia': 'Westerlands', 'reach': 'Reach',
    'stormlands': 'Stormlands', 'aserai': 'Dorne', 'sturgia': 'IronIslands', 'crownlands': 'Crownlands', 'dragonstone': 'Crownlands',
    'empire': 'Braavos', 'lyseni': 'Lys', 'volantine': 'Volantis', 'ghiscari': 'SlaversBay',
}
HOLDER_AREAS = set(CULTURE_AREA.values())


def area_of_region(region):
    r = region.strip()
    if r.startswith('Vale of Arryn/Riverlands'):
        return 'Riverlands'
    if r.startswith('Crownlands (origin); Riverlands') or r.startswith('Iron Islands (origin); Riverlands'):
        return 'Riverlands'
    for pre, a in (('North', 'North'), ('Riverlands', 'Riverlands'), ('Vale of Arryn', 'Vale'), ('Westerlands', 'Westerlands'),
                   ('Reach', 'Reach'), ('Stormlands', 'Stormlands'), ('Dorne', 'Dorne'), ('Iron Islands', 'IronIslands'),
                   ('Crownlands', 'Crownlands'), ('Braavos', 'Braavos'), ('Lys', 'Lys'), ('Volantis', 'Volantis'),
                   ('Meereen', 'SlaversBay'), ('Yunkai', 'SlaversBay'), ('Astapor', 'SlaversBay'),
                   ('Summer Isles', 'SummerIsles'), ('Yi Ti', 'YiTi')):
        if r.startswith(pre):
            return a
    return ''


def house_base(name):
    """'House Baratheon of Storm's End' -> 'House Baratheon'; 'House of Loraq' -> 'House Loraq'; 'Andrik of Southshield' -> 'Andrik'."""
    n = name.strip()
    if n.startswith('House of '):
        n = 'House ' + n[9:]
    m = re.match(r"^(.*?)\s+of\s+.*$", n)
    if m and not m.group(1).endswith('House'):
        n = m.group(1)
    return n.strip()


# ------------------------------------------------------------------------------------------------ tynktury
# (fraza, klucz); dluzsze najpierw. Klucz -> barwa gry (kolory.tsv: najczesciej uzywana przez ROT w tej tynkturze).
TINCT_PHRASES = [
    ('bleu celeste', 'celeste'), ('sky-blue', 'celeste'), ('sky blue', 'celeste'), ('pale blue', 'celeste'), ('light blue', 'celeste'),
    ('dark blue', 'indigo'), ('indigo', 'indigo'), ('vert-cendree', 'greygreen'), ('grey-green', 'greygreen'), ('storm-green', 'greygreen'),
    ('pale green', 'lightgreen'), ('light green', 'lightgreen'), ('dark green', 'vert'), ('sea green', 'seagreen'), ('sea-green', 'seagreen'),
    ('blue-green', 'seagreen'), ('aquamarine', 'seagreen'), ('turquoise', 'seagreen'), ('silver-grey', 'cendree'), ('pale grey', 'lightgrey'),
    ('dark grey', 'iron'), ('red-brown', 'brun'), ('mud brown', 'brun'), ('dark brown', 'brun'), ('pale pink', 'rose'), ('dusty pink', 'rose'),
    ('pale violet', 'lilac'), ('ice-white', 'white'),
    ('or', 'or'), ('gold', 'or'), ('golden', 'or'), ('gilt', 'or'), ('yellow', 'yellow'), ('saffron', 'yellow'), ('sun-yellow', 'yellow'),
    ('argent', 'argent'), ('silver', 'silver'), ('white', 'white'), ('ivory', 'ivory'), ('cream', 'ivory'), ('bone', 'ivory'),
    ('gules', 'gules'), ('red', 'gules'), ('scarlet', 'scarlet'), ('crimson', 'gules'), ('sanguine', 'sanguine'), ('blood', 'sanguine'),
    ('bloody', 'sanguine'), ('burgundy', 'murrey'), ('murrey', 'murrey'), ('maroon', 'murrey'),
    ('azure', 'azure'), ('blue', 'azure'), ('vert', 'vert'), ('green', 'vert'), ('olive', 'olive'), ('sable', 'sable'), ('black', 'sable'),
    ('purpure', 'purpure'), ('purple', 'purpure'), ('violet', 'purpure'), ('lilac', 'lilac'), ('lavender', 'lilac'),
    ('tenne', 'tenne'), ('orange', 'tenne'), ('flame', 'tenne'), ('copper', 'copper'), ('bronze', 'bronze'), ('brass', 'bronze'),
    ('russet', 'russet'), ('brunatre', 'brun'), ('brown', 'brun'), ('oak', 'brun'), ('oaken', 'brun'), ('wooden', 'brun'),
    ('cendree', 'cendree'), ('grey', 'cendree'), ('gray', 'cendree'), ('stone', 'cendree'), ('iron', 'iron'), ('slate', 'iron'),
    ('rose', 'rose'), ('pink', 'rose'), ('carnation', 'carnation'),
    ('ermines', 'ermines'), ('erminois', 'erminois'), ('ermine', 'ermine'), ('pean', 'pean'),
]
TINCT_WORDS = set(p for p, _ in TINCT_PHRASES if ' ' not in p) | {'crossed', 'sleeping', 'falling', 'leaping', 'flying', 'double-headed', 'three-headed'}
TINCT_COLOR = {
    'or': 39, 'yellow': 143, 'argent': 33, 'white': 35, 'silver': 28, 'ivory': 41,
    'gules': 145, 'scarlet': 83, 'sanguine': 148, 'murrey': 65, 'azure': 12, 'indigo': 100, 'celeste': 152,
    'vert': 126, 'olive': 113, 'lightgreen': 120, 'greygreen': 189, 'seagreen': 74, 'sable': 116,
    'purpure': 71, 'lilac': 86, 'tenne': 38, 'copper': 91, 'bronze': 122, 'russet': 59, 'brun': 92,
    'cendree': 25, 'lightgrey': 19, 'iron': 101, 'rose': 93, 'carnation': 56,
    'ermine': 33, 'ermines': 116, 'erminois': 39, 'pean': 116,
}
METALS = {'or', 'yellow', 'argent', 'white', 'silver', 'ivory', 'ermine', 'erminois'}
LIGHT = METALS | {'celeste', 'lightgrey', 'rose', 'carnation', 'lilac', 'lightgreen', 'cendree'}
DARK_PICK = ['gules', 'azure', 'sable', 'vert', 'purpure']
LIGHT_PICK = ['or', 'argent']


def is_light(t):
    return t in LIGHT


def contrast(field, h):
    """Zasada tynktur: na jasnym polu barwa, na ciemnym metal (wybor z crc nazwy)."""
    if field is None:
        return DARK_PICK[h % len(DARK_PICK)]
    return DARK_PICK[h % len(DARK_PICK)] if is_light(field) else LIGHT_PICK[h % len(LIGHT_PICK)]


def crc(s):
    return zlib.crc32(s.encode('utf-8')) & 0xffffffff


# ------------------------------------------------------------------------------------------------ godla
# Rzeczownik (l. poj.) -> (ikony Native/ROT najlepsze najpierw, ikony CCs, rodzina, barwa "proper").
# Ikony z ikony_godla.tsv (nazwane z tekstur); slownik recznie dla glownych godel, reszta z kolumny keywords.
LEX = {
    'lion': ([540, 140, 541, 160], [], 'beast', 'or'), 'lion passant': ([141, 143], [], 'beast', 'or'),
    'sleeping lion': ([144], [], 'beast', 'or'), "lion's head": ([139], [], 'beast', 'or'), 'manticore': ([140], [75104], 'monster', 'gules'),
    'eagle': ([104, 549, 102], [], 'bird', 'brun'), 'double-headed eagle': ([100, 161, 101], [], 'bird', 'sable'),
    "eagle's head": ([119], [], 'bird', 'brun'), "eagle's leg": ([119], [], 'bird', 'sable'), 'falcon': ([103, 549], [], 'bird', 'brun'),
    'hawk': ([103, 108], [], 'bird', 'brun'), "hawk's head": ([119], [], 'bird', 'brun'), 'raven': ([118, 116, 111], [], 'bird', 'sable'),
    'crow': ([118, 116], [], 'bird', 'sable'), 'rook': ([118], [], 'bird', 'sable'), 'vulture': ([118], [], 'bird', 'sable'),
    'bat': ([116], [], 'bird', 'sable'), 'owl': ([118], [78710], 'bird', 'brun'), 'martlet': ([115, 117], [], 'bird', 'sable'),
    'swallow': ([117, 115], [], 'bird', 'sable'), 'seagull': ([108, 115], [], 'bird', 'argent'), 'gull': ([108, 115], [], 'bird', 'argent'),
    'seabird': ([108, 115], [], 'bird', 'argent'), 'mockingbird': ([115], [], 'bird', 'cendree'), 'swan': ([157], [], 'bird', 'argent'),
    'goose': ([157], [], 'bird', 'argent'), 'heron': ([157], [76514, 78302], 'bird', 'cendree'), 'crane': ([157], [76514], 'bird', 'cendree'),
    'pelican': ([157], [], 'bird', 'argent'), 'peacock': ([114], [], 'bird', 'azure'), 'pheasant': ([114], [], 'bird', 'brun'),
    'rooster': ([113], [78314], 'bird', 'gules'), 'cock': ([113], [78314], 'bird', 'gules'), "cock's head": ([113], [78314], 'bird', 'or'),
    'phoenix': ([105], [], 'bird', 'tenne'), 'wing': ([105], [78705, 78914], 'bird', 'argent'), 'feather': ([217], [78914, 78409], 'flora', 'argent'),
    'quill': ([217], [78914], 'flora', 'argent'),
    'dragon': ([120, 122], [], 'monster', 'gules'), 'three-headed dragon': ([536], [], 'monster', 'gules'), 'wyvern': ([120], [], 'monster', 'vert'),
    'sea serpent': ([121, 174], [], 'monster', 'vert'), 'serpent': ([544, 154], [], 'reptile', 'vert'), 'viper': ([544, 155], [], 'reptile', 'vert'),
    'snake': ([544, 154], [], 'reptile', 'vert'), 'leviathan': ([121, 174], [78805], 'monster', 'sable'), 'kraken': ([121], [78206, 78912], 'monster', 'or'),
    'sea lion': ([121], [78805], 'monster', 'argent'), 'unicorn': ([543, 125], [78307, 76512, 78604], 'beast', 'argent'),
    'pegasus': ([125], [76516, 78916], 'beast', 'argent'), 'griffin': ([102], [75016], 'monster', 'or'), 'harpy': ([122], [], 'monster', 'or'),
    'cockatrice': ([120], [78512], 'monster', 'vert'), 'centaur': ([129], [78405], 'human', 'brun'), 'sphinx': ([547], [75104], 'monster', 'or'),
    'horse': ([543, 125, 124, 127], [], 'beast', 'brun'), 'stallion': ([543, 125], [], 'beast', 'brun'), "horse's head": ([131], [], 'beast', 'brun'),
    'stag': ([539, 137, 135], [], 'beast', 'brun'), 'hart': ([539, 137], [], 'beast', 'brun'), 'deer': ([135, 136], [], 'beast', 'brun'),
    'fawn': ([136, 135], [], 'beast', 'brun'), 'elk': ([137], [], 'beast', 'brun'), 'moose': ([137], [], 'beast', 'brun'),
    "stag's head": ([133, 134], [], 'beast', 'brun'), "hart's head": ([133], [], 'beast', 'brun'), "stag's attire": ([133], [78312], 'beast', 'or'),
    'antlers': ([133], [78312], 'beast', 'or'), "ram's head": ([132], [], 'beast', 'argent'), 'ram': ([132, 153], [78402], 'beast', 'argent'),
    'goat': ([132], [75009, 78402], 'beast', 'argent'), 'lamb': ([132], [], 'beast', 'argent'), 'sheep': ([132], [], 'beast', 'argent'),
    'ox': ([149], [78908, 78313], 'beast', 'brun'), 'bull': ([149], [78908], 'beast', 'sable'), "bull's skull": ([132], [78313], 'beast', 'argent'),
    'bear': ([546, 138], [], 'beast', 'sable'), "bear's paw": ([138], [78310], 'beast', 'brun'), 'bear paw': ([138], [78310], 'beast', 'brun'),
    'boar': ([149, 151], [78615], 'beast', 'brun'), "boar's head": ([149], [78615], 'beast', 'brun'), 'pig': ([149], [], 'beast', 'rose'),
    'wolf': ([148, 165, 147], [], 'beast', 'cendree'), 'direwolf': ([148, 537], [], 'beast', 'cendree'), "wolf's head": ([537], [], 'beast', 'cendree'),
    'wolf head': ([537], [], 'beast', 'cendree'), 'hound': ([146, 145], [78408], 'beast', 'brun'), 'dog': ([145, 146], [78404], 'beast', 'brun'),
    'greyhound': ([148], [], 'beast', 'cendree'), 'mastiff': ([145], [], 'beast', 'brun'), 'fox': ([146], [78510], 'beast', 'tenne'),
    'cat': ([547, 142], [], 'beast', 'sable'), 'treecat': ([547], [], 'beast', 'or'), 'leopard': ([141, 143], [], 'beast', 'or'),
    'spotted-leopard': ([143], [], 'beast', 'or'), "cat's head": ([139], [], 'beast', 'cendree'),
    'badger': ([152], [], 'beast', 'cendree'), 'otter': ([152], [], 'beast', 'brun'), 'weasel': ([152], [], 'beast', 'argent'),
    'ferret': ([152], [], 'beast', 'argent'), 'squirrel': ([152], [78913], 'beast', 'brun'), 'hedgehog': ([152], [], 'beast', 'brun'),
    'porcupine': ([152], [], 'beast', 'sable'), 'mammoth': ([548], [], 'beast', 'brun'), 'elephant': ([548], [], 'beast', 'cendree'),
    'camel': ([127], [78713], 'beast', 'brun'), 'donkey': ([127], [78904], 'beast', 'cendree'),
    'fish': ([159, 158], [], 'fish', 'silver'), 'trout': ([159], [], 'fish', 'silver'), 'salmon': ([159], [], 'fish', 'silver'),
    'pike': ([159], [], 'fish', 'silver'), 'codfish': ([158, 159], [], 'fish', 'silver'), 'catfish': ([158], [78509], 'fish', 'cendree'),
    'swordfish': ([159], [78216], 'fish', 'azure'), 'dolphin': ([159], [78715], 'fish', 'argent'), 'carp': ([158], [], 'fish', 'or'),
    'crab': ([156], [78305], 'vermin', 'gules'), 'spider crab': ([156], [78305], 'vermin', 'argent'), 'lobster': ([156], [74904], 'vermin', 'gules'),
    'crayfish': ([156], [74904], 'vermin', 'gules'), 'scorpion': ([156], [], 'vermin', 'sable'), 'spider': ([156], [78315], 'vermin', 'sable'),
    'starfish': ([521], [], 'celestial', 'tenne'), 'seashell': ([403], [75414], 'flora', 'argent'), 'scallop': ([403], [75414], 'flora', 'argent'),
    'turtle': ([152], [78503], 'reptile', 'vert'), 'tortoise': ([152], [78503], 'reptile', 'vert'), 'toad': ([154], [], 'reptile', 'vert'),
    'frog': ([154], [], 'reptile', 'vert'), 'lizard': ([154], [78213], 'reptile', 'vert'), 'crocodile': ([154], [78213], 'reptile', 'vert'),
    'ant': ([156], [], 'vermin', 'sable'), 'beetle': ([156], [], 'vermin', 'sable'), 'bee': ([156], [], 'vermin', 'or'),
    'moth': ([105], [], 'vermin', 'sable'), "death's head moth": ([105], [], 'vermin', 'sable'), 'butterfly': ([105], [], 'vermin', 'tenne'),
    'wasp': ([156], [], 'vermin', 'or'),
    'rose': ([538, 206, 220], [], 'flora', 'gules'), 'flower': ([220, 205, 207], [], 'flora', 'gules'), 'lily': ([207, 424], [], 'flora', 'argent'),
    'water lily': ([207], [], 'flora', 'argent'), 'lilypad': ([209], [], 'flora', 'vert'), 'tulip': ([205], [], 'flora', 'gules'),
    'fleur-de-lis': ([424], [], 'flora', 'or'), 'trefoil': ([210], [], 'flora', 'vert'), 'cinquefoil': ([220], [], 'flora', 'argent'),
    'quatrefoil': ([221], [], 'flora', 'argent'), 'leaf': ([209, 213], [], 'flora', 'vert'), 'maple leaf': ([213], [], 'flora', 'vert'),
    'oak leaf': ([213, 214], [], 'flora', 'vert'), 'ivy': ([219], [], 'flora', 'vert'), 'ivy leaf': ([219], [], 'flora', 'vert'),
    'thistle': ([215], [], 'flora', 'purpure'), 'fern': ([204], [], 'flora', 'vert'), 'branch': ([202, 204], [], 'flora', 'vert'),
    'sprig': ([202], [], 'flora', 'vert'), 'mistletoe': ([219], [], 'flora', 'vert'), 'broom': ([217], [], 'flora', 'or'),
    'laurel': ([423, 200], [], 'flora', 'vert'), 'garland': ([423], [], 'flora', 'vert'), 'wreath': ([423], [], 'flora', 'vert'),
    'wheat': ([201], [], 'flora', 'or'), 'sheaf': ([201], [], 'flora', 'or'), 'garb': ([201], [], 'flora', 'or'),
    'tree': ([212, 203], [], 'flora', 'vert'), 'apple tree': ([212], [], 'flora', 'vert'), 'pine tree': ([203], [], 'flora', 'vert'),
    'pine': ([203], [], 'flora', 'vert'), 'fir tree': ([203], [], 'flora', 'vert'), 'fir': ([203], [], 'flora', 'vert'),
    'oak': ([212], [], 'flora', 'vert'), 'weirwood': ([212], [], 'flora', 'argent'), 'stone tree': ([212], [], 'flora', 'cendree'),
    'palm tree': ([216], [], 'flora', 'vert'), 'pinecone': ([211], [], 'flora', 'brun'), 'apple': ([211], [], 'flora', 'gules'),
    'pomegranate': ([211], [], 'flora', 'gules'), 'pear': ([211], [], 'flora', 'or'), 'lemon': ([211], [], 'flora', 'yellow'),
    'strawberry': ([211], [], 'flora', 'gules'), 'pomme': ([503], [], 'shape', 'vert'), 'grapes': ([208, 222], [], 'flora', 'purpure'),
    'bunch of grapes': ([208, 222], [], 'flora', 'purpure'), 'pea pod': ([209], [], 'flora', 'vert'), 'hop': ([219], [78811], 'flora', 'vert'),
    'vine': ([219], [78811], 'flora', 'vert'), 'cornucopia': ([208], [], 'flora', 'or'), 'thorns': ([217], [], 'flora', 'sable'),
    'sword': ([301, 300, 302], [], 'weapon', 'argent'), 'longsword': ([301], [], 'weapon', 'argent'), 'broken sword': ([300], [], 'weapon', 'argent'),
    'dagger': ([300, 302], [], 'weapon', 'argent'), 'knife': ([300], [], 'weapon', 'argent'), 'spear': ([304], [75210], 'weapon', 'brun'),
    'spearhead': ([304], [], 'weapon', 'argent'), 'lance': ([304], [], 'weapon', 'brun'), 'harpoon': ([304], [], 'weapon', 'sable'),
    'trident': ([308, 457], [78410], 'weapon', 'or'), 'axe': ([307, 310], [78502], 'weapon', 'argent'), 'battle-axe': ([310, 307], [], 'weapon', 'argent'),
    'halberd': ([306], [], 'weapon', 'argent'), 'mace': ([303, 305], [78406], 'weapon', 'cendree'), 'morningstar': ([303], [78406], 'weapon', 'cendree'),
    'hammer': ([445], [], 'weapon', 'cendree'), 'sledgehammer': ([445], [], 'weapon', 'cendree'), 'warhammer': ([445], [], 'weapon', 'cendree'),
    'battering ram': ([445], [], 'weapon', 'sable'), 'bow': ([311, 312], [], 'weapon', 'brun'), 'longbow': ([311, 312], [], 'weapon', 'or'),
    'arrow': ([314, 457], [], 'weapon', 'brun'), 'crossbow': ([315], [], 'weapon', 'sable'), 'scythe': ([309], [], 'weapon', 'argent'),
    'pitchfork': ([308], [], 'tool', 'brun'), 'scourge': ([303], [], 'weapon', 'sable'), 'whip': ([303], [], 'weapon', 'sable'),
    'shield': ([316, 317], [], 'tool', 'argent'), 'escutcheon': ([316], [], 'tool', 'argent'), 'helm': ([346, 345], [], 'tool', 'cendree'),
    'helmet': ([345, 346], [], 'tool', 'cendree'), 'greathelm': ([346], [], 'tool', 'cendree'), 'crown': ([545, 340], [], 'crown', 'or'),
    'coronet': ([340], [], 'crown', 'or'), 'chalice': ([321], [], 'tool', 'or'), 'winged chalice': ([321], [], 'tool', 'argent'),
    'goblet': ([321], [], 'tool', 'or'), 'cup': ([321], [], 'tool', 'or'), 'flagon': ([321], [], 'tool', 'or'), 'jug': ([321], [], 'tool', 'brun'),
    'kettle': ([321], [], 'tool', 'sable'), 'bell': ([321], [], 'tool', 'argent'), 'pepperpot': ([321], [], 'tool', 'brun'),
    'barrel': ([506], [], 'shape', 'brun'), 'bucket': ([321], [], 'tool', 'brun'), 'woolsack': ([506], [], 'shape', 'argent'),
    'beehive': ([506], [], 'shape', 'or'), 'book': ([505], [], 'tool', 'sable'), 'scroll': ([343], [], 'tool', 'argent'),
    'spoon': ([343], [], 'tool', 'brun'), 'staff': ([343], [], 'tool', 'brun'), 'sceptre': ([343], [], 'tool', 'or'),
    'key': ([336], [], 'tool', 'or'), 'keys': ([336], [], 'tool', 'or'), 'scales': ([337], [], 'tool', 'or'), 'balances': ([337], [], 'tool', 'or'),
    'pair of balances': ([337], [], 'tool', 'or'), 'pair of scales': ([337], [], 'tool', 'or'), 'harp': ([339], [], 'tool', 'or'),
    'lute': ([339], [], 'tool', 'or'), 'trumpet': ([339], [78514], 'tool', 'or'), 'horn': ([339], [78514, 78813], 'tool', 'or'),
    'hunting horn': ([339], [78514, 78813], 'tool', 'or'), 'wheel': ([405, 410, 409], [78212], 'tool', 'brun'), 'broken wheel': ([405], [78212], 'tool', 'sable'),
    'horseshoe': ([335], [], 'tool', 'sable'), 'anchor': ([304], [], 'tool', 'sable'), 'oar': ([304], [], 'tool', 'brun'),
    'buckle': ([402], [], 'tool', 'or'), 'brooch': ([402], [], 'tool', 'or'), 'chain': ([404], [75503, 78301], 'tool', 'argent'),
    'ring': ([500, 403], [], 'shape', 'or'), 'annulet': ([500], [], 'shape', 'or'), 'noose': ([500], [], 'shape', 'sable'),
    'nail': ([304], [], 'tool', 'copper'), 'brazier': ([338], [], 'tool', 'sable'), 'candle': ([338], [], 'tool', 'or'),
    'lantern': ([328], [], 'building', 'or'), 'throne': ([320], [], 'tool', 'or'), 'boot': ([335], [], 'tool', 'sable'),
    'maunch': ([506], [], 'shape', 'or'), 'cartouche': ([506], [], 'shape', 'argent'), 'trebuchet': ([334], [], 'tool', 'brun'),
    'castle': ([325, 326], [], 'building', 'cendree'), 'tower': ([327, 326], [], 'building', 'cendree'), 'gatehouse': ([326], [], 'building', 'cendree'),
    'bridge': ([330], [78308], 'building', 'cendree'), 'gate': ([323, 331], [], 'building', 'brun'), 'door': ([331, 323], [], 'building', 'brun'),
    'doorway': ([323], [], 'building', 'cendree'), 'portcullis': ([331], [78301], 'building', 'sable'), 'pavilion': ([329], [], 'building', 'or'),
    'tent': ([329], [], 'building', 'or'), 'column': ([322], [], 'building', 'argent'), 'pyramid': ([452], [], 'building', 'or'),
    'wall': ([324], [], 'building', 'cendree'), 'maze': ([406], [], 'knot', 'argent'),
    'ship': ([333, 332], [], 'ship', 'brun'), 'longship': ([332, 166], [], 'ship', 'brun'), 'galley': ([332], [], 'ship', 'brun'), 'cog': ([333], [], 'ship', 'brun'),
    'sun': ([401, 521, 411], [], 'celestial', 'or'), 'sun-in-splendour': ([401, 521], [], 'celestial', 'or'), 'demi-sun-in-splendour': ([401], [], 'celestial', 'or'),
    'moon': ([427, 426], [], 'celestial', 'argent'), 'crescent': ([427, 428], [], 'celestial', 'argent'), 'crescent moon': ([427], [], 'celestial', 'argent'),
    'eclipse': ([427], [], 'celestial', 'sable'), 'star': ([516, 518, 515], [75103], 'celestial', 'or'), 'mullet': ([516], [], 'celestial', 'argent'),
    'estoile': ([518], [], 'celestial', 'or'), 'blazing star': ([518], [], 'celestial', 'argent'), 'falling star': ([516], [], 'celestial', 'argent'),
    'lightning': ([524], [], 'celestial', 'or'), 'lightning bolt': ([524], [], 'celestial', 'or'), 'thunderbolt': ([524], [], 'celestial', 'or'),
    'wave': ([426], [], 'celestial', 'seagreen'), 'flame': ([401], [], 'celestial', 'tenne'), 'fire': ([401], [], 'celestial', 'tenne'),
    'heart': ([514], [], 'shape', 'gules'), 'winged heart': ([514], [], 'shape', 'sable'), 'drop': ([506], [], 'shape', 'sanguine'),
    'hand': ([341, 342], [], 'human', 'carnation'), 'fist': ([342], [], 'human', 'carnation'), 'gauntlet': ([342], [], 'human', 'cendree'),
    'skull': ([344], [78313], 'human', 'argent'), "death's head": ([344], [78313], 'human', 'argent'), 'head': ([344], [], 'human', 'carnation'),
    "woman's head": ([344], [], 'human', 'carnation'), "sea king's head": ([344], [78704], 'human', 'argent'), 'stone head': ([344], [], 'human', 'cendree'),
    "mother's head": ([344], [], 'human', 'carnation'), 'face': ([344], [], 'human', 'carnation'), 'mask': ([344, 347], [], 'human', 'argent'),
    'lips': ([514], [], 'human', 'carnation'), 'footprint': ([506], [], 'shape', 'argent'), 'man': ([443], [], 'human', 'carnation'),
    'flayed man': ([443], [78304], 'human', 'gules'), 'hanged man': ([443], [], 'human', 'sable'), 'hooded man': ([443], [], 'human', 'sable'),
    'fool': ([443], [78505], 'human', 'carnation'), 'knight': ([129], [], 'human', 'cendree'), 'woman': ([443], [], 'human', 'carnation'),
    'roundel': ([503], [], 'shape', None), 'plate': ([503], [], 'shape', 'argent'), 'bezant': ([503], [], 'shape', 'or'),
    'coin': ([503], [], 'shape', 'or'), 'hurt': ([503], [], 'shape', 'azure'), 'pellet': ([503], [], 'shape', 'sable'),
    'torteau': ([503], [], 'shape', 'gules'), 'golpe': ([503], [], 'shape', 'purpure'), 'fountain': ([503], [], 'shape', 'azure'),
    'disk': ([503], [], 'shape', 'bronze'), 'lozenge': ([512], [], 'shape', None), 'diamond': ([512], [], 'shape', None),
    'mascle': ([502], [], 'shape', None), 'billet': ([505], [], 'shape', None), 'cross': ([418, 417], [], 'cross', None),
    'escarbuncle': ([419], [78909], 'cross', 'or'), 'triskele': ([413], [], 'knot', None), 'knot': ([406], [], 'knot', None),
    'rune': ([408], [], 'glyph', None), 'runes': ([408], [], 'glyph', None),
}
# rodzina nieznanego godla -> zastepca z tej samej rodziny (ikona gry), potem neutralne godlo (gwiazda)
FAMILY_ICON = {'bird': 115, 'beast': 146, 'monster': 120, 'fish': 159, 'flora': 220, 'weapon': 301, 'tool': 337, 'building': 327,
               'celestial': 516, 'human': 344, 'ship': 333, 'reptile': 154, 'vermin': 156, 'crown': 340, 'cross': 418, 'knot': 406,
               'glyph': 408, 'shape': 503}
NEUTRAL = 516
# slowa spoza LEX -> rodzina (zastepca)
FAMILY_WORDS = {
    'bird': 'pelican seabird gull magpie dove lark nightingale stork kingfisher osprey kite jay finch sparrow wren'.split(),
    'beast': 'mouse rat hare rabbit bull calf cow lynx panther tiger wolfhound boarhound'.split(),
    'vermin': 'fly locust cricket grasshopper scarab'.split(),
    'tool': 'saddle plough plow pick shovel spade sickle quiver cauldron pot bowl jar lamp torch net'.split(),
    'monster': 'basilisk chimera hydra gargoyle'.split(),
}


def _plurals(noun):
    """'wolf' -> wolves; 'leaf' -> leaves; 'knife' -> knives; ostatnie slowo frazy."""
    parts = noun.split(' ')
    w = parts[-1]
    irr = {'wolf': ['wolves'], 'leaf': ['leaves'], 'knife': ['knives'], 'fish': ['fishes', 'fish'], 'goose': ['geese'], 'man': ['men'],
           'woman': ['women'], 'ox': ['oxen'], 'mouse': ['mice'], 'foot': ['feet'], 'tooth': ['teeth'], 'staff': ['staves', 'staffs'],
           'sheep': ['sheep'], 'deer': ['deer'], 'elk': ['elk'], 'moose': ['moose'], 'grapes': ['grapes'], 'scales': ['scales'],
           'balances': ['balances'], 'keys': ['keys'], 'antlers': ['antlers'], 'lips': ['lips'], 'runes': ['runes'], 'thorns': ['thorns']}
    if w in irr:
        forms = irr[w]
    elif w.endswith(('s', 'x', 'ch', 'sh')):
        forms = [w + 'es']
    elif w.endswith('y') and len(w) > 2 and w[-2] not in 'aeiou':
        forms = [w[:-1] + 'ies']
    else:
        forms = [w + 's']
    return [' '.join(parts[:-1] + [f]) for f in forms]


def norm_text(s):
    t = s.replace('’', "'").replace('‘', "'").replace('é', 'e').replace('â', 'a').replace('è', 'e')
    t = t.replace('ê', 'e').replace('ç', 'c').replace('ô', 'o')
    t = t.lower()
    t = re.sub(r'\bfesse\b', 'fess', t)
    t = t.replace('gouttee-de-sang', 'gouttee').replace('goutte-de-sang', 'gouttee').replace('de sang', 'gouttee')
    return t


# ------------------------------------------------------------------------------------------------ slownik tokenow
ORDS = ['double tressure', 'double-tressure', 'tressure', 'bordure', 'border', 'orle', 'chief', 'fess', 'bars gemel', 'bars-gemels', 'bar sinister',
        'bar', 'pale', 'pallet', 'bend sinister', 'bend', 'bendlet', 'chevron inverted', 'chevron', 'chevronel', 'cross', 'saltire', 'pile inverted',
        'pile', 'pall reversed', 'pall inverted', 'inverted pall', 'pall', 'canton', 'escutcheon', 'inescutcheon', 'lozenge', 'fret', 'flaunch',
        'flanch', 'flanche', 'tierce', 'cartouche']
ORD_CANON = {'border': 'bordure', 'double-tressure': 'double tressure', 'bars gemel': 'bars', 'bars-gemels': 'bars', 'bar sinister': 'bend sinister',
             'bar': 'bars', 'pallet': 'pallets', 'bendlet': 'bendlets', 'chevronel': 'chevron', 'flanch': 'flaunches', 'flanche': 'flaunches',
             'flaunch': 'flaunches', 'pall inverted': 'pall reversed', 'inverted pall': 'pall reversed', 'inescutcheon': 'escutcheon',
             'pile inverted': 'pile inverted'}
DIVS = [('per pale', 'pale'), ('per fess', 'fess'), ('per bend sinister', 'bendsin'), ('per bend', 'bend'), ('per chevron', 'chevron'),
        ('per saltire', 'saltire'), ('quarterly', 'quarterly'), ('quartered', 'quarterly'), ('gyronny', 'gyronny'), ('chequy', 'chequy'),
        ('checkered', 'chequy'), ('checked', 'chequy'), ('checks', 'chequy'), ('lozengy', 'lozengy'), ('fusily', 'lozengy'), ('diamonds', 'lozengy'),
        ('paly', 'paly'), ('barry', 'barry'), ('bendy sinister', 'bendysin'), ('bendy', 'bendy'), ('vairy-in-point', 'vair'), ('vairy', 'vair'),
        ('vair', 'vair'), ('pily', 'pily'), ('undy', 'barry'), ('wavy', 'barry'), ('stripes', 'paly'), ('striped', 'paly'), ('tierced in pale', 'tierce'),
        ('parted into six', 'paly'), ('parted into nine', 'chequy')]
MODS = ['embattled', 'engrailed', 'indented', 'wavy', 'rayonne', 'rayonny', 'nebuly', 'cotised', 'enhanced', 'inverted', 'reversed', 'undy',
        'saltirewise', 'saltirwise', 'in saltire', 'crossed', 'counter-salient', 'counter salient', 'combatant', 'addorsed', 'palewise', 'fesswise',
        'bendwise sinister', 'bendwise', 'bendways sinister', 'bendways', 'erect', 'upright', 'in fess', 'in pale', 'in bend',
        'rampant', 'passant', 'statant', 'salient', 'sejant', 'couchant', 'dormant', 'displayed', 'volant', 'rising', 'rousant', 'close',
        'naiant', 'hauriant', 'erased', 'couped', 'caboshed', 'cabossed', 'regardant', 'guardant', 'segreant', 'embowed', 'courant',
        'springing', 'trippant', 'queue-fourchee', 'queue-forchee', 'blasted', 'veined', 'increscent', 'decrescent',
        'demi', 'conjoined', 'interlaced', 'flying', 'soaring', 'leaping', 'running', 'sleeping', 'upside-down']
DETAIL = ['beaked', 'membered', 'armed', 'langued', 'crined', 'combed', 'attired', 'fructed', 'tipped', 'clasped', 'garnished', 'knobbed', 'orbed',
          'headed', 'bearded', 'holding', 'clutching', 'juggling', 'bearing', 'crested', 'clothed', 'lashed', 'banded', 'studded', 'spotted',
          'inflamed', 'flammant', 'thereon', 'with', 'breathing', 'standing', 'gammed', 'spilling', 'overflowing', 'surrounded', 'flashing',
          'issuant', 'bound and slung', 'bound', 'slung', 'its claws', 'crowned', 'bicapitated', 'two-headed', 'nine-headed', 'thrice-headed']
COUNTS = {'a': 1, 'an': 1, 'one': 1, 'two': 2, 'three': 3, 'four': 4, 'five': 5, 'six': 6, 'seven': 7, 'eight': 8, 'nine': 9, 'ten': 10,
          'pair of': 2, 'a pair of': 2, 'brace of': 2, 'as many': None}


def build_lexicon(icon_rows):
    """LEX + slowa kluczowe ikon (heraldic=yes) -> slownik fraza -> klucz LEX. Zwraca (fraza->rzeczownik, rzeczownik->(act, ccs, fam, proper))."""
    lex = dict(LEX)
    act_ids = {int(r['icon_id']) for r in icon_rows if r['loaded_now'] == 'yes'}
    by_kw_act, by_kw_ccs, fam_kw = collections.defaultdict(list), collections.defaultdict(list), {}
    for r in icon_rows:
        if r['heraldic'] != 'yes':
            continue
        iid = int(r['icon_id'])
        for k in r['keywords'].split('|'):
            k = k.strip().lower()
            if not k or any(re.search(r'\b' + re.escape(o) + r's?\b', k) for o in ORDS if o not in ('lozenge', 'escutcheon', 'cartouche')):
                continue
            if k.split(' ')[0] in TINCT_WORDS and ' ' in k:
                continue
            (by_kw_act if iid in act_ids else by_kw_ccs)[k].append(iid)
            fam_kw.setdefault(k, r['family'])
    for k in set(by_kw_act) | set(by_kw_ccs):
        if k in lex:
            a, c, f, p = lex[k]
            if not a and by_kw_act.get(k):
                a = by_kw_act[k]
            if not c and by_kw_ccs.get(k):
                c = by_kw_ccs[k]
            lex[k] = (a, c, f, p)
        else:
            lex[k] = (by_kw_act.get(k, []), by_kw_ccs.get(k, []), fam_kw.get(k, 'shape'), None)
    for fam, words in FAMILY_WORDS.items():
        for w in words:
            if w not in lex:
                lex[w] = ([], [], fam, None)
    phrase = {}
    for n in lex:
        phrase[n] = n
    for n in lex:
        for p in _plurals(n):
            if n in LEX or p not in phrase:
                phrase[p] = n
    return phrase, lex


class Tok(object):
    __slots__ = ('kind', 'v', 'pos')

    def __init__(self, kind, v, pos):
        self.kind, self.v, self.pos = kind, v, pos

    def __repr__(self):
        return '%s:%s' % (self.kind, self.v)


def tokenize(text, phrase):
    """Tekst (znormalizowany) -> tokeny: sep, cnt, tin, div, ord, mod, det, kw, chg. Najdluzsza fraza wygrywa."""
    table = []
    for p, k in TINCT_PHRASES:
        table.append((p, 'tin', k))
    for p, k in DIVS:
        table.append((p, 'div', k))
    for p in ORDS:
        table.append((p, 'ord', ORD_CANON.get(p, p)))
        if p in ('bars gemel', 'bars-gemels', 'double tressure', 'double-tressure'):
            continue
        head = p.split(' ')[0]
        for q in _plurals(head):
            table.append((q + p[len(head):], 'ord', ORD_CANON.get(p, p)))
    for p in MODS:
        table.append((p, 'mod', p.replace('saltirwise', 'saltirewise').replace('counter salient', 'counter-salient').replace('bendways', 'bendwise')))
    for p in DETAIL:
        table.append((p, 'det', p))
    for p, n in COUNTS.items():
        table.append((p, 'cnt', n))
    for p in ['on', 'upon', 'between', 'within', 'surmounted by', 'surmounted', 'debruised by', 'debruised', 'over', 'overall', 'beneath', 'below',
              'above', 'under', 'in chief', 'in base', 'in dexter chief', 'in sinister chief', 'in dexter base', 'in sinister base', 'in dexter',
              'in sinister', 'of the field', 'of the first', 'of the second', 'of the third', 'of the last', 'of the same', 'the last', 'proper',
              'counterchanged', 'countercharged', 'counter-charged', 'seme of', 'seme', 'strewn with', 'strewn on', 'strewn', 'field of', 'flock of',
              'shoal of', 'a field of', 'charged with', 'each charged with', 'each', 'all', 'and', 'masoned', 'fretty', 'gouttee', 'plate',
              'formerly', 'unknown', 'colours', 'colors', 'field', 'around', 'surrounding', 'across', 'piercing', 'transfixed by', 'atop',
              'against', 'in a', 'in its', 'clockwise', 'quarters', '1st', '2nd', '3rd', '4th', 'first and third', 'second and fourth',
              'first and fourth', 'second and third']:
        table.append((p, 'kw', p))
    for p, n in phrase.items():
        table.append((p, 'chg', n))
    table.sort(key=lambda x: -len(x[0]))
    out = []
    i, n = 0, len(text)
    while i < n:
        ch = text[i]
        if ch in ',;:()/':
            out.append(Tok('sep', ch, i))
            i += 1
            continue
        if not (ch.isalnum() or ch in "'-"):
            i += 1
            continue
        if i > 0 and (text[i - 1].isalnum() or text[i - 1] in "'-"):
            i += 1
            continue
        m = re.match(r'(\d+(?:-\d+)+)\b', text[i:])
        if m:
            out.append(Tok('cnt', sum(int(x) for x in m.group(1).split('-')), i))
            i += len(m.group(1))
            continue
        m = re.match(r'(\d+)\b', text[i:])
        if m:
            out.append(Tok('cnt', int(m.group(1)), i))
            i += len(m.group(1))
            continue
        hit = None
        for p, kind, v in table:
            if text.startswith(p, i):
                j = i + len(p)
                if j < n and (text[j].isalnum() or (text[j] == '-' and kind != 'chg')):
                    continue
                if j < n and text[j] == "'" and j + 1 < n and text[j + 1] == 's':
                    continue
                hit = (p, kind, v)
                break
        if hit:
            p, kind, v = hit
            if kind == 'tin' and p in ('rose', 'roses') and out and (out[-1].kind == 'cnt' or (out[-1].kind == 'kw' and out[-1].v in ('seme of', 'strewn with'))):
                kind, v = 'chg', 'rose'
            out.append(Tok(kind, v, i))
            i += len(p)
        else:
            m = re.match(r"[a-z0-9'-]+", text[i:])
            w = m.group(0) if m else text[i]
            out.append(Tok('word', w, i))
            i += len(w)
    return out


# ------------------------------------------------------------------------------------------------ rozbior herbu
class Item(object):
    def __init__(self, kind, name, count=1, tin=None):
        self.kind, self.name, self.count, self.tin = kind, name, count, tin
        self.mods, self.role, self.host, self.pos, self.tins = [], 'main', None, None, []

    def __repr__(self):
        return '%s(%s x%s %s %s%s)' % (self.kind, self.name, self.count, self.tin, self.role, ' on ' + self.host.name if self.host else '')


class Arms(object):
    def __init__(self):
        self.div, self.field, self.items, self.quarters, self.notes = None, [], [], None, []


def split_blazon(b):
    """('English', 'technical') - techniczny z ostatnich nawiasow; 'Formerly' / '/' - tylko pierwszy herb."""
    b = b.strip()
    b = re.split(r'\s/\s|\bFormerly\b', b)[0].strip()
    tech = ''
    m = re.search(r'\(([^()]*)\)\s*$', b)
    if m:
        tech = m.group(1).strip()
        b = b[:m.start()].strip()
    return b, tech


def resolve_ref(word, mentioned):
    if word in ('of the field', 'of the first'):
        return mentioned[0] if mentioned else None
    if word == 'of the second':
        return mentioned[1] if len(mentioned) > 1 else None
    if word == 'of the third':
        return mentioned[2] if len(mentioned) > 2 else None
    if word in ('of the last', 'of the same', 'the last'):
        return mentioned[-1] if mentioned else None
    return None


def parse_field_tokens(toks):
    """Poczatek blazonu technicznego do pierwszego przecinka (z ogonem samych tynktur: 'Barry wavy vert, argent, and or')."""
    div, tins, k = None, [], 0
    fret = gout = False
    while k < len(toks):
        t = toks[k]
        if t.kind == 'sep' and t.v == ',':
            # ogon samych tynktur po przecinku
            j, more = k + 1, []
            while j < len(toks) and (toks[j].kind == 'tin' or (toks[j].kind == 'kw' and toks[j].v == 'and')):
                if toks[j].kind == 'tin':
                    more.append(toks[j].v)
                j += 1
            if more and (j >= len(toks) or toks[j].kind == 'sep') and div is not None:
                tins += more
                k = j
                continue
            break
        if t.kind == 'div' and div is None:
            div = t.v
        elif t.kind == 'tin':
            tins.append(t.v)
        elif t.kind == 'kw' and t.v == 'fretty':
            fret = True
        elif t.kind == 'kw' and t.v == 'gouttee':
            gout = True
        elif t.kind in ('ord', 'chg', 'cnt'):
            break
        k += 1
    return div, tins, fret, gout, k


def parse_items(toks, mentioned, field, tech=False):
    """Godla po polu: zwyczajne (ord) i figury (chg) z rolami main / on (na nosicielu) / around (miedzy) / over (na wierzchu).
    tech = blazon techniczny: tynktura PO rzeczowniku (wolna tynktura przed nim nie nalezy do nastepnej figury)."""
    items = []
    mode, host, between_for = None, None, None
    pcount, ptins, last, skip, pos = None, [], None, False, None
    prev_kind = None
    for t in toks:
        if t.kind == 'sep':
            pcount, ptins, skip = None, [], False
            prev_kind = 'sep'
            if t.v in (';', ':'):
                mode, host = None, None
            continue
        if skip:
            if t.kind == 'kw' and t.v in ('within', 'between', 'on', 'upon', 'all'):
                skip = False
            else:
                continue
        if t.kind == 'det':
            if last is not None and t.v in ('two-headed', 'bicapitated') and last.kind == 'chg' and last.name == 'eagle':
                last.name = 'double-headed eagle'
            if last is not None and t.v == 'thrice-headed' and last.kind == 'chg' and last.name == 'dragon':
                last.name = 'three-headed dragon'
            if t.v in ('crowned',) and last is not None:
                last.mods.append('crowned')
            skip = t.v not in ('two-headed', 'bicapitated', 'thrice-headed', 'nine-headed', 'crowned')
            if t.v == 'crowned' and tech:
                skip = True
            prev_kind = 'mod' if not skip else 'det'
            continue
        if t.kind == 'cnt':
            pcount = t.v
            prev_kind = 'cnt'
            continue
        if t.kind == 'mod':
            if last is not None:
                last.mods.append(t.v)
            prev_kind = 'mod'
            continue
        if t.kind == 'kw':
            v = t.v
            ref = resolve_ref(v, mentioned)
            if ref is not None or v in ('of the field', 'of the first', 'of the second', 'of the third', 'of the last', 'of the same', 'the last'):
                tv = ref
                if last is not None and last.tin is None and prev_kind in ('chg', 'ord', 'mod'):
                    last.tin = tv
                    if tv:
                        last.tins.append(tv)
                prev_kind = 'tin'
                continue
            if v == 'proper':
                if last is not None and last.tin is None:
                    last.tin = 'proper'
                prev_kind = 'tin'
                continue
            if v in ('counterchanged', 'countercharged', 'counter-charged'):
                if last is not None:
                    last.tin = 'counter'
                prev_kind = 'tin'
                continue
            if v in ('charged with', 'each charged with', 'thereon') and last is not None:
                host = last
                mode = 'onhost'
                prev_kind = 'kw'
                continue
            if v in ('on', 'upon', 'charged with', 'each charged with', 'atop'):
                if v == 'atop' and last is not None:
                    host = last
                    mode = 'onhost-rev'
                else:
                    mode = 'on'
                prev_kind = 'kw'
                continue
            if v in ('between', 'surrounding', 'around'):
                mode = 'between'
                between_for = last
                prev_kind = 'kw'
                continue
            if v == 'within':
                mode = 'within'
                prev_kind = 'kw'
                continue
            if v in ('surmounted by', 'surmounted', 'debruised by', 'debruised', 'over', 'overall', 'across', 'piercing', 'transfixed by'):
                mode = 'over'
                prev_kind = 'kw'
                continue
            if v in ('beneath', 'below', 'under'):
                mode = 'beneath'
                prev_kind = 'kw'
                continue
            if v.startswith('in ') and v not in ('in a', 'in its'):
                pos = v
                prev_kind = 'kw'
                continue
            if v in ('seme of', 'seme', 'strewn with', 'strewn', 'field of', 'a field of', 'flock of', 'shoal of', 'strewn on'):
                pcount = 'seme'
                prev_kind = 'kw'
                continue
            if v == 'plate':
                t = Tok('chg', 'plate', t.pos)
            else:
                prev_kind = 'kw'
                continue
        if t.kind == 'tin':
            mentioned.append(t.v)
            if last is not None and last.tin is None and prev_kind in ('chg', 'ord', 'mod'):
                last.tin = t.v
                last.tins.append(t.v)
            elif last is not None and prev_kind == 'tin' and last.tins and t.kind == 'tin':
                last.tins.append(t.v)
            else:
                ptins.append(t.v)
            prev_kind = 'tin'
            continue
        if t.kind in ('ord', 'chg'):
            if t.kind == 'chg' and prev_kind == 'of' and last is not None:
                if last.kind == 'ord' and last.name in ('orle', 'bordure', 'tressure'):
                    # 'an orle of pellets', 'a bordure of three rows of peas' - figury w obramowaniu
                    it = Item('chg', t.v, 8, ptins[-1] if ptins and not tech else None)
                    it.role, it.host = 'orle', last
                    items.append(it)
                    last = it
                    prev_kind = 'chg'
                    continue
                if last.kind == 'chg':
                    # 'a garland of laurel', 'sprigs of mistletoe' - dopelnienie pierwszego rzeczownika
                    prev_kind = 'chg'
                    continue
            it = Item(t.kind, t.v, pcount if pcount is not None else 1, ptins[-1] if ptins and not tech else None)
            if it.tin:
                it.tins.append(it.tin)
            it.pos = pos
            pos = None
            if mode == 'on':
                it.role = 'main'
                host = it
                mode = 'onhost'
            elif mode == 'onhost':
                it.role, it.host = 'on', host
            elif mode == 'onhost-rev':
                it.role, it.host = 'main', None
                if host is not None:
                    host.role, host.host = 'on', it
                mode = None
            elif mode == 'between':
                it.role = 'around'
                it.host = between_for
                if between_for is None:
                    it.host = 'next'
                mode = 'between-done' if between_for is None else None
            elif mode == 'between-done':
                it.role = 'main'
                for o in items:
                    if o.host == 'next':
                        o.host = it
                mode = None
            elif mode == 'within':
                it.role = 'main'
                mode = None
            elif mode == 'over':
                it.role = 'over'
                mode = None
            elif mode == 'beneath':
                it.role = 'main'
                mode = None
            items.append(it)
            last = it
            pcount, ptins = None, []
            prev_kind = t.kind
            continue
        if t.kind == 'word':
            prev_kind = 'of' if t.v == 'of' else 'word'
            continue
        if t.kind == 'div':
            prev_kind = 'div'
            continue
    for o in items:
        if o.host == 'next':
            o.host = None
            o.role = 'main'
        if o.kind == 'ord' and o.role == 'around':
            o.role, o.host = 'main', None
        if o.kind == 'chg' and o.name == 'lion' and 'dormant' in o.mods:
            o.name = 'sleeping lion'
    return items


def parse_english_field(toks):
    """Pole z opisu slowami: ostatnie 'on [a] [field of] <podzial> <tynktura> [and <tynktura>] [field]' nie zakonczone godlem."""
    best = None
    for k, t in enumerate(toks):
        if t.kind == 'kw' and t.v in ('on', 'upon', 'over', 'against'):
            j = k + 1
            div, tins = None, []
            while j < len(toks) and toks[j].kind in ('cnt', 'div', 'tin', 'kw', 'mod', 'word') and not (toks[j].kind == 'kw' and toks[j].v in ('on', 'upon', 'between', 'within', 'beneath')):
                tj = toks[j]
                if tj.kind == 'div':
                    div = div or tj.v
                elif tj.kind == 'tin':
                    tins.append(tj.v)
                elif tj.kind == 'word' and tj.v not in ('field', 'a', 'an'):
                    break
                j += 1
            if j < len(toks) and toks[j].kind in ('ord', 'chg'):
                continue
            if tins:
                best = (k, j, div, tins)
    m = None
    for k, t in enumerate(toks):
        if t.kind == 'tin' and k + 1 < len(toks) and toks[k + 1].kind == 'kw' and toks[k + 1].v == 'field':
            m = (k, k + 2, None, [t.v])
    if best is None and m is not None:
        best = m
    return best


def parse_arms(blazon, phrase, house):
    eng, tech = split_blazon(blazon)
    a = Arms()
    src = tech if tech and re.search(r'[a-z]', tech) else ''
    E = norm_text(eng)
    T = norm_text(src)
    etoks = tokenize(E, phrase)
    if T:
        ttoks = tokenize(T, phrase)
        div, tins, fret, gout, k = parse_field_tokens(ttoks)
        rest = ttoks[k:]
        mentioned = list(tins)
        # quarterly z figurami w cwiartkach (1st and 3rd ... 2nd and 4th ...)
        if div == 'quarterly' and any(t.kind == 'kw' and t.v in ('1st', '2nd', 'first and third', 'second and fourth', 'first and fourth', 'second and third') for t in rest):
            return parse_quarters(T, phrase, house, a)
        items = parse_items(rest, mentioned, tins[0] if tins else None, tech=True)
        # tynktury z opisu slowami, gdy techniczny ich nie podal: ta sama figura, inaczej ta na tym samym miejscu wsrod figur
        eitems = parse_items([t for t in etoks], [], None)
        echg = [e for e in eitems if e.kind == 'chg']
        ichg = [i for i in items if i.kind == 'chg']
        for it in items:
            if it.tin is None:
                for e in eitems:
                    if e.name == it.name and e.tin:
                        it.tin = e.tin
                        break
            if it.tin is None and it.kind == 'chg' and len(echg) == len(ichg):
                e = echg[ichg.index(it)]
                if e.tin:
                    it.tin = e.tin
        if not tins:
            ef = parse_english_field(etoks)
            if ef:
                div = div or ef[2]
                tins = ef[3]
        if fret:
            items.insert(0, Item('ord', 'fret', 1, tins[1] if len(tins) > 1 else None))
            tins = tins[:1]
        if gout:
            items.insert(0, Item('chg', 'drop', 'seme', 'sanguine'))
        a.div, a.field, a.items = div, tins, items
        a.notes.append('technical')
    else:
        if re.match(r'^\s*(quarterly|quartered)\s*:', E):
            return parse_quarters(E, phrase, house, a)
        ef = parse_english_field(etoks)
        cut = len(etoks)
        div, tins = None, []
        if ef:
            k, j, div, tins = ef
            sub = etoks[:k] + etoks[j:]
        else:
            sub = etoks
            # 'Undy green, white, and yellow' / 'Pily flame and black' / 'Barry red and pink' - pole na poczatku
            if etoks and etoks[0].kind == 'div':
                div = etoks[0].v
                j = 1
                while j < len(etoks) and etoks[j].kind in ('tin', 'kw', 'sep') and not (etoks[j].kind == 'sep' and etoks[j].v == ',' and j + 1 < len(etoks) and etoks[j + 1].kind not in ('tin', 'kw')):
                    if etoks[j].kind == 'tin':
                        tins.append(etoks[j].v)
                    j += 1
                sub = etoks[j:]
            elif etoks and etoks[0].kind == 'kw' and etoks[0].v in ('colours', 'colors'):
                tins = [t.v for t in etoks if t.kind == 'tin']
                div = 'pale' if len(tins) > 1 else None
                sub = []
        items = parse_items(sub, list(tins), tins[0] if tins else None)
        a.div, a.field, a.items = div, tins, items
        a.notes.append('english')
    return a


def parse_quarters(T, phrase, house, a):
    """Cwiartki: 'Quarterly clockwise, 1st and 3rd or, an apple tree proper, 2nd and 4th argent, a gatehouse cendree' albo
    'Quartered: a silver chalice on black, a black rose on yellow' -> dwie polowy (tynktura pola, godla)."""
    body = re.sub(r'^\s*(quarterly( clockwise)?|quartered)\s*[:,]?\s*', '', T)
    parts = re.split(r',?\s*\b(?:2nd and 4th|second and fourth|second and third|2nd and 3rd)\b', body, maxsplit=1)
    halves = []
    if len(parts) == 2:
        for p in parts:
            p = re.sub(r'^\s*(1st and 3rd|first and third|first and fourth|1st and 4th)\s*,?', '', p).strip(' ,')
            halves.append(p)
    else:
        bits = [x.strip() for x in re.split(r',(?![^()]*\))', body) if x.strip()]
        if len(bits) >= 2:
            halves = [bits[0], ', '.join(bits[1:])]
    out = []
    for h in halves[:2]:
        toks = tokenize(h, phrase)
        ef = parse_english_field(toks)
        tins, sub = [], toks
        if ef:
            k, j, _, tins = ef
            sub = toks[:k] + toks[j:]
        elif toks and toks[0].kind == 'tin':
            tins = [toks[0].v]
            sub = toks[1:]
        items = parse_items(sub, list(tins), tins[0] if tins else None, tech=True)
        out.append((tins[0] if tins else None, items))
    a.div = 'quarterly'
    a.quarters = out
    a.field = [q[0] for q in out]
    a.notes.append('quarters')
    return a


# ------------------------------------------------------------------------------------------------ herb bez opisu (kanting z nazwiska)
CANT = [('wolf', 'wolf'), ('bear', 'bear'), ('boar', 'boar'), ('stag', 'stag'), ('hart', 'stag'), ('hawk', 'falcon'), ('falcon', 'falcon'),
        ('raven', 'raven'), ('crow', 'crow'), ('swan', 'swan'), ('fox', 'fox'), ('hound', 'hound'), ('horse', 'horse'), ('bull', 'ox'),
        ('lion', 'lion'), ('fish', 'fish'), ('wood', 'tree'), ('tree', 'tree'), ('ash', 'tree'), ('oak', 'oak leaf'), ('elm', 'tree'),
        ('birch', 'tree'), ('byrch', 'tree'), ('pine', 'pine tree'), ('branch', 'branch'), ('bole', 'tree'), ('leaf', 'leaf'), ('briar', 'rose'),
        ('thorn', 'thorns'), ('rose', 'rose'), ('apple', 'apple'), ('berry', 'grapes'), ('stone', 'tower'), ('rock', 'tower'),
        ('tower', 'tower'), ('castle', 'castle'), ('keep', 'castle'), ('hall', 'castle'), ('gate', 'gate'), ('bridge', 'bridge'),
        ('brook', 'fish'), ('water', 'fish'), ('well', 'fish'), ('pool', 'fish'), ('lake', 'fish'), ('mere', 'fish'), ('river', 'fish'),
        ('myre', 'water lily'), ('mire', 'water lily'), ('marsh', 'frog'), ('fen', 'water lily'), ('bog', 'frog'), ('ford', 'fish'),
        ('hill', 'tower'), ('mount', 'tower'), ('star', 'star'), ('sun', 'sun'), ('moon', 'crescent'), ('sword', 'sword'), ('axe', 'axe'),
        ('spear', 'spear'), ('lance', 'lance'), ('bow', 'bow'), ('arrow', 'arrow'), ('shield', 'escutcheon'), ('helm', 'helm'),
        ('crown', 'crown'), ('king', 'crown'), ('ship', 'ship'), ('sail', 'ship'), ('salt', 'fish'), ('sea', 'ship'), ('wave', 'wave'),
        ('smith', 'hammer'), ('hammer', 'hammer'), ('mill', 'wheel'), ('wheel', 'wheel'), ('bell', 'bell'), ('horn', 'hunting horn'),
        ('field', 'wheat'), ('corn', 'wheat'), ('wheat', 'wheat'), ('hay', 'wheat'), ('grain', 'wheat'), ('flower', 'flower'),
        ('lily', 'lily'), ('heart', 'heart'), ('hand', 'hand'), ('key', 'key'), ('cross', 'cross'), ('knot', 'knot'), ('ring', 'annulet'),
        ('frost', 'mullet'), ('snow', 'mullet'), ('ice', 'mullet'), ('winter', 'mullet'), ('flint', 'tower'), ('iron', 'hammer'),
        ('gold', 'bezant'), ('silver', 'plate'), ('night', 'crescent'), ('dawn', 'sun'), ('day', 'sun'), ('storm', 'lightning bolt'),
        ('thunder', 'lightning bolt'), ('rain', 'drop'), ('cliff', 'tower'), ('wyck', 'tower'), ('wyk', 'tower'), ('pyke', 'fish'),
        ('drum', 'tower'), ('reed', 'leaf'), ('moss', 'leaf'), ('sand', 'sun'), ('dune', 'sun'), ('wind', 'wing'), ('wing', 'wing'),
        ('feather', 'feather'), ('quill', 'quill'), ('book', 'book'), ('cup', 'chalice'), ('pyr', 'pyramid'), ('zak', 'pyramid')]
CANT_TINCT = [('black', 'sable'), ('white', 'argent'), ('green', 'vert'), ('red', 'gules'), ('gold', 'or'), ('grey', 'cendree'),
              ('gray', 'cendree'), ('brown', 'brun'), ('blue', 'azure'), ('snow', 'argent'), ('frost', 'argent'), ('ice', 'argent'),
              ('silver', 'argent'), ('ash', 'cendree'), ('night', 'sable'), ('dark', 'sable')]
NEUTRALS = ['mullet', 'cross', 'fleur-de-lis', 'roundel', 'crescent', 'lozenge', 'annulet', 'trefoil', 'cinquefoil', 'martlet', 'estoile']
ORD_PICK = [None, None, 'fess', 'chevron', 'bend', 'pale', 'chief', 'bordure', 'saltire', 'cross', None, 'bend sinister']
AREA_FLAVOUR = {'SlaversBay': 'pyramid', 'Braavos': 'galley', 'Lys': 'fleur-de-lis', 'Volantis': 'tower', 'IronIslands': 'longship'}


def canting_arms(house, area):
    """Rod bez herbu w kanonie: godlo z nazwiska (kanting, jak w heraldyce), inaczej neutralne; barwy z nazwiska albo z crc (zasada tynktur)."""
    a = Arms()
    core = house_base(house.split('|')[0]).replace('House ', '').lower()
    h = crc(house)
    noun = None
    for k, n in CANT:
        if k in core:
            noun = n
            break
    if noun is None and area in AREA_FLAVOUR and h % 2 == 0:
        noun = AREA_FLAVOUR[area]
    field = None
    for k, tt in CANT_TINCT:
        if k in core:
            field = tt
            break
    if field is None:
        field = ['azure', 'gules', 'vert', 'sable', 'or', 'argent', 'purpure', 'brun', 'cendree', 'tenne'][(h >> 4) % 10]
    if noun is None:
        noun = NEUTRALS[(h >> 8) % len(NEUTRALS)]
        if noun == 'escallop':
            noun = 'seashell'
        a.notes.append('neutral')
    else:
        a.notes.append('canting')
    ordn = ORD_PICK[(h >> 12) % len(ORD_PICK)]
    tin = contrast(field, h >> 16)
    cnt = [1, 1, 1, 3, 1, 2, 3, 1][(h >> 20) % 8]
    a.field = [field]
    if ordn:
        otin = contrast(field, (h >> 24) + 1)
        a.items.append(Item('ord', ordn, 1, otin))
        if ordn in ('fess', 'chevron', 'bend', 'bend sinister', 'pale', 'saltire', 'cross'):
            cnt = 2 if ordn in ('bend', 'bend sinister', 'pale') else (3 if ordn in ('fess', 'chevron') else 4)
            it = Item('chg', noun, cnt, tin)
            it.role, it.host = 'around', a.items[0]
            a.items.append(it)
            return a
    a.items.append(Item('chg', noun, cnt, tin))
    return a


# ------------------------------------------------------------------------------------------------ rysunek
ORD_ICON = {'saltire': 522, 'cross': 418, 'lozenge': 512, 'chevron': 511, 'chevron inverted': 528, 'pile': 504, 'fess': 510, 'escutcheon': 316}
INTRINSIC = {'pellet': 'sable', 'plate': 'argent', 'bezant': 'or', 'hurt': 'azure', 'torteau': 'gules', 'golpe': 'purpure', 'pomme': 'vert',
             'fountain': 'azure', 'coin': 'or'}
ICON_KW = {}   # ikona -> slowa (keywords + nazwa godla) - czy dobrana ikona to DOKLADNIE to godlo, czy zastepca z rodziny


def is_exact(noun, iid):
    kw = ICON_KW.get(iid)
    if not kw:
        return False
    if noun in kw:
        return True
    last = noun.split(' ')[-1]
    return last in kw


class Painter(object):
    def __init__(self, lex, act_icons, ccs_icons, ccs):
        self.lex, self.act, self.ccsset, self.ccs = lex, act_icons, ccs_icons, ccs
        self.layers, self.notes, self.used_ccs = [], [], False

    def icon(self, noun):
        """(ikona, jak dobrana): dokladne godlo -> (CCs, gdy wariant CCs) -> ta sama rodzina -> neutralne."""
        e = self.lex.get(noun)
        if e:
            act, ccs, fam, _ = e
            # wariant CCs: ikona CCs tylko tam, gdzie gra nie ma dokladnego godla (sowa, zolw, byk, wiewiorka...)
            for i in act:
                if i in self.act and is_exact(noun, i):
                    return i, 'exact'
            if self.ccs:
                for i in ccs:
                    if i in self.ccsset:
                        self.used_ccs = True
                        return i, 'ccs'
            for i in act:
                if i in self.act:
                    return i, 'family:' + fam
            if fam in FAMILY_ICON:
                return FAMILY_ICON[fam], 'family:' + fam
        return NEUTRAL, 'neutral'

    def add(self, *lay):
        self.layers.append(L(*lay))


def col(t, field_t, h):
    t = t if t else contrast(field_t, h)
    return TINCT_COLOR.get(t, TINCT_COLOR['argent'])


def proper_of(noun, lex):
    e = lex.get(noun)
    return e[3] if e and e[3] else None


# rozmieszczenie n figur (przesuniecia od srodka, rozmiar) w polu o polowie boku hb
def arrangement(n, hb, mods=()):
    if n == 'seme':
        pts, s = [], hb * 0.36
        rows = [(-0.66, (-0.66, 0.0, 0.66)), (-0.22, (-0.33, 0.33)), (0.22, (-0.66, 0.0, 0.66)), (0.66, (-0.33, 0.33))]
        for y, xs in rows:
            for x in xs:
                pts.append((x * hb, y * hb))
        return pts, s
    n = max(1, min(int(n), 12))
    if n == 1:
        return [(0, 0)], hb * 1.55
    if n == 2:
        if 'in pale' in mods:
            return [(0, -0.48 * hb), (0, 0.48 * hb)], hb * 0.88
        return [(-0.46 * hb, 0), (0.46 * hb, 0)], hb * 0.86
    if n == 3:
        if 'in fess' in mods:
            return [(-0.62 * hb, 0), (0, 0), (0.62 * hb, 0)], hb * 0.6
        if 'in pale' in mods:
            return [(0, -0.62 * hb), (0, 0), (0, 0.62 * hb)], hb * 0.6
        return [(-0.44 * hb, -0.38 * hb), (0.44 * hb, -0.38 * hb), (0, 0.44 * hb)], hb * 0.74
    if n == 4:
        return [(-0.44 * hb, -0.42 * hb), (0.44 * hb, -0.42 * hb), (-0.44 * hb, 0.42 * hb), (0.44 * hb, 0.42 * hb)], hb * 0.7
    if n == 5:
        if 'in fess' in mods:
            return [((k - 2) * 0.4 * hb, 0) for k in range(5)], hb * 0.38
        return [(-0.5 * hb, -0.5 * hb), (0.5 * hb, -0.5 * hb), (0, 0), (-0.5 * hb, 0.5 * hb), (0.5 * hb, 0.5 * hb)], hb * 0.56
    if n == 6:
        return [(-0.6 * hb, -0.5 * hb), (0, -0.5 * hb), (0.6 * hb, -0.5 * hb), (-0.3 * hb, 0.05 * hb), (0.3 * hb, 0.05 * hb), (0, 0.6 * hb)], hb * 0.5
    if n == 7:
        return [(-0.6 * hb, -0.55 * hb), (0, -0.55 * hb), (0.6 * hb, -0.55 * hb), (-0.6 * hb, 0.05 * hb), (0, 0.05 * hb), (0.6 * hb, 0.05 * hb), (0, 0.62 * hb)], hb * 0.46
    if n == 8:
        pts = []
        for k in range(8):
            import math
            a = k * math.pi / 4
            pts.append((0.66 * hb * math.cos(a), 0.66 * hb * math.sin(a)))
        return pts, hb * 0.38
    if n == 9:
        return [((x - 1) * 0.6 * hb, (y - 1) * 0.6 * hb) for y in range(3) for x in range(3)], hb * 0.46
    rows = [4, 3, 2, 1] if n == 10 else ([4, 3, 2] if n == 9 else [n - n // 2, n // 2])
    pts = []
    ys = [-0.6, -0.2, 0.2, 0.6][:len(rows)]
    for r, y in zip(rows, ys):
        for k in range(r):
            pts.append(((k - (r - 1) / 2.0) * 0.42 * hb, y * hb))
    return pts[:n], hb * 0.38


def draw(arms, lex, act_icons, ccs_icons, house, ccs=False):
    """Arms -> (warstwy, notatki, uzyto CCs)."""
    P = Painter(lex, act_icons, ccs_icons, ccs)
    h = crc(house)
    field = arms.field[0] if arms.field else None
    if field is None:
        # brak pola: z godla (zasada tynktur) albo z crc
        tins = [it.tin for it in arms.items if it.tin and it.tin not in ('proper', 'counter')]
        field = contrast(tins[0], h) if tins else ['azure', 'gules', 'vert', 'sable', 'or', 'argent'][h % 6]
        P.notes.append('field=rule')
    f2 = arms.field[1] if len(arms.field) > 1 else contrast(field, h >> 3)
    c1, c2 = TINCT_COLOR.get(field, 33), TINCT_COLOR.get(f2, 116)
    div = arms.div
    # --- tlo (warstwa 0)
    if div == 'pale':
        P.add(1, c1, c2, FULL, FULL, MID, MID, 1, 0, 0)
    elif div == 'quarterly':
        # tlo 3: c2 w 1. i 4. cwiartce (gorna lewa, dolna prawa)
        P.add(3, c2, c1, FULL, FULL, MID, MID, 1, 0, 0)
    elif div == 'bend':
        P.add(12, c1, c2, FULL, FULL, MID, MID, 1, 1, 0)
    elif div == 'bendsin':
        P.add(12, c1, c2, FULL, FULL, MID, MID, 1, 0, 0)
    elif div == 'chevron':
        P.add(13, c2, c1, FULL, FULL, MID, MID + 300, 1, 0, 0)
    elif div == 'saltire':
        P.add(14, c1, c2, FULL, FULL, MID, MID, 1, 0, 0)
    elif div == 'gyronny':
        P.add(16, c1, c2, FULL, FULL, MID, MID, 1, 0, 0)
    elif div == 'chequy':
        P.add(4, c1, c2, FULL, FULL, MID, MID, 1, 0, 0)
    elif div == 'lozengy':
        P.add(8, c1, c2, FULL, FULL, MID, MID, 1, 0, 0)
    elif div == 'vair':
        if len(arms.field) < 2:
            c1, c2 = TINCT_COLOR['argent'], TINCT_COLOR['azure']
        P.add(7, c1, c2, FULL, FULL, MID, MID, 1, 0, 0)
    elif div == 'pily':
        P.add(6, c1, c2, FULL, FULL, MID, MID, 1, 0, 0)
    else:
        P.add(11, c1, c1, FULL, FULL, MID, MID, 1, 0, 0)
        if div == 'fess':
            P.add(505, c2, c2, FULL / SQ, 764 / SQ, MID, MID + 382, 0, 0, 0)
        elif div in ('paly', 'barry', 'tierce'):
            cols = [TINCT_COLOR.get(t, 33) for t in arms.field] if len(arms.field) > 2 else [c1, c2]
            k = 6 if len(cols) == 2 else len(cols) * 2
            if div == 'tierce':
                k, cols = 3, ([TINCT_COLOR.get(t, 33) for t in arms.field] + [c2, c1])[:3]
            w = 510.0 / k
            for s in range(k):
                cc = cols[s % len(cols)]
                if cc == c1 and s == 0:
                    continue
                if cc == c1 and len(cols) == 2:
                    continue
                cx = V0 + w * (s + 0.5)
                if div == 'barry':
                    P.add(505, cc, cc, FULL / SQ, (w + 2) / SQ, MID, cx, 0, 0, 0)
                else:
                    P.add(505, cc, cc, (w + 2) / SQ, FULL / SQ, cx, MID, 0, 0, 0)
        elif div in ('bendy', 'bendysin'):
            rot = 315 if div == 'bendy' else 45
            for s in (-2, 0, 2):
                off = s * 85
                dx, dy = (0.707 * off, -0.707 * off) if div == 'bendy' else (0.707 * off, 0.707 * off)
                P.add(505, c2, c2, FULL / SQ, 85 / SQ, MID + dx, MID + dy, 0, 0, rot)
    if arms.quarters:
        # godla cwiartek: polowa A w 1. i 4. (gora-lewo, dol-prawo), B w 2. i 3.
        offs = [((-0.5, -0.5), (0.5, 0.5)), ((0.5, -0.5), (-0.5, 0.5))]
        for qi, (qt, items) in enumerate(arms.quarters[:2]):
            chg = [it for it in items if it.kind == 'chg'] or [it for it in items if it.kind == 'ord' and it.name in ORD_ICON]
            if not chg:
                continue
            it = chg[0]
            ic, how = (ORD_ICON[it.name], 'ordinary') if it.kind == 'ord' else P.icon(it.name)
            tin = it.tin
            if tin == 'proper':
                tin = proper_of(it.name, lex)
            cc = col(tin if tin and tin != 'counter' else None, qt or field, h + qi)
            for ox, oy in offs[qi]:
                P.add(ic, cc, cc, 190, 190, MID + ox * HALF, MID + oy * HALF, 0, 0, 0)
            P.notes.append('q%d:%s->%d(%s)' % (qi + 1, it.name, ic, how))
        return P.layers, P.notes, P.used_ccs
    # --- zwyczajne (ord) w roli main, potem bordiura i glowica, potem figury
    ords = [it for it in arms.items if it.kind == 'ord' and it.role in ('main', 'over')]
    chief = next((o for o in ords if o.name == 'chief'), None)
    bord = next((o for o in ords if o.name in ('bordure',)), None)
    area = [V0, V0, V1, V1]       # wolne pole na figury glowne
    hosts, placed = {}, {}
    for o in ords:
        if o.name in ('chief', 'bordure'):
            continue
        oc = col(o.tin if o.tin not in ('proper', 'counter') else None, field, h + 7)
        hosts[id(o)] = draw_ord(P, o, oc, field)
        if o.name == 'flaunches':
            area[0], area[2] = V0 + 135, V1 - 135
    if chief is not None:
        cc = col(chief.tin if chief.tin not in ('proper', 'counter') else None, field, h + 11)
        P.add(505, cc, cc, FULL / SQ, 340 / SQ, MID, V0, 0, 0, 0)
        area[1] = V0 + 175
        hosts[id(chief)] = ('chief', MID, V0 + 85, 170)
    if bord is not None:
        bc = col(bord.tin if bord.tin not in ('proper', 'counter') else None, field, h + 13)
        t = 60
        for (sx, sy, px, py) in ((FULL, 2 * t, MID, V0), (FULL, 2 * t, MID, V1), (2 * t, FULL, V0, MID), (2 * t, FULL, V1, MID)):
            P.add(505, bc, bc, sx / SQ, sy / SQ, px, py, 0, 0, 0)
        area = [max(area[0], V0 + t + 6), max(area[1], V0 + t + 6), V1 - t - 6, V1 - t - 6]
    # figury
    chg = [it for it in arms.items if it.kind == 'chg']
    mains = [it for it in chg if it.role == 'main']
    order = [it for it in chg if it.role == 'around' and it.host is not None and it.host in chg] + mains
    order += [it for it in chg if it not in order]
    for it in order:
        tin = it.tin
        if tin is None and it.name in INTRINSIC:
            tin = INTRINSIC[it.name]
        if tin == 'proper':
            tin = proper_of(it.name, lex) or None
        stroke, c2x = 0, 0
        counter = tin == 'counter'
        if tin == 'counter':
            ft = [t for t in arms.field if t]
            light = [t for t in ft if is_light(t)]
            tin = light[0] if light else (ft[-1] if ft else None)
            other = [t for t in ft if t != tin]
            stroke, c2x = 1, TINCT_COLOR.get(other[0], 116) if other else 116
        host_field = field
        if it.role == 'on' and it.host is not None:
            host_field = it.host.tin if it.host.tin not in (None, 'proper', 'counter') else contrast(field, h + 5)
        cc = col(tin, host_field, h + 17 + len(P.layers))
        if cc == TINCT_COLOR.get(host_field) and it.role != 'on' and not counter and it.name not in INTRINSIC:
            cc = col(contrast(host_field, h + 19), host_field, h)
        ic, how = P.icon(it.name)
        P.notes.append('%s %s->%d(%s)' % (it.role, it.name, ic, how))
        if it.role == 'on' and it.host is not None and id(it.host) in placed:
            for (hx, hy, hs) in placed[id(it.host)]:
                P.add(ic, cc, c2x or cc, hs * 0.62, hs * 0.62, hx, hy, stroke, 0, 0)
        elif it.role == 'on' and it.host is not None:
            place_on(P, it, ic, cc, hosts.get(id(it.host)), it.host, stroke, c2x)
        elif it.role == 'around' and it.host is not None and id(it.host) in hosts:
            place_around(P, it, ic, cc, hosts[id(it.host)], stroke, c2x)
        elif it.role == 'around' and it.host is not None:
            crossed = any(m in it.mods for m in ('saltirewise', 'in saltire', 'crossed'))
            if crossed and it.count == 2:
                P.add(ic, cc, c2x or cc, 200, 470, MID - 2, MID, stroke, 0, 45)
                P.add(ic, cc, c2x or cc, 200, 470, MID + 2, MID, stroke, 1, 315)
            else:
                place_around(P, it, ic, cc, ('fieldhost', MID, MID, 300), stroke, c2x)
        elif it.role == 'orle':
            import math
            r = 205 if bord is None else 150
            for k in range(8):
                a = k * math.pi / 4 + math.pi / 8
                P.add(ic, cc, c2x or cc, 70, 70, MID + r * math.cos(a), MID + r * math.sin(a), stroke, 0, 0)
        elif it.role == 'over':
            s = (area[2] - area[0]) * 0.8
            rot = 315 if lex.get(it.name, ([], [], ''))[2] == 'weapon' else 0
            P.add(ic, cc, c2x or cc, s, s, MID, MID, stroke, 0, rot)
        else:
            cx, cy = (area[0] + area[2]) / 2.0, (area[1] + area[3]) / 2.0
            hb = min(area[2] - area[0], area[3] - area[1]) / 2.0
            if it.pos:
                if 'chief' in it.pos:
                    cy -= hb * 0.45
                if 'base' in it.pos:
                    cy += hb * 0.45
                if 'dexter' in it.pos:
                    cx -= hb * 0.45
                if 'sinister' in it.pos:
                    cx += hb * 0.45
                hb *= 0.55
            if len(mains) > 1 and it in mains and it.pos is None and it.count != 'seme':
                # dwie grupy glowne bez polozenia: obok siebie
                k = mains.index(it)
                cx += (-0.42 if k == 0 else 0.42) * hb
                hb *= 0.55
            crossed = it.count == 2 and any(m in it.mods for m in ('saltirewise', 'in saltire', 'crossed'))
            if crossed:
                s = hb * 1.7
                P.add(ic, cc, c2x or cc, s * 0.55, s, cx - 2, cy, stroke, 0, 45)
                P.add(ic, cc, c2x or cc, s * 0.55, s, cx + 2, cy, stroke, 1, 315)
                continue
            pts, s = arrangement(it.count, hb, it.mods)
            mirror_alt = any(m in it.mods for m in ('counter-salient', 'combatant', 'addorsed'))
            placed[id(it)] = []
            for k, (ox, oy) in enumerate(pts):
                mir = 1 if (mirror_alt and k % 2 == 1) else 0
                P.add(ic, cc, c2x or cc, s, s, cx + ox, cy + oy, stroke, mir, 0)
                placed[id(it)].append((cx + ox, cy + oy, s))
    if len(P.layers) == 1:
        # samo pole (np. 'Barry vert and argent', 'Colours: Black') - bez godla nie ma czego pokazac na proporczyku z daleka:
        # zostaje pole, a gdy jednobarwne - neutralne godlo w metalu (zasada tynktur)
        if div is None:
            noun = AREA_FLAVOUR.get(arms.notes[-1] if arms.notes and arms.notes[-1] in AREA_FLAVOUR else '', 'mullet')
            ic, how = P.icon(noun)
            cc = col(contrast(field, h + 23), field, h)
            P.add(ic, cc, cc, 330, 330, MID, MID, 0, 0, 0)
            P.notes.append('sole field -> %s %d' % (noun, ic))
        else:
            # samo pole podzielone (kanon: 'Lozengy argent and gules'): bez godla, jak w kanonie; warstwa 1 = niewidoczny punkt 2x2 poza kadrem
            # w barwie pola (Banner.GetFirstIconColor / GetBannerDataAtIndex(1) w grze zakladaja co najmniej jedna ikone)
            P.add(503, c1, c1, 2, 2, 60, 60, 0, 0, 0)
            P.notes.append('field only (placeholder layer)')
    return P.layers, P.notes, P.used_ccs


def draw_ord(P, o, oc, field):
    """Rysuje zwyczajna figure; zwraca opis nosiciela (rodzaj, cx, cy, rozmiar) dla godel 'on' i 'around'."""
    n = o.name
    if n == 'fess':
        P.add(505, oc, oc, FULL / SQ, 170 / SQ, MID, MID, 0, 0, 0)
        return ('fess', MID, MID, 170)
    if n == 'bars':
        for dy in (-95, 95):
            P.add(505, oc, oc, FULL / SQ, 70 / SQ, MID, MID + dy, 0, 0, 0)
        return ('fess', MID, MID, 70)
    if n == 'pale':
        P.add(505, oc, oc, 170 / SQ, FULL / SQ, MID, MID, 0, 0, 0)
        return ('pale', MID, MID, 170)
    if n == 'pallets':
        k = o.count if isinstance(o.count, int) and o.count > 1 else 2
        xs = [V0 + 510.0 * (j + 1) / (k + 1) for j in range(k)]
        for x in xs:
            P.add(505, oc, oc, 60 / SQ, FULL / SQ, x, MID, 0, 0, 0)
        return ('pale', MID, MID, 60)
    if n in ('bend', 'bend sinister'):
        rot = 315 if n == 'bend' else 45
        enh = 'enhanced' in o.mods
        dx = -60 if enh else 0
        cy = MID + (-60 if enh else 0)
        cx = MID + (60 if enh and n == 'bend' else (-60 if enh else 0))
        P.add(505, oc, oc, FULL / SQ, 150 / SQ, cx, cy, 0, 0, rot)
        if 'cotised' in o.mods:
            for s in (-1, 1):
                ox, oy = (0.707 * 110 * s, -0.707 * 110 * s) if n == 'bend' else (0.707 * 110 * s, 0.707 * 110 * s)
                P.add(505, oc, oc, FULL / SQ, 22 / SQ, cx + ox, cy + oy, 0, 0, rot)
        return (n, cx, cy, 150)
    if n == 'bendlets':
        k = o.count if isinstance(o.count, int) and o.count > 1 else 2
        enh = 'enhanced' in o.mods
        for j in range(k):
            off = (j - (k - 1) / 2.0) * 95 + (-90 if enh else 0)
            P.add(505, oc, oc, FULL / SQ, 50 / SQ, MID + 0.707 * off, MID - 0.707 * off, 0, 0, 315)
        return ('bend', MID, MID, 50)
    if n in ('chevron', 'chevron inverted'):
        mesh = 528 if n == 'chevron inverted' or 'inverted' in o.mods else 511
        if mesh == 511:
            P.add(511, oc, oc, 470 / 0.89, 300 / 0.55, MID, MID + 40, 0, 0, 0)
        else:
            P.add(528, oc, oc, 430 / 0.77, 420 / 0.85, MID, MID, 0, 0, 0)
        return ('chevron' if mesh == 511 else 'chevron inverted', MID, MID + 40, 300)
    if n == 'cross':
        P.add(505, oc, oc, 150 / SQ, FULL / SQ, MID, MID, 0, 0, 0)
        P.add(505, oc, oc, FULL / SQ, 150 / SQ, MID, MID, 0, 0, 0)
        return ('cross', MID, MID, 150)
    if n == 'saltire':
        P.add(505, oc, oc, FULL / SQ, 130 / SQ, MID, MID, 0, 0, 45)
        P.add(505, oc, oc, FULL / SQ, 130 / SQ, MID, MID, 0, 0, 315)
        return ('saltire', MID, MID, 130)
    if n in ('pile', 'pile inverted'):
        inv = n == 'pile inverted' or 'inverted' in o.mods
        if o.count == 2:
            for x in (MID - 150, MID + 150):
                P.add(504, oc, oc, 200 / 0.88, 510 / 0.59, x, MID, 0, 0, 0 if inv else 180)
            return ('pile', MID, MID, 200)
        P.add(504, oc, oc, 360 / 0.88, 520 / 0.59, MID, MID, 0, 0, 0 if inv else 180)
        return ('pile', MID, MID - (0 if inv else 60), 300)
    if n in ('pall', 'pall reversed'):
        rev = n == 'pall reversed'
        s = 1 if not rev else -1
        P.add(505, oc, oc, 420 / SQ, 95 / SQ, MID - 128, MID - 128 * s, 0, 0, 315 if not rev else 45)
        P.add(505, oc, oc, 420 / SQ, 95 / SQ, MID + 128, MID - 128 * s, 0, 0, 45 if not rev else 315)
        P.add(505, oc, oc, 95 / SQ, 300 / SQ, MID, MID + 150 * s, 0, 0, 0)
        return ('pall reversed' if rev else 'pall', MID, MID, 95)
    if n == 'canton':
        P.add(505, oc, oc, 220 / SQ, 220 / SQ, V0 + 100, V0 + 100, 0, 0, 0)
        return ('canton', V0 + 100, V0 + 100, 200)
    if n == 'escutcheon':
        P.add(316, oc, oc, 330 / 0.67, 330 / 0.89, MID, MID, 0, 0, 0)
        return ('escutcheon', MID, MID + 10, 230)
    if n == 'lozenge':
        P.add(512, oc, oc, 300 / 0.58, 440 / 0.89, MID, MID, 0, 0, 0)
        return ('escutcheon', MID, MID, 200)
    if n == 'cartouche':
        P.add(506, oc, oc, 300, 400, MID, MID, 0, 0, 0)
        return ('escutcheon', MID, MID, 200)
    if n == 'tierce':
        P.add(505, oc, oc, 170 / SQ, FULL / SQ, V0 + 85, MID, 0, 0, 0)
        return ('pale', V0 + 85, MID, 150)
    if n in ('tressure', 'double tressure', 'orle'):
        insets = (70, 100) if n == 'double tressure' else (80,)
        tins = o.tins if len(o.tins) > 1 else [None]
        for k, ins in enumerate(insets):
            c = oc
            if len(o.tins) > 1 and k < len(o.tins):
                c = TINCT_COLOR.get(o.tins[k], oc)
            a, b = V0 + ins, V1 - ins
            ln = (b - a) + 16
            for (sx, sy, px, py) in ((ln, 16, MID, a), (ln, 16, MID, b), (16, ln, a, MID), (16, ln, b, MID)):
                P.add(505, c, c, sx / SQ, sy / SQ, px, py, 0, 0, 0)
        return ('escutcheon', MID, MID, 260)
    if n == 'flaunches':
        for x in (V0 - 120, V1 + 120):
            P.add(503, oc, oc, 520 / 0.87, 760 / 0.87, x, MID, 0, 0, 0)
        return ('fieldhost', MID, MID, 300)
    if n == 'fret':
        P.add(505, oc, oc, FULL / SQ, 50 / SQ, MID, MID, 0, 0, 45)
        P.add(505, oc, oc, FULL / SQ, 50 / SQ, MID, MID, 0, 0, 315)
        P.add(502, oc, oc, 230 / 0.88, 230 / 0.88, MID, MID, 0, 0, 45)
        return ('escutcheon', MID, MID, 150)
    return ('fieldhost', MID, MID, 300)


def place_on(P, it, ic, cc, host, ho, stroke, c2x):
    if host is None:
        # nosiciel = figura (np. 'upon a plate a falcon', 'on an escutcheon or a hand') - w srodku, mniejsza
        host = ('escutcheon', MID, MID, 230)
    kind, cx, cy, size = host
    n = it.count if isinstance(it.count, int) else 3
    if kind == 'chief':
        n = max(1, min(n, 7))
        s = min(150, 480.0 / max(n, 1) * 0.85)
        for k in range(n):
            x = MID + (k - (n - 1) / 2.0) * (480.0 / max(n, 1))
            P.add(ic, cc, c2x or cc, s, s, x, cy, stroke, 0, 0)
        return
    if kind in ('fess',):
        n = max(1, min(n, 7))
        s = min(size * 0.9, 460.0 / n * 0.85)
        for k in range(n):
            P.add(ic, cc, c2x or cc, s, s, MID + (k - (n - 1) / 2.0) * (460.0 / n), cy, stroke, 0, 0)
        return
    if kind == 'pale':
        n = max(1, min(n, 7))
        s = min(size * 0.9, 460.0 / n * 0.85)
        for k in range(n):
            P.add(ic, cc, c2x or cc, s, s, cx, MID + (k - (n - 1) / 2.0) * (460.0 / n), stroke, 0, 0)
        return
    if kind in ('bend', 'bend sinister'):
        n = max(1, min(n, 7))
        step = 560.0 / (n + 1)
        s = min(size * 0.9, step * 0.85)
        for k in range(n):
            d = (k - (n - 1) / 2.0) * step
            dx = 0.707 * d
            dy = 0.707 * d if kind == 'bend' else -0.707 * d
            P.add(ic, cc, c2x or cc, s, s, cx + dx, cy + dy, stroke, 0, 0)
        return
    if kind in ('chevron', 'chevron inverted'):
        pts = [(0, -95), (-150, 40), (150, 40)] if kind == 'chevron' else [(0, 80), (-150, -60), (150, -60)]
        pts = pts[:max(1, min(n, 3))]
        for (ox, oy) in pts:
            P.add(ic, cc, c2x or cc, 95, 95, MID + ox, cy + oy, stroke, 0, 0)
        return
    if kind in ('cross', 'saltire'):
        if kind == 'cross':
            pts = [(0, 0), (0, -170), (0, 170), (-170, 0), (170, 0), (0, -85), (0, 85), (-85, 0), (85, 0)]
        else:
            pts = [(0, 0), (-130, -130), (130, -130), (-130, 130), (130, 130), (-65, -65), (65, -65), (-65, 65), (65, 65)]
        for (ox, oy) in pts[:max(1, min(n, 9))]:
            P.add(ic, cc, c2x or cc, 90, 90, MID + ox, MID + oy, stroke, 0, 0)
        return
    if kind == 'canton':
        P.add(ic, cc, c2x or cc, 150, 150, cx, cy, stroke, 0, 0)
        return
    if kind == 'pile':
        P.add(ic, cc, c2x or cc, 190, 190, MID, cy - 40, stroke, 0, 0)
        return
    # tarcza / krazek / figura: w srodku nosiciela
    pts, s = arrangement(n, size / 2.0 * 0.85, it.mods)
    for ox, oy in pts:
        P.add(ic, cc, c2x or cc, s, s, cx + ox, cy + oy, stroke, 0, 0)


def place_around(P, it, ic, cc, host, stroke, c2x):
    kind = host[0]
    n = it.count if isinstance(it.count, int) else 2
    if kind == 'bend':
        pts = [(140, -140), (-140, 140), (60, -200), (-200, 60)]
    elif kind == 'bend sinister':
        pts = [(-140, -140), (140, 140), (-200, 60), (60, 200)]
    elif kind == 'fess':
        pts = [(-130, -170), (130, -170), (0, 170)] if n == 3 else [(0, -170), (0, 170), (-140, -170), (140, 170), (140, -170), (-140, 170)]
    elif kind == 'pale':
        pts = [(-170, 0), (170, 0), (-170, -150), (170, -150), (-170, 150), (170, 150)]
    elif kind in ('chevron',):
        pts = [(-160, -150), (160, -150), (0, 180)]
    elif kind in ('chevron inverted',):
        pts = [(0, -170), (-160, 150), (160, 150)]
    elif kind == 'cross':
        pts = [(-145, -145), (145, -145), (-145, 145), (145, 145)]
    elif kind == 'saltire':
        pts = [(0, -175), (175, 0), (0, 175), (-175, 0)]
    elif kind == 'pall':
        pts = [(0, -190), (-170, 105), (170, 105)]
    elif kind == 'pall reversed':
        pts = [(-170, -105), (170, -105), (0, 190)]
    else:
        pts = [(-175, 0), (175, 0), (0, -175), (0, 175)]
    s = 130 if kind != 'fieldhost' else 150
    if kind in ('pall', 'pall reversed'):
        s = 115
    for k, (ox, oy) in enumerate(pts[:max(1, min(n, len(pts)))]):
        P.add(ic, cc, c2x or cc, s, s, MID + ox, MID + oy, stroke, 0, 0)


# ------------------------------------------------------------------------------------------------ poprawki reczne (zly rozbior)
# rod -> blazon techniczny zastepczy (ta sama gramatyka) albo pelny opis; tylko gdy rozbior automatyczny daje inny herb niz kanon.
OVERRIDES = {
    'House Arryn of Gulltown': 'Bleu celeste, upon a plate a falcon of the field',
    'House Baelish of Harrenhal': 'Vert, seme of mockingbirds argent',
    'House Bolton of Winterfell': 'Rose, a flayed man gules',
    'House Breakstone': 'Argent, a sledgehammer gules',
    'House Byrch': 'Vert, an axe argent',
    'House Casterly of Casterly Rock': 'Gules, a lion or',
    'House Codd': 'Azure, a codfish argent',
    'House Costayne of the Three Towers': 'Sable, a chalice argent',
    'House Crowl of Deepdown': 'Sable, a pile tenne',
    'House Edgerton': 'Indigo, a wheel tenne',
    'House Keath': 'Cendree, a fish argent',
    'House Lake': 'Brunatre, seven pommes vert',
    'House Lannister of Darry': 'Gules, a lion or',
    'House Lonmouth': 'Or, three lips gules',
    'House Spicer of Castamere': 'Vert, on a bend or three pepperpots brunatre',
    'House Suggs': 'Azure, a pig rose',
    'House Willum': 'Sable, three swords argent, on a chief argent a skull cendree',
    'House Wydman': 'Or, five lances azure, on a chief argent a castle gules',
    'House Yarwyck': 'Argent, two halberds saltirewise bronze',
    'House Harlaw of Grey Garden': 'Sable, a scythe argent',
    'House Fenn': 'Lilac, three water lilies sable',
    'House Harclay': 'Azure, on a bend argent three crescents azure',
    'House Myre of Harlaw': 'Argent, ten nooses sable within a bordure sanguine',
    'House Drox': 'Or, three crossbows sable within a bordure sable',
    'House Hoare of Orkmont': 'Per saltire sable and or, a longship or',
    'House Polander': 'Per bend sinister argent and purpure, a broken sword argent',
    'House Shawney': 'Per fess azure and sable, a catfish argent',
    'House Wendwater': 'Argent, a pall reversed vert between three trees vert',
    'House Langward': 'Per fess murrey and sable, five mullets argent',
    'House Sloane': 'Per pale purpure and or, a sun tenne',
    'House Orme': 'Per fess sable and or, three harps or',
    'House Lyberr': "Per bend sinister sable and argent, a cat's head cendree",
    'House Doggett': 'Per bend vert and argent, a unicorn argent',
    'House Vikary': 'Quarterly clockwise, 1st and 3rd white, a boar gules, 2nd and 4th gules, a lion argent',
    'House Parren': "Per saltire gules and or, a lion's head sable",
    'House Hutcheson': 'Or, two bendlets gules, a sun gules',
    'House Kenning of Harlaw': 'Sable, a hand cendree between two lightning bolts or',
    'House Falwell': 'Sable, a fool gules between two suns or',
    'House Merryweather of Longtable': 'Argent, a cornucopia or within a bordure or',
    'House Royce of the Gates of the Moon': 'Purpure, a portcullis sable within a bordure bronze',
    'House Risley of Risley Glade': 'Argent, a knight sable',
    'House Rhysling': 'Sable, a door brunatre',
    'House Graceford of Holyhall': "Brunatre, on a pale argent a mother's head carnation",
    'House Webber of Coldmoat': 'Sable, a spider argent',
    'House Kidwell of Ivy Hall': 'Sable, three ivy leaves vert',
    'House Turnberry': 'Vert, on a saltire argent five strawberries gules',
    'House Hawthorne': 'Vert, an annulet sable',
    'House Templeton of Ninestars': 'Sable, on a saltire or nine mullets sable',
    'House Rogers of Amberly': 'Sable, a maze argent',
    'House Darklyn of Duskendale': 'Lozengy sable and or, on a pale gules seven escutcheons argent',
    'House Cressey': 'Azure, on a bend sinister gules seven plates, between two helmets argent',
    'House Melcolm of Old Anchor': 'Aquamarine, an anchor russet, on a chief or three hurts',
    'House Uffering': 'Paly brunatre and cendree, on a chief argent seven hands vert',
    'House Yelshire': 'Barry vert and or, on a chief argent a battering ram sable',
    'House Bridges': 'Argent, three pallets azure, on a chief or a bridge sable',
    'House Follard': 'Gyronny gules and argent, on a canton or a bell argent',
    'House Bolling': 'Vairy tenne and azure, on a canton sable a stag or',
    'House Greyiron of Orkmont': "Cendree, a sea king's head argent",
    'House Ferren': 'Per saltire gules and vert, four ferrets argent',
    'House Middlebury': 'Quarterly clockwise, 1st and 3rd or, a saltire sable, 2nd and 4th vert, a lozenge argent',
    'House Musgood': 'Quarterly clockwise, first and fourth azure, a pavilion or, second and third argent, a garland vert',
    'House Appleton of Appleton': 'Quarterly clockwise, 1st and 3rd or, an apple tree vert, 2nd and 4th argent, a gatehouse cendree',
    'House Harlaw of Harlaw Hall': 'Per bend sable and argent, two scythes counterchanged',
    'House Dunn': 'Per pale sable and rose, a pelican counterchanged',
    'House Farring': 'Per pale purpure and argent, two knights combatant counterchanged',
    'House Terrick': "Per saltire purpure and or, four hawk's heads counterchanged",
    'House Tarbeck of Tarbeck Hall': 'Gyronny argent and azure, an estoile counterchanged',
    'House Hersy of Newkeep': 'Rose, a winged chalice argent',
    'House Erenford': 'Rose, a heron or',
    'House Pryor of Pebble': 'Rose, a sun or',
    'House Donniger': 'Per fess wavy or and vert-cendree, a sun gules',
    'House Upcliff of the Witch Isle': 'Sable, a wave aquamarine',
    'House Wynch of Iron Holt': 'Purpure, a crescent sanguine',
    'House Lightfoot': 'Brunatre, five footprints argent',
    'House Lipps': 'Azure, lips carnation',
    'House Thenn': 'Argent, a disk bronze',
    'House Meadows of Grassy Vale': 'Vert, eight flowers or',
    'House Roxton of the Ring': 'Bleu celeste, a saltire or',
    'House Hetherspoon': 'Lozengy tenne and sable, on a cartouche argent a spoon brunatre',
    'House Mudd of Oldstones': 'Brunatre, a crown or',
    'House Fisher of the Misty Isle': 'Azure, a catfish cendree',
    'House Myatt': 'Brunatre, a treecat or',
    'House Hunt': 'Argent, a deer brunatre',
    'House Chester of Greenshield': 'Vert, on an escutcheon or a hand vert, all within a bordure gules',
    'House Grimm of Greyshield': 'Vert-cendree, an escutcheon cendree',
    'House Staedmon of Broad Arch': 'Argent, a heart gules within a bordure gules',
    'House Toyne': 'Or, a winged heart sable within a bordure sable',
    'House Peasebury of Poddingfield': 'Argent, a pea pod within a bordure vert',
    'House Gaunt': 'Rose, three lances sable between two flaunches sable',
    'House Teague': 'Sable, between two flaunches a trident or',
    'House Yew': 'Argent, between two flaunches gules a longbow or',
    'House Waterman': 'Argent, between two flaunches azure two oars saltirewise brunatre',
    'House Manning': 'Argent, between two pallets sable a sea lion gules',
    'House Elesham of the Paps': 'Rose, two piles inverted cendree, a mullet sable',
    'House Jast': "Sable, a pall reversed or between three lion's heads or",
    'House Woodwright': 'Brunatre, a pall gules',
    'House Butterwell of Whitewalls': 'Barry wavy vert, argent, and or',
    'House Strong of Harrenhal': 'Argent, a pale azure',
    'House Sunderland of the Three Sisters': "Barry wavy azure and vert, three woman's heads argent",
    'House Moreland': 'Paly brunatre and vert',
    'House Kettleblack': 'Gules, a kettle sable within an orle of pellets',
    'House Harroway of Harrenhal': 'Per bend sinister tenne and sable, a castle counterchanged',
    'House Inchfield': 'Cendree, a bend sinister sable',
    'House Overton': 'Sable, a fess argent',
    'House Osgrey of Standfast': 'Argent, a lion vert',
    'House Hamell': 'Rose, a maunch erminois within a bordure erminois',
    'House Rambton': "Gules, a ram's head argent",
    'House Oldflowers': 'Vert, ten hands argent, a bend sinister gules',
    'House Norridge': 'Azure, three arrows bendwise tenne',
    'House Kellington': 'Bleu celeste, a book sable',
    'House Swygert': 'Purpure, a scroll argent within a tressure argent',
    'House Shermer of Smithyton': 'Azure, seme of nails copper within a bordure copper',
    'House Redding': 'Murrey, a flagon or within a bordure or',
    'House Slynt of Harrenhal': 'Sable, a spear or within a bordure or',
    'House Wull': 'Azure, three buckets brunatre within a bordure cendree',
    'House Towers of Harrenhal': 'Argent, five towers sable within a double tressure gules and sable',
    'House Qoherys of Harrenhal': 'Sable, a saltire gules between four skulls argent',
    'House Condon': "Argent, between two tridents saltirewise gules an eagle's head gules",
    'House Liddle': 'Per pale argent and vert, three pinecones brunatre',
    'House Mollen': 'Argent, on a pile vert a pine tree argent',
    'House Burley': 'Argent, on a pile azure a knife argent',
    'House Saltcliffe of Saltcliffe': 'Argent, a serpent sable',
    'House Santagar of Spottswood': 'Per bend sinister azure and argent, a spotted-leopard or',
    'House Stonetree of Harlaw': 'Sable, a stone tree cendree',
    'House Volmark of Volmark': 'Cendree, a leviathan sable',
    'House Tawney of Orkmont': 'Argent, a scourge sable',
    'House Tudbury': 'Vert, on a lozenge or a tortoise brunatre',
    'House Vypren': 'Vert, a toad sable',
    'House Wells': 'Per fess cendree and vert, five pellets in fess',
    'House Horpe': "Per chevron cendree and argent, three death's head moths counterchanged",
    'House Blackfyre': 'Gules, a three-headed dragon sable',
    'House Hollard': 'Barry gules and rose, on a chief azure three crowns or',
    'House Bywater': 'Argent fretty azure, on a chief azure three fish argent',
    'House Hayford of Hayford': 'Or fretty vert, a pale vert',
    'House Cockshaw': 'Sable, three feathers in fess gules',
    'House Charlton': 'Or, three sprigs of mistletoe vert within a bordure vert',
    'House Kidwell': 'Sable, three ivy leaves vert',
    'House Brune of Brownhollow': "Argent, a bear's paw brunatre within a double tressure brunatre",
    'House Haigh': 'Brunatre, on a bend sinister or a pitchfork sable',
    'House Grell': 'Azure, on a bend argent three martlets gules',
    'House Harte': "Vair, on a bend argent three hart's heads gules",
    'House Chelsted': 'Per bend vert and argent, a mace proper surmounted by a dagger silver',
    'House Pyle': 'Argent, a greathelm cendree',
    'House Lothston of Harrenhal': 'Per bend argent and or, a bat sable',
    'House Gower': 'Ermine, on a cross sable nine trefoils or',
    'House Estren of Wyndhall': 'Argent, a saltire vert between four double-headed eagles gules',
    'House Ironsmith': 'Or, between four horseshoes a sword sable, all within a bordure vert-cendree',
    'House Ruthermont': 'Pean, on a pale or five starfish sable',
    'House Shett of Gull Tower': 'Chequy argent and sable, three wings or',
    'House Shett of Gulltown': 'Brunatre, nine seagulls argent',
    'House Cassel': "Cendree, ten wolf's heads argent within a bordure sable",
    'House Marsh': 'Or, ten frogs vert',
}


# ------------------------------------------------------------------------------------------------ glowna
def load_tsv(path):
    with open(path, encoding='utf-8') as f:
        lines = [l for l in f if not l.startswith('#')]
    return list(csv.DictReader(lines, delimiter='\t'))


def lore_names(repo, villages):
    """Kraina -> poczatki imion rycerzy z lore ('Ser Howard', 'Lady Barbrey', 'Harwood', 'Gorzhak zo')."""
    out = collections.defaultdict(set)
    for fn in ('arm_map_village_lore.tsv', 'arm_map_village_lore_v4.tsv'):
        p = os.path.join(repo, 'Armoury', 'ModuleData', fn)
        if not os.path.exists(p):
            continue
        for line in open(p, encoding='utf-8'):
            if line.startswith('#'):
                continue
            f = line.rstrip('\n').split('\t')
            if len(f) < 2 or not f[1]:
                continue
            dist = f[0].split('#')[0]
            cul = villages.get(dist, ('', ''))[0]
            area = CULTURE_AREA.get(cul)
            if not area:
                continue
            ns = name_start(f[1])
            if ns:
                out[area].add(ns)
    return {a: sorted(v) for a, v in out.items()}


def name_start(knight):
    """'Ser Elwood Largent of Crossfield' -> 'Ser Elwood'; 'Gorzhak zo Gheghaz of Galamash' -> 'Gorzhak zo'; 'Harwood Icewood of X' -> 'Harwood'.
    Ta sama zasada co VillageHolders.NameStart (C#)."""
    w = knight.split()
    if not w:
        return ''
    pre = ''
    if w[0] in ('Ser', 'Lady', 'Lord', 'Maester', 'Magister'):
        pre, w = w[0], w[1:]
    if not w:
        return ''
    first = w[0]
    if len(w) > 1 and w[1] == 'zo':
        first += ' zo'
    return (pre + ' ' + first).strip()


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    modules = argv[1]
    out = os.path.join(REPO, 'Armoury', 'ModuleData', 'arm_houses_canon.tsv')
    if '--out' in argv:
        out = argv[argv.index('--out') + 1]
    report = '--report' in argv
    icons_xml = load_banner_icons(modules)
    bg_act = icons_xml['Native'][0] | icons_xml['NavalDLC'][0] | icons_xml['ROT-Content'][0]
    ic_act = icons_xml['Native'][1] | icons_xml['NavalDLC'][1] | icons_xml['ROT-Content'][1]
    col_act = icons_xml['Native'][2] | icons_xml['NavalDLC'][2] | icons_xml['ROT-Content'][2]
    ic_ccs = icons_xml['Bannerlord.CCsBanners'][1]
    col_all = col_act | icons_xml['Bannerlord.CCsBanners'][2]
    for t, c in TINCT_COLOR.items():
        assert c in col_act and c not in FORBID_COLORS, (t, c)
    spclans = load_spclans(modules)
    villages, alls = load_villages(modules)
    icon_rows = load_tsv(os.path.join(DANE, 'ikony_godla.tsv'))
    for ir in icon_rows:
        ICON_KW[int(ir['icon_id'])] = set(k.strip().lower() for k in ir['keywords'].split('|') if k.strip()) | set(re.findall(r"[a-z'-]+", ir['charge'].lower()))
    phrase, lex = build_lexicon(icon_rows)
    houses = load_tsv(os.path.join(DANE, 'kanon_rody.tsv'))
    names = lore_names(REPO, villages)

    rows, stats = [], collections.Counter()
    bad = []
    seen_keys = set()
    for r in houses:
        name = r['house'].strip()
        area = area_of_region(r['region'])
        key = name if name not in seen_keys else name + '|' + area
        seen_keys.add(key)
        culture = ''
        m = re.search(r'Culture\.([A-Za-z_]+)', r['rot_kingdom_culture'] or '')
        if m:
            culture = m.group(1)
        seats = [s for s in (r['seat_settlement_id'] or '').split(';') if s.strip()]
        seats = [s for s in seats if s in alls]
        status = (r['status'] or '').strip()
        liege = (r['liege'] or '').strip()
        clans = [c for c in (r['rot_clan_id'] or '').split(';') if c.strip()]
        rot_code = ''
        for c in clans:
            if c in spclans:
                rot_code = spclans[c]
                break
        blazon = (r['blazon'] or '').strip()
        holder = 'yes'
        why = ''
        if area not in HOLDER_AREAS:
            holder, why = 'no', 'area'
        elif not name.startswith('House '):
            holder, why = 'no', 'person'     # 'Andrik of Southshield', 'Nute of Oakenshield' - lordowie z AFFC, nie rody
        elif status.startswith('extinct'):
            holder, why = 'no', 'extinct'
        elif status.startswith('adwd'):
            holder, why = 'no', 'adwd'
        elif status.startswith('great') or liege.startswith('none'):
            holder, why = 'no', 'great'
        code = ccode = parse = ''
        if rot_code:
            source = 'rot'
            code = rot_code
            pr = problems(code, bg_act, ic_act, col_act, False)
            if pr:
                bad.append((name, 'rot', pr))
                holder, why = 'no', 'rot-code'
            parse = 'ROT clan %s banner_key (%d layers)' % (clans[0], len(code.split('.')) // 10)
        else:
            source = 'generated'
            spec = OVERRIDES.get(name)
            if spec:
                arms = parse_arms(spec if '(' in spec else 'x (' + spec + ')', phrase, name)
                arms.notes.append('override')
            elif blazon.lower().startswith(('colours:', 'colors:')):
                # Wielcy Panowie Zatoki Niewolniczej: same barwy - pole w barwach rodu i godlo krainy (piramida) w metalu
                arms = parse_arms(blazon, phrase, name)
                arms.items.append(Item('chg', AREA_FLAVOUR.get(area, 'mullet'), 1, None))
                arms.notes.append('colours+area charge')
            elif blazon and not blazon.lower().startswith('unknown') and len(blazon) > 12:
                arms = parse_arms(blazon, phrase, name)
            else:
                arms = canting_arms(key, area)
            layers, notes, _ = draw(arms, lex, ic_act, ic_ccs, key, ccs=False)
            code = code_of(layers)
            pr = problems(code, bg_act, ic_act, col_act, True)
            if pr:
                bad.append((name, 'generated', pr))
                holder, why = 'no', 'code'
            layers2, notes2, used = draw(arms, lex, ic_act, ic_ccs, key, ccs=True)
            if used:
                ccode = code_of(layers2)
                pr2 = problems(ccode, bg_act, ic_act | ic_ccs, col_all, True)
                if pr2:
                    bad.append((name, 'ccs', pr2))
                    ccode = ''
            fld = (arms.div + ' ' if arms.div else '') + '/'.join(t for t in arms.field if t) if arms.field else 'rule'
            parse = '%s | field %s | %s' % ('+'.join(arms.notes), fld, '; '.join(notes))
            for nn in notes:
                if '(exact)' in nn:
                    stats['charge exact'] += 1
                elif '(family' in nn:
                    stats['charge family'] += 1
                elif '(neutral)' in nn:
                    stats['charge neutral'] += 1
            if used:
                stats['ccs variant'] += 1
        stats['source ' + source] += 1
        stats['holder ' + holder + (' (' + why + ')' if why else '')] += 1
        if holder == 'yes':
            stats['holder area ' + area] += 1
        rows.append([name, key, (r['region'] or '').strip(), area, culture, ';'.join(seats), liege, house_base(liege) if liege and not liege.startswith('none') else '',
                     house_base(name), status, blazon, (r['words'] or '').strip(), ';'.join(clans), code, ccode, source, holder, parse])

    dist_rows = []
    for vid in sorted(villages):
        cul, nm = villages[vid]
        dist_rows.append([vid, CULTURE_AREA.get(cul, ''), cul, nm])
    stats['districts'] = len(dist_rows)
    stats['districts with area'] = sum(1 for d in dist_rows if d[1])

    def clean(s):
        return s.replace('\t', ' ').replace('\r', ' ').replace('\n', ' ')

    buf = io.StringIO()
    buf.write('# Armoury canon houses (A Song of Ice and Fire, awoiaf.westeros.org) for the keepers of map villages (VillageHolders.cs, MCM VillageCanonHouses).\n')
    buf.write('# Generated by tools/herby/herby_kanon.py from tools/herby/dane/*.tsv and the game data - do not edit.\n')
    buf.write('# banner_code: source rot = the ROT clan banner_key (spclans.xml, first rot_clan_id); generated = drawn from the blazon (field and division,\n')
    buf.write('#   ordinaries, charges -> Native/NavalDLC/ROT icons; exact charge, else the same family, else a mullet), or canting arms from the name\n')
    buf.write('#   when the canon gives none. banner_code_ccs: the same arms with Bannerlord.CCsBanners icons, used only when that module is loaded.\n')
    buf.write('# holder = yes: may keep villages of its area (not extinct, not only in ADWD, not a great or sovereign house, area with villages).\n')
    buf.write('# Every code passes Banner.TryGetBannerDataFromCode (1.4.8) and has its background, icons and colours in banner_icons.xml.\n')
    buf.write('#house\tkey\tregion\tarea\tculture\tseat_settlement_id\tliege\tliege_base\tbase\tstatus\tblazon\twords\trot_clan_id\tbanner_code\tbanner_code_ccs\tsource\tholder\tparse\n')
    for row in rows:
        buf.write('\t'.join(clean(str(x)) for x in row) + '\n')
    buf.write('# districts: game village -> area of its culture in ROT-Map settlements.xml (fixed - Banner Kings changes Settlement.Culture in play)\n')
    buf.write('#district\tarea\tculture\tname\n')
    for d in dist_rows:
        buf.write('\t'.join(clean(x) for x in d) + '\n')
    buf.write("# names: first names of the lore knights of each area (spare name for a village without lore); 'X zo' = Ghiscari form 'X zo <House>'\n")
    buf.write('#names\tarea\tnames\n')
    for a in sorted(names):
        buf.write('names\t%s\t%s\n' % (a, '|'.join(names[a])))
    data = buf.getvalue()
    with io.open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write(data)
    # kontrola: plik czytany od nowa, kazdy kod jeszcze raz
    back = [l.rstrip('\n').split('\t') for l in io.open(out, encoding='utf-8') if l and not l.startswith('#')]
    nh = sum(1 for b in back if len(b) == 18)
    assert nh == len(rows), (nh, len(rows))
    recheck = 0
    for b in back:
        if len(b) == 18:
            assert not problems(b[13], bg_act, ic_act, col_act, b[15] == 'generated') or b[16] == 'no', b[0]
            recheck += 1
    print('arm_houses_canon.tsv: %d rodow (%d sprawdzonych ponownie), %d okregow, imiona %d krain, %d bajtow -> %s' % (len(rows), recheck, len(dist_rows), len(names), len(data.encode('utf-8')), out))
    for k in sorted(stats):
        print('  %-40s %d' % (k, stats[k]))
    if bad:
        print('NIEWAZNE KODY:')
        for b in bad:
            print('  ', b)
    if report:
        for row in rows:
            if row[15] == 'generated':
                print('%s | %s | %s' % (row[0], row[10][:90], row[17]))
    return 0 if not [b for b in bad if b[1] != 'ccs'] else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv))
