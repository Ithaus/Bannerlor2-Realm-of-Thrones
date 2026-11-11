#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""416 (audyt RBM 11.11, A1 + A2): progi przebicia RBM dla strzal, beltow i oszczepow w config.xml RBM.

  /Config/RBMCombat/WeaponTypes/Arrow/ExtraArmorThresholdFactorCut      2.6 -> 2
  /Config/RBMCombat/WeaponTypes/Bolt/ExtraArmorThresholdFactorCut       2.6 -> 2
  /Config/RBMCombat/WeaponTypes/Javelin/ExtraArmorThresholdFactorPierce 3   -> 2

Plik: Documents/Mount and Blade II Bannerlord/Configs/RBM/config.xml (RBMConfig.Utilities.GetConfigFilePath).
RBM czyta go przy starcie gry (SubModule.OnSubModuleLoad, RegisterSubModuleTypes, OnGameStart -> RBMConfig.LoadConfig) i zaraz zapisuje
z powrotem z pamieci (parseXmlConfig -> saveXmlConfig) - nasze wartosci zostaja. Ekran ustawien RBM tych wezlow nie pokazuje.
UWAGA: gdy RBM uzna plik za zly (brak atrybutu version="2" albo blad XML), tworzy nowy z wartosciami fabrycznymi - wtedy uruchom skrypt ponownie.
Kontrola w grze: linia "Wypoczety (416)" w Armoury.log konczy sie "progi RBM w pamieci: Arrow ciecie 2.00, Bolt ciecie 2.00, Javelin klucie 2.00 (...)".
416-p: nawias na koncu tej linii mowi, co jest wlasciwe - przy wlaczonej drabinie 423 "fabryczne 2.60 / 2.60 / 3.00 = WLASCIWE" (patrz UWAGA 423 nizej).

UWAGA 423 (11.11): zestaw A1 / A2 liczono dla drabiny pancerzy ROT SPRZED 423 (przeszywanica 61, kolczuga 88, plyta 120). Po 423a korpusy ROT
sa w skali RBM (26 / 55 / 91), a progi fabryczne 2.6 / 3 RBM stroil wlasnie pod te skale: strzala szeroka 90 lb przy progu 2.6 daje juz
55 HP w ubranie i 26 w przeszywanice (przed 423: 9 i 5), oszczep 84 HP w przeszywanice. Przy wlaczonej drabinie 423 stosuj ten skrypt
WYLACZNIE po pomiarze autotestem (linia "Drabina pancerzy (423): koniec misji"); nowych zestawow progow tu nie ma do czasu pomiaru.

Uzycie (gra ZAMKNIETA):
  python tools/rbm_config_combat.py                   # --dry-run (domyslnie): pokazuje stare i nowe wartosci, niczego nie zapisuje
  python tools/rbm_config_combat.py --apply           # kopia config.xml.bak-<data>-przed-416 i zapis
  python tools/rbm_config_combat.py --apply --force   # nadpisz takze wartosc inna niz fabryczna
  python tools/rbm_config_combat.py --config <sciezka>

Idempotentny: przy wartosciach docelowych niczego nie zapisuje i nie robi kopii. Zmienia TYLKO trzy wezly (podmiana tekstu wezla,
reszta pliku bajt w bajt; kontrola po zmianie: ponowny rozbior XML i porownanie wszystkich pozostalych wezlow).
Kody wyjscia: 0 = dobrze (takze "nic do zrobienia"), 1 = blad / odmowa.
"""
from __future__ import print_function

import argparse
import datetime
import os
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

# (typ broni, wezel, wartosc fabryczna RBM, wartosc docelowa)
CHANGES = [
    ("Arrow", "ExtraArmorThresholdFactorCut", 2.6, 2.0),
    ("Bolt", "ExtraArmorThresholdFactorCut", 2.6, 2.0),
    ("Javelin", "ExtraArmorThresholdFactorPierce", 3.0, 2.0),
]
EPS = 1e-4
BASE = "/Config/RBMCombat/WeaponTypes/"


def default_config():
    home = os.environ.get("USERPROFILE") or os.path.expanduser("~")
    return os.path.join(home, "Documents", "Mount and Blade II Bannerlord", "Configs", "RBM", "config.xml")


def num(text):
    return float(text.strip().replace(",", "."))


def fmt(v):
    return "%g" % v


def flatten(root):
    """Sciezka -> tekst dla wszystkich lisci (do kontroli 'nic poza trzema wezlami sie nie zmienilo')."""
    out = {}

    def walk(e, path):
        kids = list(e)
        if not kids:
            out[path] = (e.text or "").strip()
        counts = {}
        for k in kids:
            counts[k.tag] = counts.get(k.tag, 0) + 1
            walk(k, path + "/" + k.tag + ("[%d]" % counts[k.tag] if counts[k.tag] > 1 else ""))

    walk(root, "/" + root.tag)
    out["@root-attrs"] = repr(sorted(root.attrib.items()))
    return out


def game_running():
    if os.name != "nt":
        return False
    try:
        out = subprocess.check_output(["tasklist", "/FO", "CSV", "/NH"], stderr=subprocess.DEVNULL).decode("mbcs", "replace").lower()
    except Exception:
        return False
    return any(n in out for n in ('"bannerlord.exe"', '"bannerlord.blse.', '"bannerlord_be.exe"', '"bannerlord.native.exe"', '"taleworlds.mountandblade.launcher.exe"'))


WARN_423 = ("UWAGA 423: progi 2.0 (A1 / A2) liczono dla drabiny pancerzy ROT sprzed 423. Przy wlaczonym kluczu 'ROT Body Armour In RBM Scale' "
            "progi fabryczne 2.6 / 3 sa wlasciwe dla nowej skali - ten skrypt stosuj wylacznie po pomiarze autotestem.")


def main():
    ap = argparse.ArgumentParser(description="416: progi przebicia RBM (Arrow / Bolt ciecie 2.6 -> 2, Javelin klucie 3 -> 2)")
    ap.add_argument("--config", default=default_config(), help="sciezka do config.xml RBM")
    g = ap.add_mutually_exclusive_group()
    g.add_argument("--apply", action="store_true", help="zapisz zmiany (z kopia .bak)")
    g.add_argument("--dry-run", action="store_true", help="tylko pokaz (domyslnie)")
    ap.add_argument("--force", action="store_true", help="zmien takze wezel o wartosci innej niz fabryczna")
    a = ap.parse_args()
    path = a.config
    print("Plik: " + path)
    print("Tryb: " + ("APPLY" if a.apply else "dry-run (niczego nie zapisuje)"))
    print(WARN_423)
    if not os.path.isfile(path):
        print("BLAD: brak pliku. RBM tworzy go przy pierwszym starcie gry.")
        return 1
    with open(path, "rb") as f:
        raw = f.read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    try:
        text = raw[3:].decode("utf-8") if bom else raw.decode("utf-8")
        root = ET.fromstring(text)
    except Exception as e:
        print("BLAD: plik nie jest poprawnym XML w UTF-8 (%s) - nie ruszam." % e)
        return 1
    if root.tag != "Config" or root.attrib.get("version") != "2":
        print("BLAD: korzen nie jest <Config version=\"2\"> (jest <%s version=%r>) - RBM zastapi taki plik fabrycznym; nie ruszam." % (root.tag, root.attrib.get("version")))
        return 1
    wt = root.find("RBMCombat/WeaponTypes")
    if wt is None:
        print("BLAD: brak /Config/RBMCombat/WeaponTypes - nie ruszam.")
        return 1

    new_text = text
    todo, refused = 0, 0
    for weapon, node, factory, target in CHANGES:
        label = "WeaponTypes/%s/%s" % (weapon, node)
        ws = wt.findall(weapon)
        if len(ws) != 1 or len(ws[0].findall(node)) != 1:
            print("  %-52s BLAD: wezel nie wystepuje dokladnie raz - nie ruszam." % label)
            refused += 1
            continue
        old_txt = (ws[0].find(node).text or "").strip()
        try:
            old = num(old_txt)
        except ValueError:
            print("  %-52s BLAD: wartosc %r nie jest liczba - nie ruszam." % (label, old_txt))
            refused += 1
            continue
        if abs(old - target) < EPS:
            print("  %-52s %s -> %s  (juz ustawione)" % (label, old_txt, fmt(target)))
            continue
        if abs(old - factory) > EPS and not a.force:
            print("  %-52s %s -> %s  ODMOWA: wartosc inna niz fabryczna %s (ktos ja zmienil) - uzyj --force" % (label, old_txt, fmt(target), fmt(factory)))
            refused += 1
            continue
        # podmiana tekstu tylko w bloku <weapon>...</weapon> wewnatrz <WeaponTypes>
        m_wt = re.search(r"<WeaponTypes>.*?</WeaponTypes>", new_text, re.S)
        m_w = re.search(r"<%s>.*?</%s>" % (weapon, weapon), m_wt.group(0), re.S) if m_wt else None
        m_n = re.search(r"<%s>([^<]*)</%s>" % (node, node), m_w.group(0)) if m_w else None
        if not m_n:
            print("  %-52s BLAD: nie znalazlem wezla w tekscie pliku - nie ruszam." % label)
            refused += 1
            continue
        s0 = m_wt.start() + m_w.start() + m_n.start(1)
        s1 = m_wt.start() + m_w.start() + m_n.end(1)
        new_text = new_text[:s0] + fmt(target) + new_text[s1:]
        print("  %-52s %s -> %s%s" % (label, old_txt, fmt(target), "" if abs(old - factory) < EPS else "  (--force: wartosc nie byla fabryczna)"))
        todo += 1

    if refused:
        print("Wynik: %d wezlow odmowa / blad - NICZEGO nie zapisano." % refused)
        return 1
    if todo == 0:
        print("Wynik: nic do zrobienia - wszystkie trzy wezly maja wartosci docelowe.")
        return 0

    # kontrola: po zmianie rozni sie dokladnie tyle lisci, ile zmienilismy, sa to nasze wezly i maja wartosci docelowe
    before, after = flatten(root), flatten(ET.fromstring(new_text))
    ours = set(BASE + w + "/" + n for w, n, _, _ in CHANGES)
    diff = sorted(k for k in set(before) | set(after) if before.get(k) != after.get(k))
    if len(diff) != todo or any(k not in ours for k in diff):
        print("BLAD: kontrola po zmianie - roznia sie nie te wezly: %s. Niczego nie zapisano." % diff)
        return 1
    for w, n, _, t in CHANGES:
        if abs(num(after[BASE + w + "/" + n]) - t) > EPS:
            print("BLAD: kontrola po zmianie - %s/%s nie ma wartosci docelowej. Niczego nie zapisano." % (w, n))
            return 1

    if not a.apply:
        print("Wynik: dry-run - do zmiany %d wezlow. Uruchom z --apply (gra zamknieta) - przy wlaczonej drabinie 423 dopiero po pomiarze." % todo)
        return 0
    if game_running():
        print("BLAD: gra jest uruchomiona - RBM ma stare wartosci w pamieci i moze je zapisac z powrotem. Zamknij gre i powtorz.")
        return 1
    bak = path + ".bak-" + datetime.datetime.now().strftime("%Y-%m-%d-%H%M%S") + "-przed-416"
    shutil.copy2(path, bak)
    with open(bak, "rb") as f:
        if f.read() != raw:
            print("BLAD: kopia %s rozni sie od oryginalu - niczego nie zapisano." % bak)
            return 1
    tmp = path + ".tmp-416"
    with open(tmp, "wb") as f:
        f.write((b"\xef\xbb\xbf" if bom else b"") + new_text.encode("utf-8"))
    os.replace(tmp, path)
    with open(path, "rb") as f:
        chk = f.read()
    if flatten(ET.fromstring((chk[3:] if bom else chk).decode("utf-8"))) != after:
        print("BLAD: plik po zapisie nie zgadza sie z oczekiwanym - przywroc kopie: " + bak)
        return 1
    print("Kopia: " + bak)
    print("Wynik: zapisano %d wezlow. Po starcie gry sprawdz w Armoury.log linie 'Wypoczety (416)': "
          "'progi RBM w pamieci: Arrow ciecie 2.00, Bolt ciecie 2.00, Javelin klucie 2.00 (...)'. Przy wlaczonej drabinie 423 nawias na koncu tej linii "
          "przypomina, ze wlasciwe sa progi fabryczne 2.60 / 2.60 / 3.00 - zapisane wartosci 2.00 trzymaj tylko po pomiarze autotestem." % todo)
    return 0


if __name__ == "__main__":
    sys.exit(main())
