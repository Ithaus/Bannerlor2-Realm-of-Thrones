# -*- coding: utf-8 -*-
"""
LUDZKI INNY (Jeff 17.09.2026: "ten model Innego jest strasznie slaby - moge zrobic go jak
czlowieka, tylko wybrac mu odpowiednie oczy, skore i wlosy?").

ROT-Content/ModuleData/skins.xml, rasa "whitewalker" (ROT nadaje ja mezczyznom zamienionym
w Innych: OthersPatches ApplyByExecution / TakePrisoner, ROTOthersCampaignBehavior -
Race = 1). Meskie skiny adult i teenager mialy wlasne siatki (whitewalker_body/head/hands/
feet) i wlasne tekstury twarzy (whitewalker3); zenskie skiny tej rasy juz od autora ROT
uzywaja ludzkich siatek. Tu:
  1. meskie adult/teenager: siatki ciala/glowy/rak/stop = ludzkie meskie (jak w Native),
     blok <face_textures> i <eyebrow_meshes> = ludzki (tekstury twarzy sa robione pod
     siatke glowy), reszta (deform_keys, wlosy, brody, tatuaze, glos male_whitewalker,
     min_scale 1.07) bez zmian - bloki sa i tak identyczne z ludzkimi;
  2. WSZYSTKIE skiny rasy whitewalker: palety = paleta Innego - skora od sniezno-bialej
     do blado-niebieskawej, wlosy od bialych do srebrnych, oczy lodowo-niebieskie. Klucz
     twarzy nawroconego lorda (rysy + przesuniecia koloru 0..1) trafia w te palete, wiec
     kazdy Inny wyglada jak blady, bialowlosy czlowiek z lodowymi oczami, a rysy zostaja jego.
Rasa "wight" (upiory; ROT daje ja nawroconym KOBIETOM) - nietknieta (Jeff: "kobiety nie").
skins.xml czyta silnik (FaceGen), nie MBObjectManager - nakladka xslt tu nie dziala,
stad edycja pliku ROT-Content wprost (kopia .bak obok).

Uzycie: python skins_whitewalker_human.py <ROT-Content skins.xml> <Native skins.xml> <wyjscie>
"""
import re, sys, xml.dom.minidom

SRC, NATIVE, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
raw = open(SRC, "rb").read()
bom = raw.startswith(b"\xef\xbb\xbf")
text = raw.decode("utf-8-sig")
eol = "\r\n" if "\r\n" in text else "\n"
native = open(NATIVE, "rb").read().decode("utf-8-sig")

SKIN_RGB  = ((1.00, 1.00, 1.00), (0.80, 0.86, 0.92))   # sniezna biel -> blady lod
HAIR_RGB  = ((1.00, 1.00, 1.00), (0.72, 0.76, 0.82))   # biel -> srebro
EYE_RGB   = ((0.62, 0.90, 1.00), (0.18, 0.48, 0.90))   # jasny lod -> glebszy blekit

def race_block(t, rid):
    m = re.search(r'(<race\s+[^>]*?id="%s"[^>]*>)(.*?)(</race>)' % rid, t, re.S)
    if not m: raise SystemExit("brak rasy " + rid)
    return m

def skins_of(block):
    return list(re.finditer(r'<skin\b(.*?)>(.*?)</skin>', block, re.S))

def attr(head, k):
    m = re.search(r'\b%s="([^"]*)"' % k, head)
    return m.group(1) if m else None

def find_skin(block, gender, maturity):
    for sk in skins_of(block):
        head = re.sub(r"\s+", " ", sk.group(1))
        if attr(head, "gender") == gender and attr(head, "mesh_maturity_type") == maturity:
            return sk
    return None

def sub_block(body, tag):
    return re.search(r'<%s\b[^>]*>.*?</%s>' % (tag, tag), body, re.S)

def gradient(tag, n, rgb):
    (r0, g0, b0), (r1, g1, b1) = rgb
    pts = []
    for i in range(n):
        f = i / float(max(1, n - 1))
        pts.append('\t\t\t\t<%s point="%.3f, %.3f, %.3f" />' % (tag[:-1], r0 + (r1 - r0) * f, g0 + (g1 - g0) * f, b0 + (b1 - b0) * f))
    return "<%s>%s%s%s\t\t\t</%s>" % (tag, eol, eol.join(pts), eol, tag)

human = race_block(native, "human").group(2)
ww = race_block(text, "whitewalker")
ww_body = ww.group(2)
changes = []

def set_attr(head, k, v):
    n = len(re.findall(r'\b%s="[^"]*"' % k, head))
    if n != 1: raise SystemExit("atrybut %s x%d" % (k, n))
    return re.sub(r'\b%s="[^"]*"' % k, '%s="%s"' % (k, v), head)

new_skins = []
for sk in skins_of(ww_body):
    head, body = sk.group(1), sk.group(2)
    flat = re.sub(r"\s+", " ", head)
    gender, mat = attr(flat, "gender"), attr(flat, "mesh_maturity_type")
    tag = "skin %s/%s" % (gender, mat)
    # 1. meskie adult/teenager: siatki + tekstury twarzy + brwi z ludzkiego
    if gender == "0" and mat in ("adult", "teenager"):
        hs = find_skin(human, "0", mat)
        if hs is None: raise SystemExit("brak ludzkiego skinu " + mat)
        hh = re.sub(r"\s+", " ", hs.group(1))
        for k in ("body_meta_mesh", "face_meta_mesh", "hands_mesh", "legs_mesh"):
            old = attr(head, k); new = attr(hh, k)
            if old != new:
                head = set_attr(head, k, new); changes.append("%s: %s %s -> %s" % (tag, k, old, new))
        for blk in ("face_textures", "eyebrow_meshes"):
            hb = sub_block(hs.group(2), blk)
            if hb is None: raise SystemExit("brak bloku %s w ludzkim %s" % (blk, mat))
            wb = sub_block(body, blk)
            if wb is not None:
                body = body[:wb.start()] + hb.group(0) + body[wb.end():]
                changes.append("%s: blok %s = ludzki" % (tag, blk))
            else:
                vt = sub_block(body, "voice_types")
                pos = vt.start() if vt else len(body)
                body = body[:pos] + hb.group(0) + eol + "\t\t\t" + body[pos:]
                changes.append("%s: blok %s DODANY (ludzki)" % (tag, blk))
    # 2. palety dla kazdego skinu rasy - WSZYSTKIE wystapienia bloku (takze te
    #    dwupunktowe, ciemne, zagniezdzone w <face_textures>/<face_texture> - inaczej
    #    lord z taka tekstura twarzy dostalby ciemne wlosy i oczy)
    for blk, rgb in (("skin_color_gradient_points", SKIN_RGB), ("hair_color_gradient_points", HAIR_RGB), ("eye_color_gradient_points", EYE_RGB)):
        cnt = [0, 0]
        def repl(m, blk=blk, rgb=rgb, cnt=cnt):
            n = len(re.findall(r'point="', m.group(0))); cnt[0] += 1; cnt[1] += n
            return gradient(blk, n, rgb)
        body = re.sub(r'<%s\b[^>]*>.*?</%s>' % (blk, blk), repl, body, flags=re.S)
        if cnt[0] == 0: raise SystemExit("brak bloku %s w %s" % (blk, tag))
        changes.append("%s: %s -> paleta Innego (%d blokow, %d pkt)" % (tag, blk, cnt[0], cnt[1]))
    new_skins.append((sk.start(), sk.end(), "<skin" + head + ">" + body + "</skin>"))

for start, end, rep in reversed(new_skins):
    ww_body = ww_body[:start] + rep + ww_body[end:]
text = text[:ww.start()] + ww.group(1) + ww_body + ww.group(3) + text[ww.end():]

xml.dom.minidom.parseString(text.encode("utf-8"))   # musi sie parsowac
open(OUT, "wb").write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))
print("OK -> %s | zmian: %d" % (OUT, len(changes)))
for c in changes: print("  " + c)
