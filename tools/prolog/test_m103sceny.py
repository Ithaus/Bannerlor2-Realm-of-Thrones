# m103-sceny: test grupy progow m103sceny (tools/sprawdz_logi.py) na logach syntetycznych w formacie linii Armoury (Prologue.Scenes.cs, StoryPictures.cs,
# RotWindowPictures.cs) i CrashScribe (AutotestUi "OKNO wydarzenie ROT"). Uruchomienie z korzenia repo: python -I tools/prolog/test_m103sceny.py [katalog_wyjscia]
# Kod wyjscia 0 = wszystkie oczekiwane werdykty sie zgadzaja.
import contextlib, importlib.util, io, json, os, re, sys, tempfile

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
spec = importlib.util.spec_from_file_location("sl", os.path.join(ROOT, "tools", "sprawdz_logi.py"))
sl = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sl)
SC = json.load(open(os.path.join(ROOT, "tools", "prolog", "prolog298_sceny.json"), encoding="utf-8"))["scenes"]
DAYS = [s["day"] for s in SC]
TITLES = [s["Title"] for s in SC]
OUT = sys.argv[1] if len(sys.argv) > 1 else tempfile.mkdtemp(prefix="m103sceny-")
os.makedirs(OUT, exist_ok=True)

END_LINE = ("Prolog (m101): KONIEC PROLOGU (doba 186, dzien %.1f) | Robert martwy TAK (WoundedInBattle) | korona: wladca Joffrey Baratheon (lord_7_1) - Joffrey krolem TAK "
            "(Joffrey glowa rodu przed smiercia krola (byl Robert Baratheon (ROTlord_506))) | Ned: jeniec w town_EW4 TAK (wlasciciel vlandia) | pakty: sojusze zerwane 3, "
            "pakty wygaszone 6 | potkniecia 0.")


def ts(k):
    s = 6 * 3600 + 30 * 60 + k
    return "[%02d:%02d:%02d] " % (s // 3600, s // 60 % 60, s % 60)


def start(shown=0, skipped=0, legacy=False, nxt="01 (dzien 0)"):
    return ("Prolog (m103): start sesji | klucz Prologue Scenes TAK | sceny pokazane %d/23, pominiete %d%s | nastepna %s | katalog obrazkow C:\\Gra\\Modules\\Armoury\\Images"
            " | plikow: Prolog 1, ROT 0, Kronika 0 | potkniecia 0." % (shown, skipped, " (zapis sprzed scen - minione pominiete przy pierwszym ticku mapy)" if legacy else "", nxt))


def show(i, day, shown, skipped=0, mode="ROT", img=None):
    img = img or ("arm_prolog_01 (plik prolog_01.png 1024x576, dekodowanie 21 ms)" if i == 0 else SC[i]["fallback_sprite"] + " (ROT - brak pliku)")
    return ("Prolog (m103): scena %02d '%s' pokazana | dzien %.2f (nalezna od %d) | obrazek %s | okno %s | gracz: town_EW1, menu town_wait_menus | pokazane %d/23,"
            " pominiete %d | 34 ms." % (i + 1, TITLES[i], day, DAYS[i], img, mode, shown, skipped))


def close(i, how="przycisk"):
    return "Prolog (m103): scena %02d zamknieta po 1.6 s (%s)." % (i + 1, how)


def daily(d):
    return ("Prolog (m101): dzien %d prologu (opowiesc za %.1f dni) | koniec prologu za %.1f dni | pokoj krola TAK | potkniecia 0 (razem 0)."
            % (d, 188 - d, max(0.0, 186 - d)))


def run(variant):
    """Bieg prologu 0..207 dni. Zwraca (linie Armoury, linie CrashScribe)."""
    arm, cs, k = [], [], 0
    arm.append(ts(k) + start())
    shown, nxt, open_nr = 0, 0, None
    for d in range(0, 208):
        if d > 0:
            arm.append(ts(k) + daily(d))
        if d == 186:
            arm.append(ts(k) + END_LINE % 186.0)
        while nxt < 23 and DAYS[nxt] <= d:
            day = d + 0.05
            if variant == "late" and nxt == 5:
                day += 1.5
            shown += 1
            mode = "zapas ShowInquiry (brak kategorii sprite'ow ui_event_category (ROT-Content))" if variant == "zapas" and nxt == 7 else "ROT"
            arm.append(ts(k) + show(nxt, day, shown, mode=mode))
            if mode == "ROT" and not (variant == "cs" and nxt == 10):
                cs.append(ts(k) + 'AUTOTEST: OKNO wydarzenie ROT "%s" (%s) -> [Continue...]' % (TITLES[nxt], SC[nxt]["UpperText"][:120]))
            if not (variant == "overlap" and nxt == 3):
                arm.append(ts(k) + close(nxt, "bez przycisku - okna nie ma na stosie stanow" if variant == "nobtn" and nxt == 12 else "przycisk"))
            if variant == "dup" and nxt == 2:
                arm.append(ts(k) + show(nxt, day + 0.1, shown))
                arm.append(ts(k) + close(nxt))
            nxt += 1
            k += 2
        k += 1
    arm.append(ts(k) + "Prolog (m103): sceny prologu zakonczone | pokazane 23/23, pominiete 0 (dzien prologu 186.1) | obrazki prologu do zwolnienia | okna ROT 23, zapas 0,"
               " potkniecia okna 0 | potkniecia 0.")
    if variant == "err":
        arm.append(ts(k) + "ERROR in StoryPictures.Acquire Prolog/prolog_05 (potkniecie 1): System.IO.IOException: test")
    return arm, cs


def run_legacy():
    """Wczytanie zapisu prologu z WGRANIA 101 (bez sc=) w dniu 40.5: sceny 01..04 (dni < 40) POMINIETE, potem 05 i dalej po kolei."""
    arm, k = [], 0
    arm.append(ts(k) + start(legacy=True))
    arm.append(ts(k) + "Prolog (m103): zapis prologu sprzed scen (bez sc=) - minionych scen pominietych 4 (dzien prologu 40.5).")
    for i in range(4):
        arm.append(ts(k) + "Prolog (m103): POMINIETA scena %02d '%s' - zapis prologu sprzed scen (WGRANIE 101) - minione sceny bez serii zaleglych okien (dzien prologu 40.50)."
                   % (i + 1, TITLES[i]))
    shown = 0
    for i in range(4, 8):
        shown += 1
        arm.append(ts(k) + daily(DAYS[i]))
        arm.append(ts(k) + show(i, DAYS[i] + (0.5 if i == 4 else 0.05), shown, skipped=4))
        arm.append(ts(k) + close(i))
        k += 3
    return arm


def run_reload(first):
    """Wczytanie zapisu z maska sc= (pokazane 01..21): pierwsza scena sesji ma byc 22."""
    arm = [ts(0) + start(shown=21, skipped=0, nxt="22 (dzien 182)"), ts(1) + daily(182)]
    arm.append(ts(2) + show(first, 182.05, 22))
    arm.append(ts(3) + close(first))
    return arm


def verdicts(arm, cs=None, rgl=None, name="x"):
    ap = os.path.join(OUT, "Armoury-%s.log" % name)
    open(ap, "w", encoding="utf-8").write("\n".join(arm) + "\n")
    cp = None
    if cs is not None:
        cp = os.path.join(OUT, "session-%s.log" % name)
        open(cp, "w", encoding="utf-8").write("\n".join(cs) + "\n")
    rp = None
    if rgl is not None:
        rp = os.path.join(OUT, "rgl_log_%s.txt" % name)
        open(rp, "w", encoding="utf-8").write("\n".join(rgl) + "\n")
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        sl.m103sceny_main(ap, cp, rp)
    txt = buf.getvalue()
    out = {}
    for ln in txt.splitlines():
        m = re.match(r"(SC-\d+):.*?\| (TAK|NIE|INFO|ETAP|DECYZJA)\s*\|", ln)
        if m:
            out[m.group(1)] = m.group(2)
    return out, txt


CASES = []
arm, cs = run("ok")
CASES.append(("ok", arm, cs, ["2026-10-21 rgl: nic"], {"SC-0": "TAK", "SC-1": "TAK", "SC-2": "TAK", "SC-3": "TAK", "SC-4": "TAK", "SC-5": "TAK", "SC-6": "TAK",
                                                        "SC-7": "TAK", "SC-8": "TAK", "SC-9": "INFO", "SC-10": "TAK", "SC-11": "TAK", "SC-12": "INFO", "SC-13": "TAK"}))
arm, cs = run("late")
CASES.append(("late", arm, cs, None, {"SC-3": "NIE", "SC-4": "TAK", "SC-11": "TAK"}))
arm, cs = run("overlap")
CASES.append(("overlap", arm, cs, None, {"SC-6": "NIE", "SC-3": "TAK"}))
arm, cs = run("dup")
CASES.append(("dup", arm, cs, None, {"SC-4": "NIE"}))
arm, cs = run("zapas")
CASES.append(("zapas", arm, cs, None, {"SC-8": "NIE", "SC-11": "TAK"}))
arm, cs = run("cs")
CASES.append(("cs", arm, cs, None, {"SC-11": "NIE"}))
arm, cs = run("nobtn")
CASES.append(("nobtn", arm, cs, None, {"SC-10": "NIE"}))
arm, cs = run("err")
CASES.append(("err", arm, cs, None, {"SC-1": "NIE"}))
# recenzja m103: assert gry "Trying to render a released texture" w grze wydaniowej nie trafia do zadnego logu (MBDebugManager.Assert puste) - SC-13 liczy linie
# Armoury z kontroli przed zwolnieniem (StoryPictures.OnScreen); stary przypadek z wymyslona linia rgl_log usuniety
arm, cs = run("ok")
arm.append(ts(9000) + "Obrazki (m103): ZWOLNIENIE PRZY OKNIE arm_prolog_01 - okno wydarzenia ROT na wierzchu ekranu z tym obrazkiem - zwolnienie wstrzymane"
           " (blad kolejki obrazkow: tekstura w kolejce, a okno ja rysuje) | wstrzymanych w sesji 1.")
CASES.append(("przy-oknie", arm, cs, None, {"SC-13": "NIE", "SC-1": "TAK"}))
CASES.append(("legacy", run_legacy(), None, None, {"SC-7": "TAK", "SC-4": "TAK", "SC-12": "TAK", "SC-3": "TAK", "SC-11": "INFO", "SC-2": "TAK"}))
CASES.append(("reload-ok", run_reload(21), None, None, {"SC-12": "TAK", "SC-3": "TAK", "SC-2": "TAK", "SC-13": "TAK"}))
CASES.append(("reload-zle", run_reload(19), None, None, {"SC-12": "NIE"}))
CASES.append(("klasyczna", [ts(0) + "Prolog (m101): start sesji | prolog NIE | potkniecia 0."], None, None, {"SC-0": "INFO", "SC-1": "TAK", "SC-13": "TAK"}))
# recenzja m103: w kampanii klasycznej dzialaja latka okien ROT, StoryWindow.Watch i StoryPictures.AppTick - ich bledy maja dac NIE, nie samo INFO
CASES.append(("klas-blad", [ts(0) + "Prolog (m101): start sesji | prolog NIE | potkniecia 0.",
                            ts(1) + "Obrazki (m103): okno ROT BlackwaterEvent_1 'Stannis Claims the Iron Throne' - prefab Blackwater ROT (obrazek wpisany w prefab, bez"
                                    " @HeaderImage) - obrazek z pliku niemozliwy, zostaje obrazek ROT.",
                            ts(2) + "ERROR in RotWindowPictures.ActivatePrefix (potkniecie 1): System.NullReferenceException: test",
                            ts(3) + "ERROR in StoryPictures.AppTick (potkniecie 1): System.InvalidOperationException: test"], None, None,
              {"SC-0": "INFO", "SC-1": "NIE", "SC-13": "TAK", "SC-14": "INFO"}))

bad = 0
for name, arm, cs, rgl, exp in CASES:
    got, txt = verdicts(arm, cs, rgl, name)
    diff = {k: (v, got.get(k)) for k, v in exp.items() if got.get(k) != v}
    print("%-10s %s" % (name, "OK" if not diff else "ROZNICA " + str(diff)))
    if diff:
        bad += 1
        print(txt)
# m101prolog na tym samym logu - bez wywrotki (grupy czytaja te same linie m101)
buf = io.StringIO()
with contextlib.redirect_stdout(buf):
    sl.m101prolog_main(os.path.join(OUT, "Armoury-ok.log"))
print("m101prolog na logu ok: %d wierszy progow, bez wyjatku" % len([l for l in buf.getvalue().splitlines() if re.match(r"PR-\d+", l)]))
print("WYNIK: %s (%d przypadkow, rozbieznych %d) | logi: %s" % ("OK" if bad == 0 else "BLAD", len(CASES), bad, OUT))
sys.exit(1 if bad else 0)
