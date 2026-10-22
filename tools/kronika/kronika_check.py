# -*- coding: utf-8 -*-
# Kronika Westeros - walidator warstwy danych (bez C#).
# Sprawdza Armoury/ModuleData/arm_kronika_k*.tsv i arm_kronika_tematy.tsv wedlug docs/KRONIKA-FORMAT.md.
# Uruchomienie (z katalogu repo albo skadkolwiek):
#     python -I tools/kronika/kronika_check.py                 # wszystkie pliki paczek
#     python -I tools/kronika/kronika_check.py <plik.tsv> ...  # tylko te pliki (reszta paczek nadal czytana do odwolan)
#     opcje: --gra <katalog gry>  --repo <katalog repo>  --ciche (bez UWAG)  --stat (liczby na paczke)
# Wynik: linie "plik:wiersz: BLAD|UWAGA: ..." i podsumowanie; kod wyjscia 0 = bez bledow, 1 = bledy (UWAGI nie psuja wyniku).
# Tylko biblioteka standardowa. Miara linii okna: tools/kronika/linie.py (kopia e4c/linie.py, ladowana ze sciezki pliku).
import sys
import os
import re
import glob
import hashlib
import importlib.util
import argparse

HERE = os.path.dirname(os.path.abspath(__file__))
DEF_REPO = os.path.normpath(os.path.join(HERE, '..', '..'))
DEF_GRA = 'C:/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord'

# ----------------------------------------------------------------------------------------------- stale formatu
# Kolumny sekcji (dokladnie w tej kolejnosci; wiersz naglowka zaczyna sie od nazwy sekcji).
COLS = {
    '#okno': ['id', 'war', 'od', 'do', 'po', 'przed', 'warunki', 'gracz', 'forma', 'inni', 'wersja', 'temat', 'lektor',
              'tytul', 'gora', 'dol', 'zmienne', 'wybor', 'skutki', 'powtorz'],
    '#wybor': ['okno', 'opcja', 'etykieta', 'podpowiedz', 'dostepna', 'skutki', 'bierna'],
    '#tekst': ['id', 'tekst'],
    '#banda': ['id', 'klan', 'osada', 'min', 'max', 'herszt', 'zachowanie', 'dni', 'list', 'po_rozbiciu'],
    '#zadanie': ['id', 'rodzaj', 'cel', 'co', 'ilosc', 'dni', 'nagroda', 'tytul', 'opis'],
    '#para': ['okno', 'wiersz', 'kierunek'],
    '#usuniete': ['id', 'powod'],
}
TEMAT_COLS = ['temat', 'sprite', 'prolog', 'tylko', 'opis']

# Paczki: numery wydarzen (1-117 z KRONIKA-WESTEROS-V2) + sceny "juz w grze" 901-906; 000 = okna testowe silnika (k00).
PACZKI = {
    0: [0],
    1: list(range(1, 13)) + [901],
    2: list(range(13, 25)) + [902],
    3: list(range(25, 37)) + [903],
    4: list(range(37, 49)),
    5: list(range(49, 61)) + [904, 905],
    6: list(range(61, 73)),
    7: [73, 74, 75, 76, 81, 82, 83, 84, 85, 86, 87, 906],
    8: [77, 78, 79, 88, 89, 90, 91, 92, 93, 94, 95],
    9: [80] + list(range(96, 106)),
    10: list(range(106, 118)),
}
NR_PACZKA = {}
for _k, _v in PACZKI.items():
    for _n in _v:
        NR_PACZKA[_n] = _k

# Wydarzenia ROT (ROT.Events.*, dekompilacja ROT 8.1.8 - 29 klas bez EventBase).
ROT_EVENTS = {
    'AegonInvasionEvent', 'BastardsEvent', 'BastardsSiegeEvent', 'BastardsSiegeNotificationEvent', 'BlackwaterEvent',
    'BlackwaterSiegeEvent', 'BlackwaterSiegeNotificationEvent', 'DanyInvasionEvent', 'EastwatchEvent', 'FistEvent',
    'HarrenhalEvent', 'HarrenhalSiegeEvent', 'HarrenhalSiegeNotificationEvent', 'JeorMutinyEvent', 'JoffreyEvent', 'JonEvent',
    'KingsLandingEvent', 'NedEvent', 'NightKingDeathEvent', 'RamsayEvent', 'RedWeddingEvent', 'RenlyDeathEvent',
    'RickardKarstarkExecutionEvent', 'RiverlandsDeclareWarEvent', 'RooseDeathEvent', 'StannisDeathEvent', 'WallEvent',
    'WallSiegeEvent', 'WallSiegeNotificationEvent',
}

# Os planu Fabuli (CrashScribe/src/Fabula.cs, slownik Timeline) - term plan:X-N dziala tylko dla X z osi; X spoza osi = term ZAWSZE prawdziwy
# (Terms.PlanDay false -> decyduje od). Recenzja K0 R-11: plan:BlackwaterEvent w 032.1. main() czyta os z pliku repo; ta lista to zapas.
FABULA = {
    'NedEvent', 'HarrenhalSiegeNotificationEvent', 'RiverlandsDeclareWarEvent', 'HarrenhalSiegeEvent', 'RenlyDeathEvent',
    'BlackwaterSiegeNotificationEvent', 'BlackwaterSiegeEvent', 'RickardKarstarkExecutionEvent', 'JeorMutinyEvent', 'RedWeddingEvent',
    'JoffreyEvent', 'WallSiegeNotificationEvent', 'WallSiegeEvent', 'EastwatchEvent', 'RamsayEvent', 'JonEvent', 'RooseDeathEvent',
    'BastardsSiegeNotificationEvent', 'BastardsSiegeEvent', 'AegonInvasionEvent', 'DanyInvasionEvent',
}


def load_fabula(repo):
    """Wydarzenia osi Fabuli z CrashScribe/src/Fabula.cs (wpisy { "X", N.f }); brak pliku albo wpisow - FABULA bez zmian."""
    p = os.path.join(repo, 'CrashScribe', 'src', 'Fabula.cs')
    try:
        with open(p, encoding='utf-8', errors='replace') as fh:
            t = fh.read()
    except OSError:
        return
    m = re.search(r'Dictionary<string, float> Timeline\s*=\s*new Dictionary<string, float>\s*\{(.*?)\};', t, re.S)
    found = set(re.findall(r'\{\s*"([A-Za-z]+)"\s*,\s*[0-9.]+f\s*\}', m.group(1))) if m else set()
    if found:
        FABULA.clear()
        FABULA.update(found)

# Lista chronionych: postac -> warunek, po ktorym wolno ja zabic/ukryc (alternatywy; kazda = zbior termow wymaganych razem).
# Plan D + uzupelnienie z dekompilacji ROT.Events (ROTLords.* uzywane przez pozniejsze wydarzenia ROT).
PROTECTED = {
    'lord_1_29': ('Ramsay Snow', [['done:BastardsEvent']]),
    'lord_2_3': ('Mance Rayder', [['done:WallSiegeEvent']]),
    'lord_5_17': ('Jon Snow', [['done:BastardsEvent']]),            # plan: JonEvent; Bastards*Event tez uzywaja ROTLords.JonSnow
    'lord_1_14': ('Daenerys Targaryen', [['ev:DanyInvasionEvent']]),
    'lord_1_11': ('Stannis Baratheon', [['done:StannisDeathEvent'], ['done:EastwatchEvent']]),  # plan: WallEvent; EastwatchEvent uzywa Stannisa
    'lord_1_17': ('Roose Bolton', [['done:RooseDeathEvent']]),
    'lord_5_1': ('Robb Stark', [['done:RedWeddingEvent']]),
    'lord_5_7': ('Bran Stark', [['done:RedWeddingEvent']]),
    'lord_5_13_1': ('Sansa Stark', [['done:RedWeddingEvent']]),
    'lord_7_1': ('Joffrey Baratheon', [['done:JoffreyEvent']]),
    'ROTlord_369': ('Aegon Targaryen', [['ev:AegonInvasionEvent']]),
    # uzupelnienie z dekompilacji
    'lord_1_9': ('Renly Baratheon', [['done:RenlyDeathEvent']]),
    'lord_1_25': ('Cortnay Penrose', [['done:RenlyDeathEvent']]),
    'lord_WE9_l': ('Davos Seaworth', [['done:BlackwaterSiegeEvent']]),
    'lord_4_14': ('Lancel Lannister', [['done:BlackwaterSiegeEvent']]),
    'lord_4_25': ('Amory Lorch', [['done:HarrenhalSiegeEvent']]),
    'lord_4_25_1': ('Polliver', [['done:HarrenhalSiegeEvent']]),
    'lord_1_3': ('Edmure Tully', [['done:RedWeddingEvent']]),
    'lord_1_50': ('Brynden Tully', [['done:RedWeddingEvent']]),
    'lord_1_5': ('Walder Frey', [['done:RedWeddingEvent']]),
    'ROTlord_513': ('Catelyn Stark', [['done:RedWeddingEvent']]),
    'lord_5_16': ('Greatjon Umber', [['done:RedWeddingEvent']]),
    'lord_2_18': ('Rickard Karstark', [['done:RickardKarstarkExecutionEvent']]),
    'lord_5_3': ('Jeor Mormont', [['done:JeorMutinyEvent']]),
    'lord_4_2': ('Cersei Lannister', [['done:JoffreyEvent']]),
    'ROTlord_568': ('Alliser Thorne', [['done:JonEvent']]),
    'lord_5_21': ('Eddison Tollett', [['done:JonEvent']]),
    'lord_2_19': ('Tormund Giantsbane', [['done:JonEvent']]),
    'lord_2_23': ('Harald Karstark', [['done:BastardsEvent']]),
    'lord_5_20': ('Smalljon Umber', [['done:BastardsEvent']]),
    'ROTlord_486': ('Yezzan zo Qaggaz', [['ev:DanyInvasionEvent']]),
    'ROTlord_373': ('Jon Connington', [['ev:AegonInvasionEvent']]),
}

# Uchwyty kod:<nazwa> - nazwane skutki C# spoza biblioteki akcji. Najwyzej 10 (bez 'test'). Nowy = wpis tutaj + w KRONIKA-FORMAT.md rozdz. 12
# (tam pelny opis dla programisty K0). nazwa -> (dozwolone numery wydarzen, gdzie, opis); gdzie = 'okno' (kolumna skutki okna - raz przy
# rozstrzygnieciu, przed lista) albo 'opcja' (skutki opcji #wybor - po wyborze, uchwyt czyta kr:NNN.w=x). Gdzie indziej (po_rozbiciu, nagroda) = BLAD.
KODY = {
    'test': ([0], 'okno', 'k00: proba silnika - tylko linia logu "kod test", bez skutkow w swiecie'),
    'zaraza': ([76], 'okno', 'blada klacz pod Meereen: ludzie miasta przez ksiege ludzi (PeopleBook, przyczyna choroba), ranni w oddzialach obok'),
    'dzwony': ([115], 'okno', 'Krolewska Przystan plonie: ludzie przez PeopleBook, dobrobyt przez Devastation, lup z kasy miasta dla rodu zdobywcy (z platnikiem)'),
    'wielka_rada': ([117], 'opcja', 'Wielka Rada po glosie gracza: lekka = tekst i relacje; pelna (ChronicleFullVersions) = krol i korona wedlug glosow'),
}
KODY_MAX = 10

# Towary rejestrowane w kodzie gry, nie w XML (TaleWorlds.CampaignSystem.DefaultItems: Grain, Meat, Hides, Tools, IronOre, HardWood,
# Charcoal, IronIngot1-6, Trash - id sprawdzone w TaleWorlds.CampaignSystem.dll i w kodzie Armoury: GetObject<ItemObject>("iron") itd.).
DEFAULT_ITEMS = {'grain', 'meat', 'hides', 'tools', 'iron', 'hardwood', 'charcoal', 'ironIngot1', 'ironIngot2', 'ironIngot3',
                 'ironIngot4', 'ironIngot5', 'ironIngot6', 'trash'}

# Wyjatki od "succeed tylko w wersji pelnej": smierc glowy rodu z dziedzicem w wersji lekkiej (plan J K4 + tabela B).
SUCCEED_LIGHT_OK = {42, 49, 52, 80}

# Wiersze #move spoza tego drzewa (m104-e5 ff9d85e7 - p10 Jaime w Riverrun).
MOVES_FROM_BRANCHES = {'p10'}

FULL_ONLY = {'own', 'ruler', 'leader', 'alliance', 'truce', 'succeed'}
# m107-kronika: wersja=smierc (smierci z kanonu w wersji lekkiej, klucz ChronicleCanonDeaths) - z akcji tylko-pelnych wolno tylko succeed
DEATH_OK = {'succeed'}
VERSIONS = ('lekka', 'pelna', 'obie', 'smierc')
# Rodzaje termow wyzwalacza #move znane CanonCompanions.BaseTrue (reszta = "nigdy" - wiersz pominiety)
MOVE_TERMS = {'ev', 'done', 'dead', 'rotday', 'prolday', 'war', 'own', 'kr'}
RE_KR = re.compile(r'^(\d{3}\.[1-9])(?:=([a-d])|~([0a-z]+))?$')   # kr:NNN.w[=x|~R] (m107: ~R - okno rozstrzygniete wierszem z R)
SIDE_CHANGE = {'clan', 'court', 'lead', 'prisoner', 'free', 'wander', 'die', 'marry', 'wed', 'succeed'}
KILL_HIDE = {'die', 'hide', 'succeed'}
CLASS = {}
for _a in ('wiesc', 'plotka', 'flag', 'kod'):
    CLASS[_a] = 'T'
for _a in ('rel', 'relp', 'relk', 'renown', 'influence'):
    CLASS[_a] = 'R'

SKILLS = {'OneHanded', 'TwoHanded', 'Polearm', 'Bow', 'Crossbow', 'Throwing', 'Riding', 'Athletics', 'Crafting', 'Scouting',
          'Tactics', 'Roguery', 'Charm', 'Leadership', 'Trade', 'Steward', 'Medicine', 'Engineering', 'Mariner', 'Boatswain', 'Shipmaster'}
SEASONS = {'winter', 'spring', 'summer', 'autumn'}
POWTORZ = {  # wyzwalacz -> opis argumentow
    'season': '', 'siegehunger': 'N', 'ironraid': '', 'oldage': '', 'brokenmen': '', 'others': 'N[,N...]',
    'captured': 'osada', 'besieged': 'osada', 'wizja': 'N',
}
POWTORZ_OPT = {'prolog', 'odstep', 'max'}

# Sprite'y ROT poza rotacja obrazkow (P8): tlo, list, portrety, wycinki, test, duplikaty, wyniki Bekartow.
SPRITE_OUT = {'event-bg', 'BlackwaterWarning', 'Davos', 'Stannis', 'Ship1', 'Ship2', 'testevent', 'jonsnowres', 'RobbMarching',
              'jon_death', 'ramsay_death'}
SPRITE_ONLY = {'stannis-slain': [81]}   # tylko w temacie z kolumna 'tylko' wskazujaca te wydarzenia
PROLOG_MAX = 23

TITLE_MAX = 45
UPPER_LINES = 4
LOWER_LINES = 3
WIDTH = 1004.0
LABEL_MAX = 60
HINT_MAX = 200
QUESTION_MAX = 200
NEWS_MAX = 250
VAR_SAMPLE = 'Ser Barristan the Bold Selmy'   # najgorszy przypadek podstawienia {ZMIENNEJ} przy mierzeniu linii

TOK_ARG = {'wladca': 'kingdom', 'glowa': 'clan', 'pan': 'settlement', 'imie': 'hero', 'nazwa': 'any', 'skarbiec': 'kingdom', 'kasa': 'settlement'}
TOK_NOARG = {'gracz', 'cel', 'kto', 'rodkto', 'region', 'liczba', 'pora'}
TOK_RECUR = {'cel', 'kto', 'rodkto', 'region', 'liczba', 'pora'}
# Typy zlozone miejsca argumentu -> typy proste (token pasuje, gdy pasuje do ktoregokolwiek; np. plotka:<id>:@cel:<dni> w oknie powracajacym).
TOK_WANT = {'captor': {'clan', 'kingdom', 'settlement'}, 'source': {'hero', 'clan', 'settlement'}}

PL_CHARS = set('\u0105\u0107\u0119\u0142\u0144\u00f3\u015b\u017a\u017c\u0104\u0106\u0118\u0141\u0143\u00d3\u015a\u0179\u017b')
QUOTES = set('"\u201c\u201d\u201e')

RE_WIN = re.compile(r'^(\d{3})\.([1-9])$')
RE_KEY = re.compile(r'^(\d{3})\.([1-9])([a-z]?)$')
RE_TEXTID = re.compile(r'^(\d{3})\.([1-9])([a-z]?)_([A-Z][A-Z0-9]*)$')
RE_BAND = re.compile(r'^b(\d{3})[a-z]?$')
RE_TASK = re.compile(r'^t(\d{3})[a-z]?$')
RE_FLAG = re.compile(r'^[a-z][a-z0-9_]{1,40}$')
RE_VAR = re.compile(r'\{([A-Z][A-Z0-9_]*)\}')
RE_PREFIX = re.compile(r'^\{=(ARM_KR_[A-Za-z0-9_]+)\}')
RE_INT = re.compile(r'^-?\d+$')
TAG = r'((?:[^>"/]|"[^"]*"|/(?!>))*)'   # atrybuty znacznika XML (tekst atrybutu moze zawierac '>'; '/>' zamyka znacznik)


# ----------------------------------------------------------------------------------------------- raport
class Report:
    def __init__(self, quiet):
        self.quiet = quiet
        self.items = []   # (gdzie, poziom, tresc)

    def e(self, where, msg):
        self.items.append((where, 'BLAD', msg))

    def w(self, where, msg):
        self.items.append((where, 'UWAGA', msg))

    def visible(self, files):
        """Pozycje do pokazania: wszystkie albo tylko dla wskazanych plikow paczek (reszta - np. dane gry, tematy - zawsze)."""
        out = []
        for where, lvl, msg in self.items:
            f = where.split(':')[0]
            if files and f.startswith('arm_kronika_k') and f not in files:
                continue
            out.append((where, lvl, msg))
        return out


# ----------------------------------------------------------------------------------------------- dane gry i repo
def read_text(path):
    with open(path, 'rb') as f:
        b = f.read()
    return b.decode('utf-8', errors='replace')


def read_xml(path):
    """XML bez komentarzy (<!-- --> - ROT ma zakomentowane osady i postacie)."""
    return re.sub(r'<!--.*?-->', '', read_text(path), flags=re.S)


def attrs_of(tag_attrs):
    return dict(re.findall(r'\b([A-Za-z_]+)="([^"]*)"', tag_attrs))


class World:
    def __init__(self, gra, repo, rep):
        self.ok = True
        mods = os.path.join(gra, 'Modules')
        self.heroes = {}      # id -> (alive, clan)
        self.wanderers = set()
        self.clans = {}       # id -> attrs
        self.kingdoms = {}    # id -> owner
        self.settlements = {}  # id -> kind (town|castle|village|hideout|other)
        self.cultures = set()
        self.items = set()
        self.sprites = set()
        self.moves = {}       # id -> (who, trigger)
        self.move_who = set()
        self.lektor = {}      # key -> (title, md5tekstu or '')
        self.lektor_titles = {}
        # krytyk K0 (22.10): nagrania Kroniki leza w manifescie Lektor/EN/arm_lektor_kronika.tsv (nagraj_kronika.py; klucz, tytul, plik, md5 tekstu,
        # md5 pliku, paczka) - nie w arm_lektor.tsv (tam okna ROT i prologu bez kluczy kr_), wiec stara kontrola md5 nigdy nie trafiala
        self.lektor_kr = {}   # key -> (tytul, plik, md5 tekstu, md5 pliku, wiersz)
        self.lektor_kr_path = None
        try:
            t = read_xml(os.path.join(mods, 'ROT-Content', 'ModuleData', 'ROT_heroes.xml'))
            for m in re.finditer(r'<Hero\b' + TAG + '/?>', t):
                a = attrs_of(m.group(1))
                if 'id' in a:
                    self.heroes[a['id']] = (a.get('alive', 'true') != 'false', a.get('faction', '').replace('Faction.', ''))
            t = read_xml(os.path.join(mods, 'ROT-Content', 'ModuleData', 'spspecialcharacters_ROTcustomwanderers.xml'))
            self.wanderers = set(re.findall(r'<NPCCharacter\b[^>]*?\bid="(ROTwanderer\d+)"', t, re.S))
            t = read_xml(os.path.join(mods, 'ROT-Content', 'ModuleData', 'spclans.xml'))
            for m in re.finditer(r'<Faction\b' + TAG + '/?>', t):
                a = attrs_of(m.group(1))
                if 'id' in a:
                    self.clans[a['id']] = a
            for a in self.clans.values():
                sf = a.get('super_faction', '')
                if sf.startswith('Kingdom.'):
                    self.kingdoms.setdefault(sf[8:], '')
            t = read_xml(os.path.join(mods, 'ROT-Content', 'ModuleData', 'ROT_spkingdoms.xml'))
            for m in re.finditer(r'<Kingdom\b' + TAG + '/?>', t):
                a = attrs_of(m.group(1))
                if 'id' in a:
                    self.kingdoms[a['id']] = a.get('owner', '').replace('Hero.', '')
            t = read_xml(os.path.join(mods, 'ROT-Content', 'ModuleData', 'spkingdoms.xslt'))
            for m in re.finditer(r'<Kingdom\s' + TAG + '/?>', t):
                a = attrs_of(m.group(1))
                if 'id' in a:
                    self.kingdoms[a['id']] = a.get('owner', '').replace('Hero.', '')
            p = os.path.join(mods, 'NavalDLC', 'ModuleData', 'kingdoms.xml')
            if os.path.isfile(p):
                for m in re.finditer(r'<Kingdom\b' + TAG + '/?>', read_xml(p)):
                    a = attrs_of(m.group(1))
                    if 'id' in a:
                        self.kingdoms.setdefault(a['id'], a.get('owner', '').replace('Hero.', ''))
            t = read_xml(os.path.join(mods, 'ROT-Map', 'ModuleData', 'settlements.xml'))
            for m in re.finditer(r'<Settlement\b' + TAG + r'(?:/>|>(.*?)</Settlement>)', t, re.S):
                a = attrs_of(m.group(1))
                body = m.group(2) or ''
                kind = 'other'
                tm = re.search(r'<Town\b([^>]*)', body)
                if tm:
                    kind = 'castle' if 'is_castle="true"' in tm.group(1) else 'town'
                elif '<Village' in body:
                    kind = 'village'
                elif '<Hideout' in body:
                    kind = 'hideout'
                if 'id' in a:
                    self.settlements[a['id']] = kind
            t = read_xml(os.path.join(mods, 'ROT-Content', 'ModuleData', 'spcultures.xml'))
            self.cultures = set(re.findall(r'<Culture\b[^>]*?\bid="([^"]*)"', t, re.S))
            item_files = [os.path.join(mods, 'ROT-Content', 'ModuleData', f) for f in ('ROTassets.xml', 'items.xml')]
            item_files += glob.glob(os.path.join(mods, 'SandBoxCore', 'ModuleData', 'items', '*.xml'))
            item_files += [os.path.join(repo, 'Armoury', 'ModuleData', 'arm_items_rare.xml')]
            for f in item_files:
                if os.path.isfile(f):
                    self.items.update(re.findall(r'<(?:Item|CraftedItem)\b[^>]*?\bid="([^"]*)"', read_xml(f), re.S))
            self.items.update(DEFAULT_ITEMS)   # towary z kodu gry (DefaultItems) - grain, iron, charcoal ... nie maja XML
            t = read_xml(os.path.join(mods, 'ROT-Content', 'GUI', 'ROT-ContentSpriteData.xml'))
            for m in re.finditer(r'<SpritePart>(.*?)</SpritePart>', t, re.S):
                b = m.group(1)
                n = re.search(r'<Name>([^<]*)</Name>', b)
                c = re.search(r'<CategoryName>([^<]*)</CategoryName>', b)
                if n and c and c.group(1) == 'ui_event_category':
                    self.sprites.add(n.group(1))
        except OSError as ex:
            rep.e('dane gry', 'nie moge przeczytac XML ROT (%s) - podaj --gra' % ex)
            self.ok = False
        # arm_canon_moves.tsv (#move: id, who, name, trigger, action, ...)
        p = os.path.join(repo, 'Armoury', 'ModuleData', 'arm_canon_moves.tsv')
        self.moves_path = p
        self.moves_line = {}
        self.move_mode = {}   # id -> '' | classic | prolog (kolumna mode #move)
        if os.path.isfile(p):
            sec = None
            for i, line in enumerate(read_text(p).splitlines(), 1):
                if line.startswith('# ') or line == '#' or not line.strip():
                    continue
                c = line.split('\t')
                if c[0].startswith('#'):
                    sec = c[0]
                    continue
                if sec == '#move' and len(c) >= 5:
                    self.moves[c[0]] = (c[1], c[3])
                    self.moves_line[c[0]] = i
                    self.move_who.add(c[1])
                    self.move_mode[c[0]] = c[5].strip() if len(c) >= 6 else ''
        else:
            rep.e('arm_canon_moves.tsv', 'brak pliku %s' % p)
        p = os.path.join(repo, 'Armoury', 'ModuleData', 'Lektor', 'EN', 'arm_lektor.tsv')
        if os.path.isfile(p):
            for i, line in enumerate(read_text(p).splitlines(), 1):
                if line.startswith('#') or not line.strip():
                    continue
                c = line.split('\t')
                if len(c) >= 2:
                    self.lektor[c[0]] = (c[1], c[4].strip() if len(c) >= 5 else '')
                    self.lektor_titles[norm_title(c[1])] = c[0]
                    if c[0].startswith('kr_'):
                        # Lektor.LoadIndex czyta arm_lektor.tsv PRZED manifestem: klucz kr_ stad zaslania wiersz manifestu i gra bez zasady md5
                        rep.e('arm_lektor.tsv:%d' % i, 'klucz Kroniki %s w arm_lektor.tsv - nagrania Kroniki tylko w arm_lektor_kronika.tsv (tu zaslania manifest '
                                                       'i gra bez kontroli md5 tekstu)' % c[0])
        p = os.path.join(repo, 'Armoury', 'ModuleData', 'Lektor', 'EN', 'arm_lektor_kronika.tsv')
        if os.path.isfile(p):
            self.lektor_kr_path = p
            for i, line in enumerate(read_text(p).splitlines(), 1):
                if line.startswith('#') or not line.strip():
                    continue
                c = [x.strip() for x in line.split('\t')]
                if len(c) < 5 or not c[0].startswith('kr_'):
                    rep.e('arm_lektor_kronika.tsv:%d' % i, 'wiersz manifestu: klucz kr_..., tytul, plik, md5 tekstu, md5 pliku[, paczka]')
                    continue
                if c[0] in self.lektor_kr:
                    rep.e('arm_lektor_kronika.tsv:%d' % i, 'klucz %s drugi raz (Lektor bierze pierwszy)' % c[0])
                    continue
                self.lektor_kr[c[0]] = (c[1], c[2], c[3], c[4], i)
        # glowy rodow i wladcy na starcie (XML) - do zakazu die/hide bez dziedzica
        self.heads = {}
        for cid, a in self.clans.items():
            o = a.get('owner', '').replace('Hero.', '')
            if o:
                self.heads.setdefault(o, []).append(cid)
        self.rulers = {}
        for kid, o in self.kingdoms.items():
            if o:
                self.rulers.setdefault(o, []).append(kid)

    def is_hero(self, x):
        return x in self.heroes or x in self.wanderers


def norm_title(t):
    t = re.sub(r'^\{=[^}]*\}', '', t).strip().rstrip('.!').lower()
    return re.sub(r'\s+', ' ', t)


# Tekst nagrania okna Kroniki DOKLADNIE jak repo glowne tools/lektor/nagraj_kronika.py (windows) i Chronicle.Voice.Text: tytul, gora, dol bez {=...},
# tytul.rstrip('.!?') + '.' + 2 x LF + gora + 2 x LF + dol, potem zamiany wymowy SAY. md5 = md5 UTF-8 tego tekstu (kolumna 4 manifestu).
VOICE_SAY = (("R'hllor", 'the Red God'),)
RE_HEX32 = re.compile(r'^[0-9a-f]{32}$')


def voice_text(t, u, l):
    s = t.rstrip('.!?') + '.\n\n' + u + '\n\n' + l
    for a, b in VOICE_SAY:
        s = s.replace(a, b)
    return s


def file_md5(path):
    h = hashlib.md5()
    with open(path, 'rb') as f:
        for blk in iter(lambda: f.read(1 << 20), b''):
            h.update(blk)
    return h.hexdigest()


# ----------------------------------------------------------------------------------------------- miara linii
def load_linie(rep):
    p = os.path.join(HERE, 'linie.py')
    saved = sys.argv
    sys.argv = [p]   # linie.py czyta szerokosc z sys.argv[2] przy imporcie - tu zawsze domyslna (WIDTH podajemy jawnie)
    try:
        spec = importlib.util.spec_from_file_location('kronika_linie', p)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        return mod
    except Exception as ex:  # brak fontu albo pliku
        rep.e('linie.py', 'nie moge zaladowac miary linii (%s)' % ex)
        return None
    finally:
        sys.argv = saved


# ----------------------------------------------------------------------------------------------- pliki TSV
class Row:
    __slots__ = ('file', 'line', 'sec', 'c')

    def __init__(self, file, line, sec, c):
        self.file = file
        self.line = line
        self.sec = sec
        self.c = c

    def where(self):
        return '%s:%d' % (os.path.basename(self.file), self.line)


def read_tsv(path, rep, cols_by_sec):
    rows = []
    try:
        with open(path, 'rb') as f:
            raw = f.read()
    except OSError as ex:
        rep.e(os.path.basename(path), 'nie moge przeczytac (%s)' % ex)
        return rows
    base = os.path.basename(path)
    if b'\x00' in raw:
        rep.e(base, 'plik zawiera bajt 0 (wyzerowany na dysku?) - silnik wylaczy cala paczke')
        return rows
    if raw.startswith(b'\xef\xbb\xbf'):
        rep.w(base, 'BOM UTF-8 na poczatku - zapisz bez BOM')
        raw = raw[3:]
    try:
        text = raw.decode('utf-8')
    except UnicodeDecodeError as ex:
        rep.e(base, 'to nie jest UTF-8 (%s)' % ex)
        return rows
    lines = text.split('\n')
    crlf = sum(1 for x in lines if x.endswith('\r'))
    if 0 < crlf < len([x for x in lines if x]):
        rep.w(base, 'mieszane konce linii (CRLF i LF)')
    sec = None
    header_ok = False
    for i, line in enumerate(lines, 1):
        line = line.rstrip('\r')
        if not line.strip():
            continue
        if line.startswith('# ') or line == '#':
            continue
        c = line.split('\t')
        if c[0].startswith('#'):
            if c[0] not in cols_by_sec:
                rep.e('%s:%d' % (base, i), 'nieznana sekcja %r (znane: %s); komentarz zaczynaj od "# "' % (c[0], ', '.join(cols_by_sec)))
                sec = None
                continue
            sec = c[0]
            want = cols_by_sec[sec]
            if c[1:] != want:
                rep.e('%s:%d' % (base, i), 'naglowek %s ma kolumny %s, a ma miec: %s' % (sec, c[1:], want))
                header_ok = False
            else:
                header_ok = True
            continue
        if sec is None:
            rep.e('%s:%d' % (base, i), 'wiersz danych przed naglowkiem sekcji')
            continue
        if not header_ok:
            continue
        want = cols_by_sec[sec]
        if len(c) > len(want):
            rep.e('%s:%d' % (base, i), 'za duzo kolumn (%d, sekcja %s ma %d)' % (len(c), sec, len(want)))
            continue
        c = c + [''] * (len(want) - len(c))
        c = [x.strip(' ') for x in c]
        rows.append(Row(path, i, sec, dict(zip(want, c))))
    return rows


# ----------------------------------------------------------------------------------------------- argumenty i tokeny
def split_args(s):
    """'a:@wladca:battania:5' -> ['a', '@wladca:battania', '5'] (token @x z argumentem zjada nastepna czesc)."""
    parts = s.split(':')
    out = []
    i = 0
    while i < len(parts):
        p = parts[i]
        if p.startswith('@') and p[1:] in TOK_ARG and i + 1 < len(parts):
            out.append(p + ':' + parts[i + 1])
            i += 2
            continue
        out.append(p)
        i += 1
    return out


class Ctx:
    """Kontekst wiersza: czy powracajace (tokeny @cel...), numer wydarzenia, gdzie."""

    def __init__(self, where, nr, recurring):
        self.where = where
        self.nr = nr
        self.recurring = recurring


def chk_token(tok, want, world, ctx, rep):
    """Token '@...' w miejscu argumentu typu want (hero|settlement|clan|kingdom|payer|any). Zwraca True gdy poprawny."""
    name = tok[1:].split(':', 1)[0]
    arg = tok.split(':', 1)[1] if ':' in tok else None
    if name in TOK_RECUR and not ctx.recurring:
        rep.e(ctx.where, 'token %s tylko w oknie powracajacym (kolumna powtorz)' % tok)
        return False
    wants = TOK_WANT.get(want, set(want.split('|')))   # typ zlozony (osada|krolestwo, rod|krolestwo, captor, source) = ktorykolwiek
    if name in TOK_NOARG:
        ok_for = {'gracz': {'hero', 'payer', 'clan', 'any'}, 'cel': {'settlement', 'any'}, 'kto': {'hero', 'payer', 'any'},
                  'rodkto': {'clan', 'any'}, 'region': {'kingdom', 'any'}, 'liczba': {'any'}, 'pora': {'any'}}[name]
        if not (wants & ok_for):
            rep.e(ctx.where, 'token %s nie pasuje w miejscu typu %s' % (tok, want))
            return False
        return True
    if name in TOK_ARG:
        if arg is None:
            rep.e(ctx.where, 'token %s wymaga argumentu (@%s:<id>)' % (tok, name))
            return False
        at = TOK_ARG[name]
        if not chk_ref(arg, at, world, ctx, rep):
            return False
        kind = {'wladca': 'hero', 'glowa': 'hero', 'pan': 'hero', 'imie': 'text', 'nazwa': 'text', 'skarbiec': 'payer', 'kasa': 'payer'}[name]
        if want == 'any' or kind in wants or ('payer' in wants and kind in ('hero', 'payer')):
            return True
        rep.e(ctx.where, 'token %s (%s) nie pasuje w miejscu typu %s' % (tok, kind, want))
        return False
    rep.e(ctx.where, 'nieznany token %s (znane: %s)' % (tok, ', '.join('@' + x for x in sorted(set(TOK_ARG) | TOK_NOARG))))
    return False


def chk_ref(x, want, world, ctx, rep, allow_tok=True):
    """Sprawdza identyfikator albo token wobec typu."""
    if x == '':
        rep.e(ctx.where, 'pusty argument (oczekiwany %s)' % want)
        return False
    if x.startswith('@'):
        if not allow_tok:
            rep.e(ctx.where, 'token %s w warunku tylko w oknie powracajacym (@cel, @kto, @rodkto, @region) - podaj id' % x)
            return False
        return chk_token(x, want, world, ctx, rep)
    if want == 'hero':
        if not world.is_hero(x):
            rep.e(ctx.where, 'nieznany bohater %r (ROT_heroes.xml / ROTwanderer*)' % x)
            return False
        if x in world.heroes and not world.heroes[x][0]:
            rep.w(ctx.where, 'bohater %s jest w ROT_heroes.xml jako martwy (alive="false")' % x)
        return True
    if want == 'clan':
        if x not in world.clans:
            rep.e(ctx.where, 'nieznany rod %r (spclans.xml)' % x)
            return False
        return True
    if want == 'kingdom':
        if x not in world.kingdoms:
            rep.e(ctx.where, 'nieznane krolestwo %r (znane: %s)' % (x, ', '.join(sorted(world.kingdoms))))
            return False
        return True
    if want == 'settlement':
        if x not in world.settlements:
            rep.e(ctx.where, 'nieznana osada %r (ROT-Map settlements.xml)' % x)
            return False
        return True
    if want == 'clan|kingdom':
        if x in world.clans or x in world.kingdoms:
            return True
        rep.e(ctx.where, 'nieznany rod ani krolestwo %r' % x)
        return False
    if want == 'clan|settlement':
        if x in world.clans or x in world.settlements:
            return True
        rep.e(ctx.where, 'nieznany rod ani osada %r' % x)
        return False
    if want == 'settlement|kingdom':
        if x in world.settlements or x in world.kingdoms:
            return True
        rep.e(ctx.where, 'nieznana osada ani krolestwo %r' % x)
        return False
    if want == 'captor':
        if x in world.clans or x in world.kingdoms or x in world.settlements:
            return True
        rep.e(ctx.where, 'nieznany rod/krolestwo/osada %r' % x)
        return False
    if want == 'payer':
        if world.is_hero(x) or x in world.clans or x in world.settlements:
            return True
        rep.e(ctx.where, 'platnik/odbiorca %r nie jest bohaterem, rodem ani osada (albo tokenem @...)' % x)
        return False
    if want == 'source':
        if world.is_hero(x) or x in world.clans or x in world.settlements:
            return True
        rep.e(ctx.where, 'zrodlo %r nie jest bohaterem, rodem ani osada' % x)
        return False
    if want == 'any':
        if world.is_hero(x) or x in world.clans or x in world.kingdoms or x in world.settlements:
            return True
        rep.e(ctx.where, 'nieznany identyfikator %r' % x)
        return False
    rep.e(ctx.where, 'wewnetrzny: typ %s' % want)
    return False


def chk_int(x, lo, hi, what, ctx, rep):
    if not RE_INT.match(x or ''):
        rep.e(ctx.where, '%s: %r to nie liczba calkowita' % (what, x))
        return None
    v = int(x)
    if v < lo or v > hi:
        rep.e(ctx.where, '%s: %d poza zakresem %d..%d' % (what, v, lo, hi))
        return None
    return v


# ----------------------------------------------------------------------------------------------- warunki
class Term:
    __slots__ = ('neg', 'base', 'plus', 'raw')

    def __init__(self, neg, base, plus, raw):
        self.neg = neg
        self.base = base
        self.plus = plus
        self.raw = raw


def parse_cond(s, ctx, rep, world, refs, what='warunki'):
    """CNF: koniunkty ' & ', w kazdej alternatywy ' | '. Zwraca liste koniunktow (lista Term) albo None przy bledzie skladni."""
    s = (s or '').strip()
    if not s:
        return []
    out = []
    ok = True
    for conj in s.split('&'):
        conj = conj.strip()
        if not conj:
            rep.e(ctx.where, '%s: pusty skladnik koniunkcji w %r' % (what, s))
            ok = False
            continue
        alts = []
        for t in conj.split('|'):
            t = t.strip()
            if not t:
                rep.e(ctx.where, '%s: pusta alternatywa w %r' % (what, s))
                ok = False
                continue
            term = chk_term(t, ctx, rep, world, refs, what)
            if term is None:
                ok = False
            else:
                alts.append(term)
        # krytyk K0 (22.10): mv:X (bez =) wiersza tylko-classic / tylko-prolog jest w kampanii drugiego trybu pominiety w pierwszej dobie
        # (CanonCompanions: Res.Skip "tylko klasyczny start" / "tylko kampania z prologiem") - rozstrzygniety = prawda od startu, wiec w alternatywie
        # caly skladnik jest prawdziwy od pierwszej doby (016.2: mv:m01 | mv:p10). "Okno za ruchem w obu trybach" = mv:m01 & mv:p10
        if len(alts) > 1:
            moded = []
            for term in alts:
                mm = re.match(r'^mv:([mp]\d\d)$', term.base)
                if mm and not term.neg and world.move_mode.get(mm.group(1)) in ('classic', 'prolog'):
                    moded.append('mv:%s (tylko %s)' % (mm.group(1), world.move_mode[mm.group(1)]))
            if moded:
                rep.w(ctx.where, '%s: %s w alternatywie | - w kampanii drugiego trybu taki wiersz jest pominiety od pierwszej doby (rozstrzygniety = prawda), '
                                 'wiec skladnik %r jest prawdziwy od startu; okno za ruchem w obu trybach: mv:A & mv:B' % (what, ', '.join(moded), conj))
        out.append(alts)
    return out if ok else None


def chk_term(t, ctx, rep, world, refs, what):
    raw = t
    neg = t.startswith('!')
    if neg:
        t = t[1:]
    plus = 0
    m = re.match(r'^(.*)\+(\d+)$', t)
    if m and not t.startswith('p:'):
        if neg:
            rep.e(ctx.where, '%s: %r - negacja z +N niedozwolona' % (what, raw))
            return None
        t = m.group(1)
        plus = int(m.group(2))
    p = t.split(':')
    kind = p[0]
    a = p[1:]

    def need(n, desc):
        if len(a) != n:
            rep.e(ctx.where, '%s: %r - %s ma miec %d argument(y): %s' % (what, raw, kind, n, desc))
            return False
        return True

    good = True
    if kind in ('ev', 'done'):
        good = need(1, '<WydarzenieROT>') and (a[0] in ROT_EVENTS or rep.e(ctx.where, '%s: nieznane wydarzenie ROT %r' % (what, a[0])))
    elif kind in ('dead', 'alive'):
        good = need(1, '<bohater>') and chk_ref(a[0], 'hero', world, ctx, rep, ctx.recurring)
    elif kind in ('clanof', 'head'):
        good = need(2, '<bohater>:<rod>') and chk_ref(a[0], 'hero', world, ctx, rep, ctx.recurring) and chk_ref(a[1], 'clan', world, ctx, rep, ctx.recurring)
    elif kind == 'leads':
        good = need(2, '<bohater>:<krolestwo>') and chk_ref(a[0], 'hero', world, ctx, rep, ctx.recurring) and chk_ref(a[1], 'kingdom', world, ctx, rep, ctx.recurring)
    elif kind == 'prisoner':
        if len(a) not in (1, 2):
            rep.e(ctx.where, '%s: %r - prisoner:<bohater>[:<rod|krolestwo|osada>]' % (what, raw))
            good = False
        else:
            good = chk_ref(a[0], 'hero', world, ctx, rep, ctx.recurring) and (len(a) == 1 or chk_ref(a[1], 'captor', world, ctx, rep, ctx.recurring))
    elif kind in ('war', 'peace'):
        good = need(2, '<krolestwo>:<krolestwo>') and chk_ref(a[0], 'kingdom', world, ctx, rep, ctx.recurring) and chk_ref(a[1], 'kingdom', world, ctx, rep, ctx.recurring)
    elif kind == 'kexists':
        good = need(1, '<krolestwo>') and chk_ref(a[0], 'kingdom', world, ctx, rep, ctx.recurring)
    elif kind == 'own':
        good = need(2, '<osada>:<rod|krolestwo>') and chk_ref(a[0], 'settlement', world, ctx, rep, ctx.recurring) and chk_ref(a[1], 'clan|kingdom', world, ctx, rep, ctx.recurring)
    elif kind == 'rotday':
        good = need(1, 'N') and chk_int(a[0], -400, 5000, raw, ctx, rep) is not None
    elif kind == 'prolday':
        good = need(1, 'N') and chk_int(a[0], 0, 400, raw, ctx, rep) is not None
    elif kind == 'kr':
        good = need(1, 'NNN.w[=opcja|~R]')
        if good:
            ref = a[0]
            opt = None
            krow = None
            if '~' in ref:
                # m107: kr:NNN.w~R - okno rozstrzygniete wierszem z R (0 = glowny, a-z = wariant), np. kr:083.1~0b
                ref, krow = ref.split('~', 1)
            if '=' in ref:
                ref, opt = ref.split('=', 1)
            if not RE_WIN.match(ref):
                rep.e(ctx.where, '%s: %r - id okna ma byc NNN.w (np. 008.1)' % (what, raw))
                good = False
            elif opt is not None and krow is not None:
                rep.e(ctx.where, '%s: %r - =opcja i ~R razem niedozwolone' % (what, raw))
                good = False
            elif opt is not None and opt not in ('a', 'b', 'c', 'd'):
                rep.e(ctx.where, '%s: %r - opcja a-d' % (what, raw))
                good = False
            elif krow is not None and not re.match(r'^[0a-z]+$', krow):
                rep.e(ctx.where, '%s: %r - ~R: 0 (wiersz glowny) i/lub litery wariantow a-z' % (what, raw))
                good = False
            else:
                refs.append(('kr', ref, opt, ctx.where, ctx.nr))
                if krow is not None:
                    refs.append(('krrow', ref, krow, ctx.where, ctx.nr))
    elif kind == 'flag':
        good = need(1, '<nazwa>') and (RE_FLAG.match(a[0]) is not None or rep.e(ctx.where, '%s: nazwa flagi %r: male litery, cyfry, _ (2-41 zn.)' % (what, a[0])))
        if good:
            refs.append(('flagq', a[0], None, ctx.where, ctx.nr))
    elif kind == 'mv':
        good = need(1, 'mNN[=done|skip]')
        if good:
            ref = a[0]
            res = None
            if '=' in ref:
                ref, res = ref.split('=', 1)
            if res is not None and res not in ('done', 'skip'):
                rep.e(ctx.where, '%s: %r - mv:<wiersz>[=done|skip]' % (what, raw))
                good = False
            elif ref not in world.moves and ref not in MOVES_FROM_BRANCHES:
                rep.e(ctx.where, '%s: %r - brak wiersza #move %s w arm_canon_moves.tsv' % (what, raw, ref))
                good = False
            else:
                refs.append(('mv', ref, res, ctx.where, ctx.nr))
    elif kind == 'cw':
        good = need(1, 'reach|dorne') and (a[0] in ('reach', 'dorne') or rep.e(ctx.where, '%s: %r - cw:reach albo cw:dorne' % (what, raw)))
    elif kind == 'season':
        good = need(1, 'winter|spring|summer|autumn') and (a[0] in SEASONS or rep.e(ctx.where, '%s: %r - pora: %s' % (what, raw, '|'.join(sorted(SEASONS)))))
    elif kind == 'near':
        # m107: near:<bohater>:<osada>:<odleglosc 1-500> - bohater (osada, loch, druzyna) w promieniu od osady (Chronicle.Terms.HeroPos)
        good = (need(3, '<bohater>:<osada>:<odleglosc>') and chk_ref(a[0], 'hero', world, ctx, rep, ctx.recurring)
                and chk_ref(a[1], 'settlement', world, ctx, rep, ctx.recurring) and chk_int(a[2], 1, 500, raw, ctx, rep) is not None)
    elif kind == 'region':
        # m107: region:<bohater>:<krolestwo> - najblizsza osada bohatera nalezy do krolestwa (jak p:region dla gracza)
        good = need(2, '<bohater>:<krolestwo>') and chk_ref(a[0], 'hero', world, ctx, rep, ctx.recurring) and chk_ref(a[1], 'kingdom', world, ctx, rep, ctx.recurring)
    elif kind == 'plan':
        mm = re.match(r'^([A-Za-z]+)-(\d+)$', ':'.join(a))
        if not mm:
            rep.e(ctx.where, '%s: %r - plan:<WydarzenieROT>-N' % (what, raw))
            good = False
        elif mm.group(1) not in ROT_EVENTS:
            rep.e(ctx.where, '%s: nieznane wydarzenie ROT %r' % (what, mm.group(1)))
            good = False
        elif mm.group(1) not in FABULA:
            rep.w(ctx.where, '%s: %r - %s nie ma na osi Fabuli (CrashScribe Fabula.cs) - term zawsze prawdziwy, decyduje od (wydarzenia osi: %s)'
                  % (what, raw, mm.group(1), ', '.join(sorted(FABULA))))
    elif kind == 'p':
        if not a:
            rep.e(ctx.where, '%s: %r - p:<region|in|near|vassal|army|rel>...' % (what, raw))
            return None
        sub, b = a[0], a[1:]
        if sub == 'region':
            good = len(b) == 1 and chk_ref(b[0], 'kingdom', world, ctx, rep, ctx.recurring)
        elif sub == 'in':
            good = len(b) == 1 and chk_ref(b[0], 'settlement', world, ctx, rep, ctx.recurring)
        elif sub == 'near':
            good = len(b) == 2 and chk_ref(b[0], 'settlement', world, ctx, rep, ctx.recurring) and chk_int(b[1], 1, 500, raw, ctx, rep) is not None
        elif sub == 'vassal':
            good = len(b) == 1 and chk_ref(b[0], 'kingdom', world, ctx, rep, ctx.recurring)
        elif sub == 'army':
            good = len(b) == 0
        elif sub == 'rel':
            good = len(b) == 2 and chk_ref(b[0], 'hero', world, ctx, rep, ctx.recurring) and chk_int(b[1], -100, 100, raw, ctx, rep) is not None
        else:
            rep.e(ctx.where, '%s: %r - nieznany term gracza p:%s' % (what, raw, sub))
            return None
        if not good:
            rep.e(ctx.where, '%s: %r - zle argumenty (p:region:<k>, p:in:<osada>, p:near:<osada>:<odl>, p:vassal:<k>, p:army, p:rel:<bohater>:<min>)' % (what, raw))
    else:
        rep.e(ctx.where, '%s: nieznany term %r (lista termow: KRONIKA-FORMAT.md rozdz. 10)' % (what, raw))
        return None
    if not good:
        return None
    return Term(neg, t, plus, raw)


def pos_terms(conds):
    """Pozytywne termy bedace samodzielnym skladnikiem koniunkcji (bez alternatyw) - baza bez +N."""
    out = set()
    for cnf in conds:
        for conj in cnf or []:
            if len(conj) == 1 and not conj[0].neg:
                out.add(conj[0].base)
    return out


def protected_ok(hero, terms):
    name, alts = PROTECTED[hero]
    for alt in alts:
        okall = True
        for need in alt:
            k, ev = need.split(':', 1)
            hit = need in terms or (k == 'ev' and ('done:' + ev) in terms)
            if not hit:
                okall = False
        if okall:
            return True
    return False


# ----------------------------------------------------------------------------------------------- skutki
class Act:
    __slots__ = ('name', 'args', 'raw')

    def __init__(self, name, args, raw):
        self.name = name
        self.args = args
        self.raw = raw


def parse_acts(s, ctx, rep, world, refs, what='skutki'):
    s = (s or '').strip()
    out = []
    if not s:
        return out
    for raw in s.split(';'):
        raw = raw.strip()
        if not raw:
            rep.e(ctx.where, '%s: pusta akcja (podwojny ";")' % what)
            continue
        a = chk_act(raw, ctx, rep, world, refs, what)
        if a is not None:
            out.append(a)
    return out


def chk_act(raw, ctx, rep, world, refs, what):
    if ':' in raw:
        name, rest = raw.split(':', 1)
    else:
        name, rest = raw, ''
    if name == 'hide':
        # hide:<bohater>[:<koniec ukrycia>] - koniec = warunek (skladnia rozdz. 10, ' & ' i ' | '; silnik: Terms.Eval), moze zawierac ':'
        p = rest.split(':', 1)
        hero = p[0]
        good = chk_ref(hero, 'hero', world, ctx, rep)
        if len(p) > 1 and p[1]:
            cnf = parse_cond(p[1], ctx, rep, world, refs, what + ' (koniec ukrycia)')
            if cnf is None:
                good = False
            else:
                refs.append(('hideend', cnf, raw, ctx.where, ctx.nr))   # recenzja K0 R-2: kr: okna powracajacego bez zapasu - UWAGA po wczytaniu okien
        return Act(name, [hero] + ([p[1]] if len(p) > 1 else []), raw) if good else None
    a = split_args(rest) if rest else []

    def need(lo, hi, desc):
        if not (lo <= len(a) <= hi):
            rep.e(ctx.where, '%s: %r - %s: %s' % (what, raw, name, desc))
            return False
        return True

    R = lambda x, t: chk_ref(x, t, world, ctx, rep)
    I = lambda x, lo, hi: chk_int(x, lo, hi, raw, ctx, rep) is not None
    good = True
    if name == 'wiesc':
        good = need(1, 1, 'wiesc:<id tekstu z #tekst>')
        if good:
            refs.append(('text', a[0], 'wiesc', ctx.where, ctx.nr))
    elif name == 'plotka':
        good = need(3, 3, 'plotka:<id tekstu>:<osada|krolestwo>:<dni>') and R(a[1], 'settlement|kingdom') and I(a[2], 1, 120)
        if good:
            refs.append(('text', a[0], 'plotka', ctx.where, ctx.nr))
    elif name == 'flag':
        good = need(1, 1, 'flag:<nazwa>') and (RE_FLAG.match(a[0]) is not None or rep.e(ctx.where, '%s: nazwa flagi %r' % (what, a[0])))
        if good:
            refs.append(('flagset', a[0], None, ctx.where, ctx.nr))
    elif name == 'kod':
        good = need(1, 1, 'kod:<nazwa z rejestru>')
        if good:
            if a[0] not in KODY:
                rep.e(ctx.where, '%s: nieznany uchwyt kod:%s (rejestr: %s; nowy = wpis w KODY i w KRONIKA-FORMAT.md)' % (what, a[0], ', '.join(sorted(KODY))))
                good = False
            elif ctx.nr not in KODY[a[0]][0]:
                rep.e(ctx.where, '%s: kod:%s dozwolony tylko w wydarzeniach %s' % (what, a[0], KODY[a[0]][0]))
                good = False
            else:
                # miejsce uchwytu: 'okno' = kolumna skutki okna (przed lista), 'opcja' = skutki opcji (po wyborze - uchwyt czyta kr:NNN.w=x)
                gdzie = {'skutki': 'okno', 'skutki opcji': 'opcja'}.get(what, what)
                if gdzie != KODY[a[0]][1]:
                    rep.e(ctx.where, '%s: kod:%s nalezy do %s (rejestr KODY: %s) - %s' % (
                        what, a[0], 'skutkow okna' if KODY[a[0]][1] == 'okno' else 'skutkow KAZDEJ opcji #wybor', KODY[a[0]][1],
                        'skutki okna ida raz przed lista, skutki opcji po wyborze (KRONIKA-FORMAT.md 3.3, 12)'))
                    good = False
    elif name == 'rel':
        good = need(3, 3, 'rel:<bohater>:<bohater>:<d>') and R(a[0], 'hero') and R(a[1], 'hero') and I(a[2], -100, 100)
        if good and a[0] == a[1]:
            rep.e(ctx.where, '%s: %r - ta sama postac dwa razy' % (what, raw))
            good = False
    elif name == 'relp':
        good = need(2, 2, 'relp:<bohater>:<d>') and R(a[0], 'hero') and I(a[1], -100, 100)
    elif name == 'relk':
        if len(a) == 2:
            good = R(a[0], 'clan|kingdom') and I(a[1], -100, 100)
        elif len(a) == 3:
            good = R(a[0], 'clan|kingdom') and R(a[1], 'clan|kingdom') and I(a[2], -100, 100)
        else:
            rep.e(ctx.where, '%s: %r - relk:<rod|krolestwo>:<d> albo relk:<grupa>:<grupa>:<d>' % (what, raw))
            good = False
    elif name in ('renown', 'influence'):
        if len(a) == 1:
            good = I(a[0], -1000, 1000)
        elif len(a) == 2:
            good = R(a[0], 'clan') and I(a[1], -1000, 1000)
        else:
            rep.e(ctx.where, '%s: %r - %s:<d> (gracz) albo %s:<rod>:<d>' % (what, raw, name, name))
            good = False
    elif name == 'gold':
        good = need(3, 3, 'gold:<platnik>:<odbiorca>:<kwota|N%>')
        if good:
            if a[0] in ('', '-', 'nic', 'null', '0', 'brak'):
                rep.e(ctx.where, '%s: %r - zloto bez platnika (zamknieta ekonomia: nic z niczego)' % (what, raw))
                good = False
            else:
                good = R(a[0], 'payer') and R(a[1], 'payer')
            if good and a[0] == a[1]:
                rep.e(ctx.where, '%s: %r - platnik = odbiorca' % (what, raw))
                good = False
            if good:
                k = a[2]
                if k.endswith('%'):
                    good = I(k[:-1], 1, 100)
                else:
                    good = I(k, 1, 1000000)
    elif name in ('security', 'loyalty'):
        good = need(2, 2, '%s:<osada>:<d>' % name) and R(a[0], 'settlement') and I(a[1], -100, 100)
        if good and not a[0].startswith('@') and world.settlements.get(a[0]) not in ('town', 'castle'):
            rep.e(ctx.where, '%s: %r - %s tylko dla miasta albo zamku' % (what, raw, name))
            good = False
    elif name == 'pop':
        good = need(2, 2, 'pop:<osada>:<d>') and R(a[0], 'settlement') and I(a[1], -100000, 100000)
    elif name == 'unhide':
        good = need(1, 2, 'unhide:<bohater>[:<osada>]') and R(a[0], 'hero') and (len(a) == 1 or R(a[1], 'settlement'))
    elif name == 'band':
        good = need(1, 1, 'band:<id z #banda>')
        if good:
            refs.append(('band', a[0], None, ctx.where, ctx.nr))
    elif name == 'task':
        good = need(1, 1, 'task:<id z #zadanie>')
        if good:
            refs.append(('task', a[0], None, ctx.where, ctx.nr))
    elif name == 'tourney':
        good = need(1, 1, 'tourney:<miasto>') and R(a[0], 'settlement')
        if good and not a[0].startswith('@') and world.settlements.get(a[0]) != 'town':
            rep.e(ctx.where, '%s: %r - turniej tylko w miescie (nie zamek/wies)' % (what, raw))
            good = False
    elif name == 'muster':
        good = need(3, 3, 'muster:<krolestwo>:<osada>:<dni>') and R(a[0], 'kingdom') and R(a[1], 'settlement') and I(a[2], 1, 60)
    elif name == 'wound':
        good = need(2, 2, 'wound:<bohater>:<dni>') and R(a[0], 'hero') and I(a[1], 1, 120)
    elif name == 'skill':
        good = need(3, 3, 'skill:<bohater>:<umiejetnosc>:<d>') and R(a[0], 'hero') and I(a[2], -300, 300)
        if good and a[1] not in SKILLS:
            rep.e(ctx.where, '%s: %r - nieznana umiejetnosc (znane: %s)' % (what, raw, ', '.join(sorted(SKILLS))))
            good = False
    elif name == 'item':
        good = need(3, 3, 'item:<bohater>:<przedmiot>:<skad: bohater|rod|osada>') and R(a[0], 'hero') and R(a[2], 'source')
        if good:
            if a[1].startswith('dragon_'):   # smoki dragon_black..dragon_thumbnail; dragonglass_axe i dragonstone_* to zwykly sprzet (P4)
                rep.e(ctx.where, '%s: %r - smoki (dragon_*) tylko Prologue.Dragons (P4), w Kronice sam tekst i flaga' % (what, raw))
                good = False
            elif a[1] not in world.items:
                rep.e(ctx.where, '%s: %r - nieznany przedmiot %s (ROTassets.xml/items.xml)' % (what, raw, a[1]))
                good = False
    elif name == 'ware':
        good = need(4, 4, 'ware:<z osady>:<do osady>:<towar>:<ilosc>') and R(a[0], 'settlement') and R(a[1], 'settlement') and I(a[3], 1, 100000)
        if good and a[0] == a[1]:
            rep.e(ctx.where, '%s: %r - ta sama osada' % (what, raw))
            good = False
        if good and a[2] not in world.items:
            rep.w(ctx.where, '%s: %r - towar %s nie znaleziony w XML (sprawdz id towaru gry)' % (what, raw, a[2]))
    elif name == 'garrison':
        good = need(3, 3, 'garrison:<osada>:<ludzi>:<skad: osada|pula>') and R(a[0], 'settlement') and I(a[1], 1, 2000)
        if good and a[2] != 'pula':
            good = R(a[2], 'settlement')
    elif name in ('clan', 'court', 'free'):
        good = need(2, 2, '%s:<bohater>:<rod>' % name) and R(a[0], 'hero') and R(a[1], 'clan')
    elif name == 'lead':
        good = need(1, 1, 'lead:<bohater>') and R(a[0], 'hero')
    elif name == 'prisoner':
        good = need(2, 2, 'prisoner:<bohater>:<rod|osada>') and R(a[0], 'hero') and R(a[1], 'clan|settlement')
    elif name == 'wander':
        good = need(2, 2, 'wander:<bohater>:<kultura>') and R(a[0], 'hero')
        if good and a[1] not in world.cultures:
            rep.e(ctx.where, '%s: %r - nieznana kultura %s (spcultures.xml)' % (what, raw, a[1]))
            good = False
    elif name == 'die':
        good = need(1, 1, 'die:<bohater>') and R(a[0], 'hero')
    elif name in ('marry', 'wed'):
        good = need(2, 2, '%s:<bohater>:<bohater>' % name) and R(a[0], 'hero') and R(a[1], 'hero')
    elif name == 'succeed':
        # m107 (recenzja A1): dziedzic @dziedzic - wybiera gra (Chronicle.Actions.PickHeir: kandydaci gry bez postaci z #move, gracza i ukrytych)
        good = need(2, 2, 'succeed:<umierajacy>:<dziedzic|@dziedzic>') and R(a[0], 'hero') and (a[1] == '@dziedzic' or R(a[1], 'hero'))
    elif name == 'own':
        good = need(2, 2, 'own:<osada>:<rod>') and R(a[0], 'settlement') and R(a[1], 'clan')
    elif name == 'ruler':
        good = need(2, 2, 'ruler:<krolestwo>:<rod>') and R(a[0], 'kingdom') and R(a[1], 'clan')
    elif name == 'leader':
        good = need(2, 2, 'leader:<rod>:<bohater>') and R(a[0], 'clan') and R(a[1], 'hero')
    elif name in ('alliance', 'truce'):
        good = need(2, 2, '%s:<krolestwo>:<krolestwo>' % name) and R(a[0], 'kingdom') and R(a[1], 'kingdom')
    elif name == 'move':
        rep.e(ctx.where, '%s: %r - akcja move odpada (plan P1): okno idzie za ruchem (po: mv:mNN) albo wiersz #move dostaje term kr:NNN.w - wpisz pare w #para' % (what, raw))
        return None
    elif name == 'remove':
        rep.e(ctx.where, '%s: %r - remove to smierc bez sladu; uzyj die albo hide' % (what, raw))
        return None
    else:
        rep.e(ctx.where, '%s: nieznana akcja %r (lista akcji: KRONIKA-FORMAT.md rozdz. 11)' % (what, raw))
        return None
    if not good:
        return None
    return Act(name, a, raw)


def act_class(name):
    return CLASS.get(name, 'S')


# ----------------------------------------------------------------------------------------------- teksty
def chk_text(cell, key, suffix, ctx, rep, linie, maxlen=None, maxlines=None, allow_vars=None, label='tekst'):
    """Komorka '{=ARM_KR_<key>_<suffix>}English text'. Zwraca (tekst bez prefiksu, zmienne) albo (None, set())."""
    want = 'ARM_KR_%s_%s' % (key.replace('.', '_'), suffix)
    m = RE_PREFIX.match(cell)
    if not m:
        rep.e(ctx.where, '%s: brak prefiksu {=%s} na poczatku' % (label, want))
        return None, set()
    if m.group(1) != want:
        rep.e(ctx.where, '%s: prefiks {=%s}, a ma byc {=%s}' % (label, m.group(1), want))
        return None, set()
    body = cell[m.end():]
    if not body.strip():
        rep.e(ctx.where, '%s: pusty tekst po prefiksie' % label)
        return None, set()
    if body != body.strip() or '  ' in body:
        rep.w(ctx.where, '%s: podwojne albo skrajne spacje' % label)
    vars_ = set(RE_VAR.findall(body))
    rest = RE_VAR.sub('', body)
    if '{' in rest or '}' in rest:
        rep.e(ctx.where, '%s: klamra { } poza {ZMIENNA} (np. drugi {=..} w srodku)' % label)
    bad = sorted(set(ch for ch in body if ch in PL_CHARS))
    if bad:
        rep.e(ctx.where, '%s: polskie znaki %s - tekst gry po angielsku' % (label, ''.join(bad)))
    if any(ch in QUOTES for ch in body):
        rep.e(ctx.where, '%s: cudzyslow - bez cytatow z ksiazek i serialu (przydomki bez cudzyslowu, mowa zalezna)' % label)
    if linie is not None:
        miss = sorted(set(ch for ch in body if ord(ch) not in linie.adv))
        if miss:
            rep.e(ctx.where, '%s: znaki spoza fontu Galahad: %s' % (label, ' '.join('U+%04X' % ord(x) for x in miss)))
    if vars_ and allow_vars is not None:
        und = vars_ - allow_vars
        if und:
            rep.e(ctx.where, '%s: zmienne %s bez definicji w kolumnie zmienne' % (label, ', '.join('{%s}' % x for x in sorted(und))))
    measured = RE_VAR.sub(VAR_SAMPLE, body)
    if maxlen is not None and len(measured) > maxlen:
        rep.e(ctx.where, '%s: %d znakow (najwyzej %d%s)' % (label, len(measured), maxlen, ', zmienne liczone jako %d zn.' % len(VAR_SAMPLE) if vars_ else ''))
    if maxlines is not None and linie is not None:
        n, _ = linie.lines(measured, WIDTH)
        if n > maxlines:
            rep.e(ctx.where, '%s: %d linii w oknie (najwyzej %d; %d znakow) - skroc' % (label, n, maxlines, len(measured)))
    return body, vars_


def plain(cell):
    return RE_PREFIX.sub('', cell or '', count=1)


# ----------------------------------------------------------------------------------------------- glowna kontrola
def main():
    ap = argparse.ArgumentParser(description='Kronika Westeros - walidator danych (KRONIKA-FORMAT.md)')
    ap.add_argument('pliki', nargs='*')
    ap.add_argument('--gra', default=DEF_GRA)
    ap.add_argument('--repo', default=DEF_REPO)
    ap.add_argument('--ciche', action='store_true')
    ap.add_argument('--stat', action='store_true')
    o = ap.parse_args()
    try:
        sys.stdout.reconfigure(encoding='utf-8')
    except Exception:
        pass
    rep = Report(o.ciche)
    md = os.path.join(o.repo, 'Armoury', 'ModuleData')
    load_fabula(o.repo)
    world = World(o.gra, o.repo, rep)
    linie = load_linie(rep)
    if len([k for k in KODY if k != 'test']) > KODY_MAX:
        rep.e('kronika_check.py', 'rejestr KODY ma wiecej niz %d uchwytow tresci' % KODY_MAX)

    # --- tematy
    themes = {}
    tpath = os.path.join(md, 'arm_kronika_tematy.tsv')
    if not os.path.isfile(tpath):
        rep.e('arm_kronika_tematy.tsv', 'brak pliku')
    else:
        for r in read_tsv(tpath, rep, {'#temat': TEMAT_COLS}):
            c = r.c
            ctx = Ctx(r.where(), None, False)
            name = c['temat']
            if not re.match(r'^[a-z][a-z_]{1,24}$', name):
                rep.e(ctx.where, 'nazwa tematu %r: male litery i _ (to tez czesc nazwy pliku kronika_<temat>_<n>)' % name)
                continue
            if name in themes:
                rep.e(ctx.where, 'temat %s drugi raz' % name)
                continue
            sprites = [x.strip() for x in c['sprite'].split(',') if x.strip()]
            if not sprites:
                rep.e(ctx.where, 'temat %s bez sprite ROT (pliki prologu moga nie istniec - sprite to zapas)' % name)
            only = []
            if c['tylko']:
                for x in c['tylko'].split(','):
                    x = x.strip()
                    if not re.match(r'^\d{3}$', x):
                        rep.e(ctx.where, 'tylko: %r - numery wydarzen NNN po przecinku' % x)
                    else:
                        only.append(int(x))
            for s in sprites:
                if world.sprites and s not in world.sprites:
                    rep.e(ctx.where, 'sprite %r nie istnieje w ROT-ContentSpriteData.xml (ui_event_category)' % s)
                if s in SPRITE_OUT:
                    rep.e(ctx.where, 'sprite %s jest poza rotacja (P8: tlo, list, portret, wycinek, test, duplikat, wynik Bekartow)' % s)
                if s in SPRITE_ONLY and (not only or not set(only) <= set(SPRITE_ONLY[s])):
                    rep.e(ctx.where, 'sprite %s tylko w temacie z kolumna tylko = %s' % (s, ','.join('%03d' % n for n in SPRITE_ONLY[s])))
            for x in [x.strip() for x in c['prolog'].split(',') if x.strip()]:
                if not re.match(r'^\d{2}$', x) or not (1 <= int(x) <= PROLOG_MAX):
                    rep.e(ctx.where, 'prolog: %r - numer obrazka prologu 01-%02d' % (x, PROLOG_MAX))
            if not c['opis']:
                rep.w(ctx.where, 'temat %s bez opisu' % name)
            themes[name] = only
        if len(themes) < 10:
            rep.e(os.path.basename(tpath), 'za malo tematow (%d)' % len(themes))

    # --- pliki paczek
    allfiles = sorted(glob.glob(os.path.join(md, 'arm_kronika_k*.tsv')))
    files = []
    for f in allfiles:
        m = re.match(r'^arm_kronika_k(\d\d)\.tsv$', os.path.basename(f))
        if not m or int(m.group(1)) not in PACZKI:
            rep.e(os.path.basename(f), 'nazwa pliku ma byc arm_kronika_kNN.tsv, NN = 00..10')
            continue
        files.append((int(m.group(1)), f))
    chosen = set(os.path.normcase(os.path.abspath(p)) for p in o.pliki)
    for p in o.pliki:
        if not os.path.isfile(p):
            rep.e(p, 'brak pliku')
    present_pk = set(k for k, _ in files)

    win_rows = {}      # id -> [Row] w kolejnosci pliku
    win_pk = {}
    choices = {}       # key (NNN.w albo NNN.w<war>) -> [Row]
    texts = {}         # id -> Row
    bands = {}
    tasks = {}
    pairs = []
    tomb = {}
    refs = []
    rows_by_file = {}
    textids = {}
    for pk, f in files:
        rows = read_tsv(f, rep, COLS)
        rows_by_file[f] = rows
        for r in rows:
            c = r.c
            if r.sec == '#okno':
                m = RE_WIN.match(c['id'])
                if not m:
                    rep.e(r.where(), 'id okna %r - ma byc NNN.w (np. 008.1)' % c['id'])
                    continue
                nr = int(m.group(1))
                if NR_PACZKA.get(nr) != pk:
                    rep.e(r.where(), 'okno %s nalezy do paczki k%02d, nie k%02d (podzial: KRONIKA-FORMAT.md rozdz. 18)' % (c['id'], NR_PACZKA.get(nr, -1), pk) if nr in NR_PACZKA else 'numer wydarzenia %03d spoza listy (001-117, 901-906, 000 test)' % nr)
                    continue
                win_rows.setdefault(c['id'], []).append(r)
                win_pk[c['id']] = pk
            elif r.sec == '#wybor':
                m = RE_KEY.match(c['okno'])
                if not m:
                    rep.e(r.where(), 'okno %r - NNN.w albo NNN.w<wariant>' % c['okno'])
                    continue
                choices.setdefault(c['okno'], []).append(r)
            elif r.sec == '#tekst':
                if c['id'] in texts:
                    rep.e(r.where(), 'tekst %s drugi raz (pierwszy %s)' % (c['id'], texts[c['id']].where()))
                    continue
                texts[c['id']] = r
            elif r.sec == '#banda':
                if c['id'] in bands:
                    rep.e(r.where(), 'banda %s drugi raz' % c['id'])
                    continue
                bands[c['id']] = r
            elif r.sec == '#zadanie':
                if c['id'] in tasks:
                    rep.e(r.where(), 'zadanie %s drugi raz' % c['id'])
                    continue
                tasks[c['id']] = r
            elif r.sec == '#para':
                pairs.append(r)
            elif r.sec == '#usuniete':
                if not RE_KEY.match(c['id']):
                    rep.e(r.where(), 'id %r - NNN.w' % c['id'])
                    continue
                if not c['powod']:
                    rep.e(r.where(), 'nagrobek bez powodu')
                tomb[c['id']] = r

    def in_scope(r):
        return not chosen or os.path.normcase(os.path.abspath(r.file)) in chosen

    for t, r in tomb.items():
        if t in win_rows:
            rep.e(r.where(), 'id %s jest nagrobkiem (#usuniete) i nie moze wrocic jako okno (%s)' % (t, win_rows[t][0].where()))

    # --- okna
    stat = {}
    lektor_chars = {}
    bound_rows = {}   # key -> [Row okna z wyborem]
    titles = {}
    used_texts = set()
    lektor_keys = set()   # klucze kr_NNN_w[war] wierszy lektor=1 z wlasnym tekstem (do nagran bez okna w manifescie)
    for wid, rows in win_rows.items():
        base = rows[0]
        nr = int(wid[:3])
        pk = win_pk[wid]
        st = stat.setdefault(pk, {'okna': 0, 'wiersze': 0, 'wybory': 0, 'bandy': 0, 'zadania': 0, 'lektor': 0, 'lwiersze': 0, 'lokna': set()})
        st['okna'] += 1
        st['wiersze'] += len(rows)
        if base.c['war'] != '':
            rep.e(base.where(), 'pierwszy wiersz okna %s ma miec pusta kolumne war (wiersz glowny); warianty pod nim' % wid)
        wars = set()
        recurring = bool(base.c['powtorz'])
        base_cond = None
        base_texts = {}
        for idx, r in enumerate(rows):
            c = r.c
            ctx = Ctx(r.where(), nr, recurring)
            is_base = idx == 0
            war = c['war']
            if not is_base:
                if not re.match(r'^[a-z]$', war):
                    rep.e(ctx.where, 'war %r - wariant to jedna mala litera a-z' % war)
                    continue
            if war in wars:
                rep.e(ctx.where, 'wariant %r okna %s drugi raz' % (war, wid))
                continue
            wars.add(war)
            key = wid + war
            # czas - tylko wiersz glowny
            if is_base:
                od = chk_int(c['od'], 0, 5000, 'od', ctx, rep) if c['od'] != '' else None
                if c['od'] == '':
                    rep.e(ctx.where, 'od: wymagane w wierszu glownym (dzien opowiesci w skali dokumentu; 0 = bez dolnej granicy)')
                if c['do'] != '':
                    do = chk_int(c['do'], 0, 9999, 'do', ctx, rep)
                    if od is not None and do is not None and do < od:
                        rep.e(ctx.where, 'do (%d) < od (%d)' % (do, od))
                po = parse_cond(c['po'], ctx, rep, world, refs, 'po')
                if c['przed']:
                    pm = re.match(r'^(ev|done):([A-Za-z]+)$', c['przed']) or re.match(r'^kr:(\d{3}\.[1-9])$', c['przed'])
                    if not pm:
                        rep.e(ctx.where, 'przed: %r - jeden term ev:<WydarzenieROT>, done:<WydarzenieROT> albo kr:NNN.w' % c['przed'])
                    elif c['przed'].startswith(('ev:', 'done:')) and pm.group(2) not in ROT_EVENTS:
                        rep.e(ctx.where, 'przed: nieznane wydarzenie ROT %r' % pm.group(2))
                    elif c['przed'].startswith('kr:'):
                        refs.append(('kr', pm.group(1), None, ctx.where, nr))
                if recurring:
                    p = [x.strip() for x in c['powtorz'].split(';')]
                    trig = p[0].split(':')
                    if trig[0] not in POWTORZ:
                        rep.e(ctx.where, 'powtorz: nieznany wyzwalacz %r (znane: %s)' % (trig[0], ', '.join(sorted(POWTORZ))))
                    else:
                        want = POWTORZ[trig[0]]
                        if want == '' and len(trig) != 1:
                            rep.e(ctx.where, 'powtorz: %s bez argumentow' % trig[0])
                        elif want == 'N' and (len(trig) != 2 or chk_int(trig[1], 1, 400, 'powtorz', ctx, rep) is None):
                            rep.e(ctx.where, 'powtorz: %s:N' % trig[0])
                        elif want == 'N[,N...]' and (len(trig) != 2 or not re.match(r'^\d+(,\d+)*$', trig[1])):
                            rep.e(ctx.where, 'powtorz: %s:N[,N...]' % trig[0])
                        elif want == 'osada' and (len(trig) not in (2, 3) or not chk_ref(trig[1], 'settlement', world, ctx, rep, False) or (len(trig) == 3 and not chk_ref(trig[2], 'kingdom', world, ctx, rep, False))):
                            rep.e(ctx.where, 'powtorz: %s:<osada>[:<krolestwo>]' % trig[0])
                    for opt in p[1:]:
                        q = opt.split(':')
                        if q[0] not in POWTORZ_OPT or (q[0] == 'prolog' and len(q) != 1) or (q[0] != 'prolog' and (len(q) != 2 or chk_int(q[1], 1, 1000, opt, ctx, rep) is None)):
                            rep.e(ctx.where, 'powtorz: opcja %r (prolog | odstep:N | max:N)' % opt)
            else:
                for col in ('od', 'do', 'po', 'przed', 'powtorz'):
                    if c[col]:
                        rep.e(ctx.where, 'kolumna %s tylko w wierszu glownym (wariant dziedziczy czas)' % col)
                po = None
            cond = parse_cond(c['warunki'], ctx, rep, world, refs, 'warunki')
            if is_base:
                base_cond = cond
            elif not c['warunki'] and not c['gracz']:
                rep.e(ctx.where, 'wariant %s bez warunkow - nie rozni sie od wiersza glownego' % war)
            gr = parse_cond(c['gracz'], ctx, rep, world, refs, 'gracz')
            # forma, inni, wersja
            if c['forma'] not in ('okno', 'wiesc', 'log'):
                rep.e(ctx.where, 'forma %r - okno|wiesc|log' % c['forma'])
            if c['inni'] not in ('okno', 'wiesc', 'nic'):
                rep.e(ctx.where, 'inni %r - okno|wiesc|nic' % c['inni'])
            # recenzja K0 R-3: pusty gracz wariantu = gracz wiersza glownego (silnik Data.CheckRows)
            eff_gracz = c['gracz'] or ('' if is_base else rows[0].c['gracz'])
            if c['inni'] != c['forma'] and not eff_gracz and c['inni'] != 'nic':
                rep.w(ctx.where, 'inni=%s bez kolumny gracz nic nie zmienia (wszyscy dostaja forme %s)' % (c['inni'], c['forma']))
            if c['wersja'] not in VERSIONS:
                rep.e(ctx.where, 'wersja %r - lekka|pelna|obie|smierc' % c['wersja'])
            elif c['wersja'] == 'smierc':
                # m107-kronika: smierc z kanonu w wersji lekkiej - zawsze wariant nad wierszem glownym obie (zapas bez smierci przy kazdym ustawieniu kluczy:
                # Canon Deaths NIE, World Changes NIE, swiat inny niz w ksiazce; glowny lekka przy Full Versions TAK nie bierze udzialu - silnik HasFull)
                if is_base:
                    rep.e(ctx.where, 'wersja smierc tylko w wariancie - wiersz glowny to zapas bez smierci (silnik: ZLY WIERSZ, okno odpada)')
                elif rows[0].c['wersja'] != 'obie':
                    rep.e(ctx.where, 'okno z wierszem smierc: wiersz glowny ma byc wersja=obie (jest %r) - zapas bez smierci przy kazdym ustawieniu kluczy' % rows[0].c['wersja'])
                else:
                    names = set(x.strip().split(':', 1)[0] for x in c['skutki'].split(';') if x.strip())
                    if c['wybor']:
                        okey = key if key in choices else wid
                        for cr in choices.get(okey, []):
                            names |= set(x.strip().split(':', 1)[0] for x in cr.c['skutki'].split(';') if x.strip())
                    if not names & {'die', 'succeed'}:
                        rep.w(ctx.where, 'wiersz smierc bez die/succeed (w skutkach i opcjach) - nikt nie ginie; wystarczy wersja obie')
            shows = 'okno' in (c['forma'], c['inni'])
            # temat
            if c['temat']:
                if themes and c['temat'] not in themes:
                    rep.e(ctx.where, 'temat %r nie istnieje w arm_kronika_tematy.tsv' % c['temat'])
                elif themes.get(c['temat']) and nr not in themes[c['temat']]:
                    rep.e(ctx.where, 'temat %s tylko dla wydarzen %s' % (c['temat'], themes[c['temat']]))
            elif shows and (is_base or not rows[0].c['temat']):
                rep.e(ctx.where, 'temat wymagany, gdy okno sie pokazuje (forma/inni = okno)')
            # zmienne
            defined = set()
            if c['zmienne']:
                for z in c['zmienne'].split(';'):
                    z = z.strip()
                    zm = re.match(r'^([A-Z][A-Z0-9_]*)=(@.+)$', z)
                    if not zm:
                        rep.e(ctx.where, 'zmienne: %r - NAZWA=@token[:arg], rozdzielone " ; "' % z)
                        continue
                    if zm.group(1) in defined:
                        rep.e(ctx.where, 'zmienne: %s drugi raz' % zm.group(1))
                    defined.add(zm.group(1))
                    tk = split_args(zm.group(2))
                    if len(tk) != 1 or tk[0][1:].split(':', 1)[0] in ('skarbiec', 'kasa'):
                        rep.e(ctx.where, 'zmienne: %r - token nazwy: @gracz, @wladca:<k>, @glowa:<rod>, @pan:<osada>, @imie:<bohater>, @nazwa:<id> albo token wyzwalacza' % z)
                    else:
                        chk_token(tk[0], 'any', world, ctx, rep)
            # teksty
            used_vars = set()
            has_text = any(c[k] for k in ('tytul', 'gora', 'dol'))
            if has_text or (is_base and shows):
                tt, v1 = chk_text(c['tytul'], key, 'T', ctx, rep, linie, maxlen=TITLE_MAX, allow_vars=defined, label='tytul')
                tu, v2 = chk_text(c['gora'], key, 'U', ctx, rep, linie, maxlines=UPPER_LINES, allow_vars=defined, label='gora')
                tl, v3 = chk_text(c['dol'], key, 'L', ctx, rep, linie, maxlines=LOWER_LINES, allow_vars=defined, label='dol')
                used_vars = v1 | v2 | v3
                eff = (tt, tu, tl)
                if is_base:
                    base_texts = {'t': tt, 'u': tu, 'l': tl}
                if tt:
                    nt = norm_title(tt)
                    if nt in titles and titles[nt][0] != wid:
                        rep.w(ctx.where, 'tytul %r jak w oknie %s (lektor szuka zapasowo po tytule)' % (tt, titles[nt][0]))
                    titles.setdefault(nt, (wid, ctx.where))
                    if nt in world.lektor_titles and not world.lektor_titles[nt].startswith('kr_'):
                        rep.w(ctx.where, 'tytul %r jak nagranie %s w arm_lektor.tsv (zapas po tytule zagralby obce nagranie)' % (tt, world.lektor_titles[nt]))
            else:
                eff = (base_texts.get('t'), base_texts.get('u'), base_texts.get('l'))
                if c['zmienne']:
                    rep.w(ctx.where, 'zmienne w wariancie bez wlasnego tekstu (wariant dziedziczy tekst i zmienne glownego; ta sama NAZWA z wariantu ma pierwszenstwo, inne nazwy tekst glownego pomija)')
            for v in defined - used_vars:
                if has_text:
                    rep.w(ctx.where, 'zmienna %s zdefiniowana, ale nieuzyta' % v)
            # lektor
            if c['lektor'] not in ('0', '1'):
                rep.e(ctx.where, 'lektor %r - 0 albo 1' % c['lektor'])
            elif c['lektor'] == '1':
                if not shows:
                    rep.e(ctx.where, 'lektor=1 tylko dla okna, ktore sie pokazuje (forma albo inni = okno)')
                if used_vars or (not has_text and RE_VAR.search(' '.join(x or '' for x in eff))):
                    rep.e(ctx.where, 'lektor=1 niedozwolony przy {ZMIENNYCH} w tekscie (nagranie bylo by inne niz okno)')
                if all(eff):
                    n = len(eff[0]) + len(eff[1]) + len(eff[2])
                    st['lektor'] += n
                    st['lwiersze'] += 1
                    st['lokna'].add(wid)
                    lk = 'kr_%s' % key.replace('.', '_')
                    # krytyk K0 (22.10): zasada md5 (KRONIKA-FORMAT 16.5) wobec manifestu arm_lektor_kronika.tsv - tekst jak nagraj_kronika.py/Chronicle.Voice.
                    # Wariant bez wlasnego tekstu gra nagranie wiersza glownego (RowDef.NarrationKey) - sprawdzany jest glowny.
                    if not has_text and not is_base:
                        rep.w(ctx.where, 'lektor=1 w wariancie bez wlasnego tekstu - gra nagranie wiersza glownego, a nagraj_kronika.py nagralby pusty tekst pod %s (ustaw lektor=0)' % lk)
                    elif world.lektor_kr_path:
                        lektor_keys.add(lk)
                        rec = world.lektor_kr.get(lk)
                        if rec is None:
                            if c['wersja'] != 'pelna':   # wiersze pelna nagraj_kronika.py pomija celowo (nagrania okien 1 paczek, plan K)
                                rep.w(ctx.where, 'lektor=1, a nagrania %s brak w arm_lektor_kronika.tsv - okno bez glosu (nagraj_kronika.py%s)'
                                      % (lk, '' if c['forma'] == 'okno' else '; skrypt nagrywa tylko forma=okno'))
                        elif not RE_HEX32.match(rec[2]):
                            rep.w(ctx.where, 'nagranie %s ze starym tekstem (manifest: %s) - okno bez glosu do ponownego nagrania (nagraj_kronika.py)' % (lk, rec[2]))
                        else:
                            md5 = hashlib.md5(voice_text(*eff).encode('utf-8')).hexdigest()
                            if md5 != rec[2]:
                                rep.e(ctx.where, 'tekst okna zmieniony po nagraniu %s (md5 %s, w arm_lektor_kronika.tsv %s) - gra da cisze: przywroc tekst albo nagraj '
                                      'ponownie (nagraj_kronika.py) / oznacz wiersz manifestu "nieaktualne"' % (lk, md5, rec[2]))
            # wybor
            if c['wybor']:
                chk_text(c['wybor'], key, 'Q', ctx, rep, linie, maxlen=QUESTION_MAX, allow_vars=defined, label='wybor')
                bound_rows.setdefault(key, []).append(r)
                if c['forma'] == 'log' and c['inni'] == 'nic':
                    rep.e(ctx.where, 'wybor przy formie log i inni=nic - gracz nigdy nie zobaczy listy')
            # wiesc
            if 'wiesc' in (c['forma'], c['inni']):
                if (key + '_W') not in texts and (wid + '_W') not in texts:
                    rep.e(ctx.where, 'forma/inni = wiesc, a brak tekstu %s_W (albo %s_W) w #tekst' % (key, wid))
                else:
                    used_texts.add(key + '_W' if (key + '_W') in texts else wid + '_W')
            # zapasowa wiesc (rozdz. 3.2 p. 4 i 5): okno z kotwica przed albo z koncem do przychodzi jako wiesc _W, gdy kotwica juz
            # zaszla albo spoznienie > 30 dni po do - wtedy _W jest uzyty, choc forma/inni nie maja wiesc
            if rows[0].c['przed'] or rows[0].c['do']:
                for tk in (key + '_W', wid + '_W'):
                    if tk in texts:
                        used_texts.add(tk)
            # skutki
            acts = parse_acts(c['skutki'], ctx, rep, world, refs, 'skutki')
            chk_acts_rules(acts, [base_cond, cond, po if is_base else None], c['wersja'], nr, ctx, rep, world)
        # powtarzalnosc wariantow: wariant bez warunkow nieosiagalny po wariancie bez warunkow
    # --- wybory
    for key, crs in choices.items():
        m = RE_KEY.match(key)
        wid = '%s.%s' % (m.group(1), m.group(2))
        nr = int(m.group(1))
        if wid not in win_rows:
            rep.e(crs[0].where(), 'wybor dla okna %s, ktorego nie ma' % wid)
            continue
        if m.group(3):
            tgt = [r for r in win_rows[wid] if r.c['war'] == m.group(3)]
            if not tgt:
                rep.e(crs[0].where(), 'wybor dla wariantu %s, ktorego nie ma' % key)
                continue
            if not tgt[0].c['wybor']:
                rep.e(crs[0].where(), 'wariant %s ma pusta kolumne wybor - lista sie nie pokaze' % key)
            bound = tgt
        else:
            own_keys = set(k for k in choices if k.startswith(wid) and k != wid)
            bound = [r for r in win_rows[wid] if r.c['wybor'] and (wid + r.c['war']) not in own_keys]
            if not bound:
                rep.e(crs[0].where(), 'okno %s nie ma zadnego wiersza z kolumna wybor - lista sie nie pokaze' % wid)
                continue
        pk = win_pk[wid]
        stat[pk]['wybory'] += 1
        letters = [r.c['opcja'] for r in crs]
        if not (2 <= len(crs) <= 4):
            rep.e(crs[0].where(), 'okno %s ma %d opcji (2-4)' % (key, len(crs)))
        if letters != ['a', 'b', 'c', 'd'][:len(letters)]:
            rep.e(crs[0].where(), 'opcje %s - kolejno a, b, c, d' % ','.join(letters))
        recurring = bool(win_rows[wid][0].c['powtorz'])
        quiet = Report(True)
        base_cond = parse_cond(win_rows[wid][0].c['warunki'], Ctx(crs[0].where(), nr, recurring), quiet, world, [])
        var_cond = parse_cond(bound[0].c['warunki'], Ctx(crs[0].where(), nr, recurring), quiet, world, []) if len(bound) == 1 and bound[0] is not win_rows[wid][0] else None
        vers = set(r.c['wersja'] for r in bound)
        for i, r in enumerate(crs):
            c = r.c
            ctx = Ctx(r.where(), nr, recurring)
            last = i == len(crs) - 1
            if c['opcja'] not in ('a', 'b', 'c', 'd'):
                continue
            chk_text(c['etykieta'], key, 'C' + c['opcja'], ctx, rep, linie, maxlen=LABEL_MAX, allow_vars=set(), label='etykieta')
            chk_text(c['podpowiedz'], key, 'H' + c['opcja'], ctx, rep, linie, maxlen=HINT_MAX, allow_vars=set(), label='podpowiedz')
            dc = parse_cond(c['dostepna'], ctx, rep, world, refs, 'dostepna')
            if c['bierna'] not in ('', '0', '1'):
                rep.e(ctx.where, 'bierna %r - 1 albo puste' % c['bierna'])
            if last:
                if c['bierna'] != '1':
                    rep.e(ctx.where, 'ostatnia opcja musi miec bierna=1 (wersja z ksiazki, autotest i gracz poza kolumna gracz)')
                if c['dostepna']:
                    rep.e(ctx.where, 'opcja bierna zawsze dostepna - kolumna dostepna pusta')
            elif c['bierna'] == '1':
                rep.e(ctx.where, 'bierna=1 tylko na ostatniej opcji')
            acts = parse_acts(c['skutki'], ctx, rep, world, refs, 'skutki opcji')
            # m107: opcje tylko wierszy pelna/smierc z co najmniej jednym smierc - jak smierc (succeed wolno, own/ruler/leader/alliance/truce nie)
            if vers == {'pelna'}:
                wv = 'pelna'
            elif vers and vers <= {'pelna', 'smierc'}:
                wv = 'smierc'
            else:
                wv = 'obie' if 'obie' in vers else 'lekka'
            chk_acts_rules(acts, [base_cond, var_cond, dc], wv, nr, ctx, rep, world)
    for key, rows in bound_rows.items():
        wid = key[:5]
        if key not in choices and wid not in choices:
            rep.e(rows[0].where(), 'kolumna wybor wypelniona, a brak opcji #wybor dla %s' % key)

    # --- teksty #tekst
    for tid, r in texts.items():
        m = RE_TEXTID.match(tid)
        ctx = Ctx(r.where(), None, False)
        if not m:
            rep.e(ctx.where, 'id tekstu %r - NNN.w[wariant]_X (np. 005.1_W, 008.2_P1); X wielkie litery/cyfry' % tid)
            continue
        if m.group(4) in ('T', 'U', 'L', 'Q') or re.match(r'^[CH][a-d]$', m.group(4)):
            rep.e(ctx.where, 'sufiks %s zarezerwowany dla kolumn okna/wyboru' % m.group(4))
        wid = '%s.%s' % (m.group(1), m.group(2))
        if wid not in win_rows:
            rep.e(ctx.where, 'tekst %s dla okna %s, ktorego nie ma' % (tid, wid))
        elif m.group(3) and not any(x.c['war'] == m.group(3) for x in win_rows[wid]):
            rep.e(ctx.where, 'tekst %s dla wariantu, ktorego nie ma' % tid)
        rec = wid in win_rows and bool(win_rows[wid][0].c['powtorz'])
        chk_text(r.c['tekst'], wid + m.group(3), m.group(4), ctx, rep, linie, maxlen=NEWS_MAX, allow_vars=None, label='tekst')
        if RE_VAR.search(r.c['tekst']) and not rec:
            rep.w(ctx.where, 'tekst wiesci z {ZMIENNA} - zmienne w #tekst biora wartosci z kolumny zmienne okna')

    # --- bandy
    for bid, r in bands.items():
        c = r.c
        m = RE_BAND.match(bid)
        ctx = Ctx(r.where(), int(m.group(1)) if m else None, False)
        if not m:
            rep.e(ctx.where, 'id bandy %r - bNNN[litera], NNN = numer wydarzenia' % bid)
            continue
        if NR_PACZKA.get(int(m.group(1))) != int(re.search(r'k(\d\d)', os.path.basename(r.file)).group(1)):
            rep.e(ctx.where, 'banda %s nie w paczce swojego wydarzenia' % bid)
        if chk_ref(c['klan'], 'clan', world, ctx, rep, False):
            a = world.clans[c['klan']]
            if a.get('is_bandit') != 'true' and a.get('is_minor_faction') != 'true' and not c['klan'].startswith('looters'):
                rep.w(ctx.where, 'klan %s nie jest bandycki ani mniejszy - sprawdzic CreateLooterParty przed paczka (plan: ryzyka)' % c['klan'])
        chk_ref(c['osada'], 'settlement', world, ctx, rep, False)
        mn = chk_int(c['min'], 1, 150, 'min', ctx, rep)
        mx = chk_int(c['max'], 1, 150, 'max', ctx, rep)
        if mn is not None and mx is not None and mn > mx:
            rep.e(ctx.where, 'min > max')
        chk_text(c['herszt'], bid, 'H', ctx, rep, linie, maxlen=40, allow_vars=set(), label='herszt')
        if c['zachowanie'] not in ('patrol', 'rabunek', 'oboz'):
            rep.e(ctx.where, 'zachowanie %r - patrol|rabunek|oboz' % c['zachowanie'])
        chk_int(c['dni'], 1, 365, 'dni', ctx, rep)
        if c['list'] not in ('0', '1'):
            rep.e(ctx.where, 'list %r - 0|1 (list gonczy)' % c['list'])
        acts = parse_acts(c['po_rozbiciu'], ctx, rep, world, refs, 'po_rozbiciu')
        chk_acts_rules(acts, [], 'lekka', ctx.nr, ctx, rep, world)
        pkk = NR_PACZKA.get(int(m.group(1)))
        if pkk in stat:
            stat[pkk]['bandy'] += 1

    # --- zadania
    for tid, r in tasks.items():
        c = r.c
        m = RE_TASK.match(tid)
        ctx = Ctx(r.where(), int(m.group(1)) if m else None, False)
        if not m:
            rep.e(ctx.where, 'id zadania %r - tNNN[litera]' % tid)
            continue
        if c['rodzaj'] not in ('deliver', 'defeat', 'prisoners'):
            rep.e(ctx.where, 'rodzaj %r - deliver|defeat|prisoners' % c['rodzaj'])
        chk_ref(c['cel'], 'settlement', world, ctx, rep, False)
        if c['rodzaj'] == 'deliver' and c['co'] not in world.items:
            rep.w(ctx.where, 'co: towar %r nie znaleziony w XML' % c['co'])
        if c['rodzaj'] == 'defeat':
            refs.append(('band', c['co'], None, ctx.where, ctx.nr))
        if c['rodzaj'] == 'prisoners' and c['co'] not in ('', '-'):
            rep.e(ctx.where, 'co: dla prisoners "-"')
        chk_int(c['ilosc'], 1, 100000, 'ilosc', ctx, rep)
        chk_int(c['dni'], 1, 365, 'dni', ctx, rep)
        acts = parse_acts(c['nagroda'], ctx, rep, world, refs, 'nagroda')
        chk_acts_rules(acts, [], 'lekka', ctx.nr, ctx, rep, world)
        chk_text(c['tytul'], tid, 'T', ctx, rep, linie, maxlen=TITLE_MAX, allow_vars=set(), label='tytul zadania')
        chk_text(c['opis'], tid, 'D', ctx, rep, linie, maxlen=NEWS_MAX, allow_vars=set(), label='opis zadania')
        pkk = NR_PACZKA.get(int(m.group(1)))
        if pkk in stat:
            stat[pkk]['zadania'] += 1

    # --- pary P1
    pair_set = {}
    for r in pairs:
        c = r.c
        ctx = Ctx(r.where(), None, False)
        if c['okno'] not in win_rows:
            rep.e(ctx.where, 'para: okna %s nie ma' % c['okno'])
            continue
        if c['wiersz'] not in world.moves and c['wiersz'] not in MOVES_FROM_BRANCHES:
            rep.e(ctx.where, 'para: wiersza #move %s nie ma w arm_canon_moves.tsv' % c['wiersz'])
            continue
        if c['kierunek'] not in ('po', 'kr'):
            rep.e(ctx.where, 'para: kierunek %r - po|kr' % c['kierunek'])
            continue
        k = (c['okno'], c['wiersz'])
        if k in pair_set:
            rep.e(ctx.where, 'para %s/%s drugi raz' % k)
        pair_set[k] = c['kierunek']
        trig = world.moves.get(c['wiersz'], ('', ''))[1]
        has_kr = any(t.strip().startswith('kr:' + c['okno']) for t in trig.split('|'))
        rows = win_rows[c['okno']]
        has_mv = any(re.search(r'(^|[\s&|!])mv:%s(=|\+|\s|$|&|\|)' % re.escape(c['wiersz']), x.c['po'] + ' ' + x.c['warunki']) for x in rows)
        if c['kierunek'] == 'po':
            if not has_mv:
                rep.e(ctx.where, 'para po: okno %s nie ma mv:%s w kolumnie po ani warunki' % k)
            if has_kr:
                rep.e(ctx.where, 'para po: wiersz %s ma term kr:%s - petla (okno czeka na ruch, ruch na okno)' % (c['wiersz'], c['okno']))
        else:
            if c['wiersz'] in MOVES_FROM_BRANCHES:
                rep.w(ctx.where, 'para kr: wiersz %s z innej galezi - sprawdz term kr: po scaleniu' % c['wiersz'])
            elif not has_kr:
                rep.e(ctx.where, 'para kr: dopisz |kr:%s do wyzwalacza wiersza %s w arm_canon_moves.tsv (stary wyzwalacz zostaje zapasem)' % (c['okno'], c['wiersz']))
            if has_mv:
                rep.e(ctx.where, 'para kr: okno %s czeka na mv:%s - petla' % k)
    # kazde mv: w oknie ma pare 'po'
    for kind, ref, opt, where, nr in refs:
        if kind == 'mv' and nr is not None:
            ok = any(w[:3] == '%03d' % nr and m == ref and d == 'po' for (w, m), d in pair_set.items())
            if not ok:
                rep.e(where, 'mv:%s bez pary w #para (okno, %s, po)' % (ref, ref))
    # kazdy kr: w arm_canon_moves ma pare 'kr'
    for mid, (who, trig) in world.moves.items():
        tt = [t.strip() for t in trig.split('|') if t.strip()]
        if tt and all(t.startswith('kr:') for t in tt):
            # przy wylaczonej Kronice kr: = nigdy (rozdz. 3.3; m107 T1: takze przy ChronicleWorldChanges=NIE) - wiersz bez starego wyzwalacza nie ruszy wtedy wcale
            rep.w('arm_canon_moves.tsv:%d' % world.moves_line.get(mid, 0), 'wiersz %s ma tylko termy kr: - przy ChronicleOfWesteros=NIE albo ChronicleWorldChanges=NIE nie ruszy nigdy (stary wyzwalacz zostaje jako zapas, rozdz. 3.3 i 8)' % mid)
        for t in trig.split('|'):
            t = t.strip()
            mwhere = 'arm_canon_moves.tsv:%d' % world.moves_line.get(mid, 0)
            # m107: rodzaj termu znany CanonCompanions.BaseTrue (inny = "nigdy") i kr:NNN.w[=x|~R] - okno, opcja i warianty istnieja (paczka napisana)
            tb = re.sub(r'\+\d+$', '', t)
            tk = tb.split(':', 1)[0]
            if t and tk not in MOVE_TERMS:
                rep.e(mwhere, 'wiersz %s: term %s - CanonCompanions zna tylko %s (inny = nigdy)' % (mid, t, ', '.join(sorted(MOVE_TERMS))))
            if tk == 'kr':
                km = RE_KR.match(tb[3:])
                if not km:
                    rep.e(mwhere, 'wiersz %s: term %s - kr:NNN.w[=x|~R] (R: 0 = wiersz glowny, a-z = warianty)' % (mid, t))
                elif km.group(1) in win_rows:
                    kw, ko, kr_ = km.groups()
                    rec_ = bool(win_rows[kw][0].c['powtorz'])
                    if (ko or kr_) and rec_:
                        rep.e(mwhere, 'wiersz %s: term %s - okno powracajace: =x i ~R nie trafiaja pod NNN.w (term nigdy nie zajdzie)' % (mid, t))
                    if ko and not any(any(r.c['opcja'] == ko for r in choices[k]) for k in choices if k[:5] == kw):
                        rep.e(mwhere, 'wiersz %s: term %s - okno %s nie ma opcji %s' % (mid, t, kw, ko))
                    if kr_:
                        wars = set(r.c['war'] for r in win_rows[kw])
                        for ch in kr_:
                            if ch != '0' and ch not in wars:
                                rep.e(mwhere, 'wiersz %s: term %s - okno %s nie ma wariantu %s' % (mid, t, kw, ch))
                elif NR_PACZKA.get(int(km.group(1)[:3])) in present_pk:
                    rep.e(mwhere, 'wiersz %s: term %s - okna %s nie ma w paczce' % (mid, t, km.group(1)))
            mm = re.match(r'^kr:(\d{3}\.[1-9])', t)
            if mm:
                w = mm.group(1)
                if NR_PACZKA.get(int(w[:3])) in present_pk and pair_set.get((w, mid)) != 'kr':
                    rep.e('arm_canon_moves.tsv:%d' % world.moves_line.get(mid, 0), 'wiersz %s ma term %s bez pary (okno, %s, kr) w #para' % (mid, t, mid))

    # --- odwolania
    set_flags = set(ref for kind, ref, _, _, _ in refs if kind == 'flagset')
    for kind, ref, opt, where, nr in refs:
        if kind == 'kr':
            rnr = int(ref[:3])
            if rnr == 0 and nr != 0:
                rep.e(where, 'kr:%s - okna testowe 000 tylko w k00' % ref)
            elif ref not in win_rows:
                if NR_PACZKA.get(rnr) in present_pk:
                    rep.e(where, 'kr:%s - okna nie ma w pliku k%02d' % (ref, NR_PACZKA.get(rnr)))
                elif rnr not in NR_PACZKA:
                    rep.e(where, 'kr:%s - numer spoza listy' % ref)
                else:
                    rep.w(where, 'kr:%s - paczka k%02d jeszcze nie napisana (sprawdzi sie po jej dopisaniu)' % (ref, NR_PACZKA[rnr]))
            elif opt is not None:
                keys = [k for k in choices if k[:5] == ref]
                if win_rows[ref][0].c['powtorz']:
                    # recenzja K0 R-10: wybor instancji zapisuje sie pod NNN.w#klucz - kr:NNN.w=x nigdy nie bylby prawdziwy ani "nigdy"
                    rep.e(where, 'kr:%s=%s - okno powracajace: wybor instancji nie trafia do kr:NNN.w=x (term nigdy nie zajdzie) - uzyj flagi z opcji' % (ref, opt))
                elif not keys:
                    rep.e(where, 'kr:%s=%s - okno nie ma wyboru' % (ref, opt))
                elif not any(any(r.c['opcja'] == opt for r in choices[k]) for k in keys):
                    rep.e(where, 'kr:%s=%s - brak opcji %s' % (ref, opt, opt))
        elif kind == 'krrow':
            # m107: kr:NNN.w~R - kazda litera R to wariant okna (0 = wiersz glowny); okno powracajace - wiersz instancji nie trafia pod NNN.w (term nigdy)
            if ref in win_rows:
                if win_rows[ref][0].c['powtorz']:
                    rep.e(where, 'kr:%s~%s - okno powracajace: wiersz instancji nie trafia do kr:NNN.w~R (term nigdy nie zajdzie) - uzyj flagi ze skutkow wiersza' % (ref, opt))
                else:
                    wars = set(r.c['war'] for r in win_rows[ref])
                    for ch in opt:
                        if ch != '0' and ch not in wars:
                            rep.e(where, 'kr:%s~%s - okno %s nie ma wariantu %s' % (ref, opt, ref, ch))
        elif kind == 'hideend':
            # recenzja K0 R-2: koniec ukrycia na kr: okna powracajacego - "nigdy" dopiero po wyczerpaniu max:N; okno, ktore nie ruszy wcale (wyzwalacz
            # nie przyjdzie), zostawia postac ukryta na zawsze - skladnik potrzebuje zapasu (| ev:X | rotday:N)
            for conj in ref:
                krs = [t for t in conj if not t.neg and t.base.startswith('kr:')]
                if krs and len(krs) == len(conj):
                    rec = [t.base[3:].split('=')[0] for t in krs if t.base[3:].split('=')[0] in win_rows and win_rows[t.base[3:].split('=')[0]][0].c['powtorz']]
                    if rec:
                        rep.w(where, '%s - koniec ukrycia tylko na kr: okna powracajacego %s (bez wyzwalacza postac zostaje ukryta na zawsze) - dopisz zapas: | ev:X | rotday:N'
                              % (opt, ', '.join(rec)))
        elif kind == 'text':
            if ref not in texts:
                rep.e(where, '%s:%s - brak tekstu %s w #tekst' % (opt, ref, ref))
            else:
                used_texts.add(ref)
        elif kind == 'band':
            if ref not in bands:
                bm = RE_BAND.match(ref)
                if bm and NR_PACZKA.get(int(bm.group(1))) not in present_pk:
                    rep.w(where, 'band:%s - paczka jeszcze nie napisana' % ref)
                else:
                    rep.e(where, 'band:%s - brak w #banda' % ref)
        elif kind == 'task':
            if ref not in tasks:
                rep.e(where, 'task:%s - brak w #zadanie' % ref)
        elif kind == 'flagq':
            if ref not in set_flags:
                rep.w(where, 'flag:%s sprawdzana, ale zadne okno jej nie ustawia (jeszcze?)' % ref)
    # samo-odwolanie po/warunki do tego samego okna
    for wid, rows in win_rows.items():
        for r in rows:
            if re.search(r'kr:%s(?![0-9])' % re.escape(wid), r.c['po'] + ' ' + r.c['warunki']):
                rep.e(r.where(), 'okno %s czeka na samo siebie (kr:%s)' % (wid, wid))
    for tid, r in texts.items():
        if tid not in used_texts:
            rep.w(r.where(), 'tekst %s nieuzyty (wiesc:/plotka:/forma wiesc)' % tid)
    used_bands = set(ref for kind, ref, _, _, _ in refs if kind == 'band')
    for bid, r in bands.items():
        if bid not in used_bands:
            rep.w(r.where(), 'banda %s nieuzyta (band:%s)' % (bid, bid))
    # krytyk K0 (22.10): pliki nagran z manifestu (dysk C zeruje pliki - wgraj-v5.ps1 robi STOP, walidator mowi o tym wczesniej) i nagrania bez okna
    if world.lektor_kr_path:
        ldir = os.path.dirname(world.lektor_kr_path)
        for lk, rec in sorted(world.lektor_kr.items()):
            where = 'arm_lektor_kronika.tsv:%d' % rec[4]
            fp = os.path.join(ldir, rec[1])
            if not os.path.isfile(fp):
                rep.e(where, 'plik nagrania %s nie istnieje (Lektor: odrzucone - okno bez glosu)' % rec[1])
            elif RE_HEX32.match(rec[3]):
                fm = file_md5(fp)
                if fm != rec[3]:
                    rep.e(where, 'plik nagrania %s inny niz w manifescie (md5 pliku %s, w manifescie %s; rozmiar %d B) - uszkodzony albo wyzerowany'
                          % (rec[1], fm, rec[3], os.path.getsize(fp)))
            if not o.pliki and lk not in lektor_keys:
                rep.w(where, 'nagranie %s bez wiersza lektor=1 z wlasnym tekstem w paczkach (zbedny plik albo zmieniony klucz okna)' % lk)

    # --- wynik
    names = set(os.path.basename(p) for p in o.pliki)
    vis = rep.visible(names)
    nerr = sum(1 for x in vis if x[1] == 'BLAD')
    nwarn = sum(1 for x in vis if x[1] == 'UWAGA')
    for where, lvl, msg in vis:
        if lvl == 'UWAGA' and o.ciche:
            continue
        print('%s: %s: %s' % (where, lvl, msg))
    if o.stat:
        tot = {}
        for pk in sorted(stat):
            s = stat[pk]
            s['lokna'] = len(s['lokna'])
            print('k%02d: okien %d (wierszy %d), wyborow %d, band %d, zadan %d, okien z lektor=1 %d (wierszy %d), znakow lektora %d'
                  % (pk, s['okna'], s['wiersze'], s['wybory'], s['bandy'], s['zadania'], s['lokna'], s['lwiersze'], s['lektor']))
            if pk != 0:
                for k, v in s.items():
                    tot[k] = tot.get(k, 0) + v
        if tot:
            print('RAZEM k01-k10: okien %d (wierszy %d), wyborow %d, band %d, zadan %d, okien z lektor=1 %d (wierszy %d), znakow lektora %d'
                  % (tot['okna'], tot['wiersze'], tot['wybory'], tot['bandy'], tot['zadania'], tot['lokna'], tot['lwiersze'], tot['lektor']))
    print('kronika_check: plikow paczek %d (%s), okien %d, wierszy okien %d, wyborow %d, tekstow %d, band %d, zadan %d, par %d, tematow %d | bledy %d, uwagi %d | WYNIK %s'
          % (len(files), ','.join('k%02d' % k for k, _ in files), len(win_rows), sum(len(v) for v in win_rows.values()), len(choices), len(texts),
             len(bands), len(tasks), len(pairs), len(themes), nerr, nwarn, 'OK' if nerr == 0 else 'BLAD'))
    return 0 if nerr == 0 else 1


def chk_acts_rules(acts, conds, wersja, nr, ctx, rep, world):
    """Reguly globalne skutkow: pelne tylko w wersji pelnej, lista chronionych, #move, glowy rodow."""
    terms = pos_terms([c for c in conds if c])
    for a in acts:
        if a.name in FULL_ONLY and wersja != 'pelna':
            if wersja == 'smierc':
                # m107: wiersz smierc (smierc kanonu w wersji lekkiej) - z akcji tylko-pelnych tylko succeed (zamki, wladcy, sojusze dalej tylko pelna)
                if a.name not in DEATH_OK:
                    rep.e(ctx.where, '%s - w wierszu wersja=smierc z akcji tylko-pelnych wolno tylko succeed (%s tylko w wierszu pelna)' % (a.raw, a.name))
            elif not (a.name == 'succeed' and nr in SUCCEED_LIGHT_OK):
                rep.e(ctx.where, '%s - akcja %s tylko w wierszu wersja=pelna (wylaczona kluczem ChronicleFullVersions)' % (a.raw, a.name))
        hero = a.args[0] if a.args else ''
        if a.name in KILL_HIDE and hero in PROTECTED and not protected_ok(hero, terms):
            name, alts = PROTECTED[hero]
            rep.e(ctx.where, '%s - %s jest na liscie chronionych: wolno dopiero po %s (dopisz to jako osobny term w warunkach okna/opcji)'
                  % (a.raw, name, ' albo '.join(' & '.join(x) for x in alts)))
        if a.name in SIDE_CHANGE and hero in world.move_who:
            rep.e(ctx.where, '%s - %s ma wiersze #move w arm_canon_moves.tsv: zmiany stron tylko przez CanonCompanions (para P1 w #para)' % (a.raw, hero))
        if a.name in SIDE_CHANGE and len(a.args) > 1 and a.name in ('marry', 'wed', 'succeed') and a.args[1] in world.move_who:
            rep.e(ctx.where, '%s - %s ma wiersze #move: zmiana tylko przez CanonCompanions' % (a.raw, a.args[1]))
        if a.name in ('die', 'hide') and hero in world.heads:
            rep.e(ctx.where, '%s - %s jest glowa rodu %s na starcie (XML): %s' % (
                a.raw, hero, ','.join(world.heads[hero]), 'smierc z dziedzicem = succeed:<umierajacy>:<dziedzic>' if a.name == 'die' else 'glowy rodu nie ukrywamy (P3)'))
        if a.name in ('die', 'hide') and hero in world.rulers:
            rep.e(ctx.where, '%s - %s jest wladca %s na starcie (XML): %s' % (
                a.raw, hero, ','.join(world.rulers[hero]), 'uzyj succeed' if a.name == 'die' else 'wladcow nie ukrywamy (P3)'))
        if a.name == 'hide' and hero.startswith('@'):
            rep.e(ctx.where, '%s - hide na tokenie niedozwolone (wladca/glowa/gracz)' % a.raw)
        if a.name in ('die', 'succeed', 'hide', 'prisoner', 'clan', 'court', 'wander', 'wed') and hero.startswith('@gracz'):
            rep.e(ctx.where, '%s - akcja na postaci gracza niedozwolona' % a.raw)


if __name__ == '__main__':
    sys.exit(main())
