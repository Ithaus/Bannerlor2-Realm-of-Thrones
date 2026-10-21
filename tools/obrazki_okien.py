# m103-sceny: obrazki okien opowiesci - z assets/ do Armoury/Images (potem wgranie: bieg-bitwa.ps1 / wgraj-v5.ps1 kopiuja Armoury\Images rekursywnie).
#
#   assets/prolog/prolog_NN.png|jpg|jpeg|webp   -> Armoury/Images/Prolog/prolog_NN.png   (NN = 01..23, sceny prologu 298)
#   assets/rot/rot_<KlasaROT>_<n>.png|...       -> Armoury/Images/ROT/rot_<KlasaROT>_<n>.png   (okna wydarzen ROT, np. rot_NedEvent_0)
#   assets/kronika/kronika_<id>.png|...         -> Armoury/Images/Kronika/kronika_<id>.png     (Kronika Westeros - pozniej)
#
# Kazdy plik: PIL verify (uszkodzony / wyzerowany - pominiety z linia BLAD), kadrowanie do 16:9 (srodek), zmniejszenie do 1024x576 (LANCZOS; ramka okna ROT
# ma dokladnie 1024x576, obrazki ROT tez), zapis PNG (albo JPG przy --jpg), ponowne otwarcie i ta sama walidacja bajtow co w grze (StoryPictures.Validate:
# bloki PNG z suma CRC, IEND; JPG SOI/EOI/SOF; wymiary <= 4096; nie same zera), md5 na wyjsciu. Nazwy spoza wzorow - pominiete z linia UWAGA.
# Gra sprawdza plik przy kazdym pokazaniu okna - nowy obrazek dziala bez przebudowy DLL (po skopiowaniu do Modules\Armoury\Images).
#
# Uzycie (z korzenia repo):  python -I tools/obrazki_okien.py [--src assets] [--out Armoury/Images] [--jpg] [--sprawdz]
#   --sprawdz : tylko sprawdza pliki w --out (walidacja jak w grze + md5), nic nie zapisuje.
# Kod wyjscia: 0 wszystko dobre, 1 byl BLAD, 2 brak PIL / zle argumenty.
import argparse, hashlib, io, os, re, struct, sys, zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
W, H = 1024, 576
MAX_SIDE = 4096
IN_EXT = ('.png', '.jpg', '.jpeg', '.webp')

# 35 okien wydarzen ROT 8.1.8 (jak tabela w Armoury/src/RotWindowPictures.cs)
ROT_WINDOWS = {
    'BlackwaterEvent_0', 'BlackwaterEvent_1', 'AegonInvasionEvent_0', 'BlackwaterSiegeEvent_0', 'BlackwaterSiegeNotificationEvent_0',
    'BastardsSiegeNotificationEvent_0', 'BastardsEvent_0', 'BastardsEvent_1', 'BastardsSiegeEvent_0', 'HarrenhalEvent_0', 'HarrenhalEvent_1',
    'HarrenhalSiegeEvent_0', 'HarrenhalSiegeNotificationEvent_0', 'JonEvent_0', 'RamsayEvent_0', 'WallEvent_0', 'WallEvent_1', 'WallSiegeEvent_0',
    'WallSiegeNotificationEvent_0', 'KingsLandingEvent_0', 'FistEvent_0', 'NightKingDeathEvent_0', 'JeorMutinyEvent_0', 'RiverlandsDeclareWarEvent_0',
    'DanyInvasionEvent_0', 'JoffreyEvent_0', 'EastwatchEvent_0', 'NedEvent_0', 'RickardKarstarkExecutionEvent_0', 'RooseDeathEvent_0',
    'RedWeddingEvent_0', 'RedWeddingEvent_1', 'RenlyDeathEvent_0', 'RenlyDeathEvent_1', 'StannisDeathEvent_0',
}

KINDS = [
    # (podkatalog assets, podkatalog Images, wzor nazwy, opis)
    ('prolog', 'Prolog', re.compile(r'^prolog_(0[1-9]|1[0-9]|2[0-3])$'), 'prolog_01..prolog_23'),
    ('rot', 'ROT', re.compile(r'^rot_([A-Za-z]+)_(\d)$'), 'rot_<KlasaROT>_<n>'),
    ('kronika', 'Kronika', re.compile(r'^kronika_[a-z0-9_]{1,60}$'), 'kronika_<id> (male litery, cyfry, _)'),
]


def md5(p):
    h = hashlib.md5()
    with open(p, 'rb') as f:
        for b in iter(lambda: f.read(1 << 20), b''):
            h.update(b)
    return h.hexdigest()


def validate(b):
    """Ta sama walidacja co StoryPictures.Validate w grze. None = dobry plik, inaczej powod."""
    if not b:
        return 'pusty plik'
    if not any(b[i] for i in range(0, len(b), 509)) and b[-1] == 0:
        return 'same zera (plik wyzerowany na dysku)'
    if b[:8] == b'\x89PNG\r\n\x1a\n':
        pos, ihdr, idat, iend = 8, False, False, False
        while pos + 12 <= len(b):
            ln = struct.unpack('>I', b[pos:pos + 4])[0]
            if ln > 0x7FFFFFFF or pos + 12 + ln > len(b):
                return 'blok PNG poza plikiem przy bajcie %d' % pos
            typ = b[pos + 4:pos + 8]
            if not all(65 <= c <= 90 or 97 <= c <= 122 for c in typ):
                return 'zly typ bloku PNG przy bajcie %d' % pos
            crc = struct.unpack('>I', b[pos + 8 + ln:pos + 12 + ln])[0]
            if zlib.crc32(b[pos + 4:pos + 8 + ln]) & 0xFFFFFFFF != crc:
                return 'zla suma CRC bloku %s przy bajcie %d' % (typ.decode('ascii'), pos)
            if not ihdr:
                if typ != b'IHDR' or ln < 13:
                    return 'pierwszy blok PNG to nie IHDR'
                ihdr = True
                w, h = struct.unpack('>II', b[pos + 8:pos + 16])
                if not (0 < w <= MAX_SIDE and 0 < h <= MAX_SIDE):
                    return 'wymiary %dx%d' % (w, h)
            elif typ == b'IDAT':
                idat = True
            elif typ == b'IEND':
                iend = True
                break
            pos += 12 + ln
        if not idat:
            return 'brak IDAT'
        if not iend:
            return 'brak IEND (plik uciety)'
        return None
    if b[:3] == b'\xff\xd8\xff':
        if b[-2:] != b'\xff\xd9':
            return 'brak EOI (plik uciety albo wyzerowany)'
        pos, w = 2, 0
        while pos + 4 <= len(b):
            if b[pos] != 0xFF:
                return 'zly znacznik JPG przy bajcie %d' % pos
            m = b[pos + 1]
            if m == 0xFF:
                pos += 1
                continue
            if m in (0xD8, 0x01) or 0xD0 <= m <= 0xD7:
                pos += 2
                continue
            ln = (b[pos + 2] << 8) | b[pos + 3]
            if ln < 2 or pos + 2 + ln > len(b):
                return 'segment JPG poza plikiem przy bajcie %d' % pos
            if 0xC0 <= m <= 0xCF and m not in (0xC4, 0xC8, 0xCC):
                h, w = struct.unpack('>HH', b[pos + 5:pos + 9])
                if not (0 < w <= MAX_SIDE and 0 < h <= MAX_SIDE):
                    return 'wymiary %dx%d' % (w, h)
            if m == 0xDA:
                return None if w > 0 else 'brak SOF przed danymi'
            pos += 2 + ln
        return 'brak SOS'
    return 'nie PNG ani JPG'


def fit(img):
    """Kadrowanie srodka do 16:9 i zmniejszenie (albo powiekszenie) do 1024x576."""
    from PIL import Image
    img = img.convert('RGB')
    w, h = img.size
    target = W / H
    if abs(w / h - target) > 0.002:
        if w / h > target:
            nw = int(round(h * target))
            x = (w - nw) // 2
            img = img.crop((x, 0, x + nw, h))
        else:
            nh = int(round(w / target))
            y = (h - nh) // 2
            img = img.crop((0, y, w, y + nh))
    if img.size != (W, H):
        img = img.resize((W, H), Image.LANCZOS)
    return img


def check_only(out):
    bad = 0
    for _, sub, rx, _desc in KINDS:
        d = os.path.join(out, sub)
        if not os.path.isdir(d):
            continue
        for n in sorted(os.listdir(d)):
            p = os.path.join(d, n)
            stem, ext = os.path.splitext(n)
            if ext.lower() not in ('.png', '.jpg', '.jpeg') or not os.path.isfile(p):
                continue
            why = validate(open(p, 'rb').read())
            print('%-6s %-40s %s' % ('OK' if why is None else 'BLAD', sub + '/' + n, md5(p) if why is None else why))
            bad += why is not None
    return bad


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', default=os.path.join(ROOT, 'assets'))
    ap.add_argument('--out', default=os.path.join(ROOT, 'Armoury', 'Images'))
    ap.add_argument('--jpg', action='store_true', help='zapis JPG (q=92) zamiast PNG')
    ap.add_argument('--sprawdz', action='store_true', help='tylko sprawdz pliki w --out')
    a = ap.parse_args()
    if a.sprawdz:
        bad = check_only(a.out)
        print('BLEDOW: %d' % bad)
        return 1 if bad else 0
    try:
        from PIL import Image
    except ImportError:
        print('BLAD: brak PIL (pip install pillow)')
        return 2
    done, bad, skipped = 0, 0, 0
    for asub, sub, rx, desc in KINDS:
        sd = os.path.join(a.src, asub)
        if not os.path.isdir(sd):
            continue
        od = os.path.join(a.out, sub)
        for n in sorted(os.listdir(sd)):
            p = os.path.join(sd, n)
            stem, ext = os.path.splitext(n)
            if not os.path.isfile(p) or ext.lower() not in IN_EXT:
                continue
            m = rx.match(stem)
            if not m:
                print('UWAGA  %-40s nazwa spoza wzoru %s - pominiety' % (asub + '/' + n, desc))
                skipped += 1
                continue
            if asub == 'rot' and stem[4:] not in ROT_WINDOWS:
                print('UWAGA  %-40s okno %s nie jest w tabeli 35 okien ROT 8.1.8 - zapisany, ale gra go nie wezmie' % (asub + '/' + n, stem[4:]))
            raw = open(p, 'rb').read()
            try:
                Image.open(io.BytesIO(raw)).verify()
                img = Image.open(io.BytesIO(raw))
                img.load()
                src_size = img.size
                img = fit(img)
            except Exception as e:
                print('BLAD   %-40s plik zrodlowy uszkodzony (%s: %s) - pominiety' % (asub + '/' + n, type(e).__name__, e))
                bad += 1
                continue
            os.makedirs(od, exist_ok=True)
            out_ext = '.jpg' if a.jpg else '.png'
            dst = os.path.join(od, stem + out_ext)
            for other in ('.png', '.jpg', '.jpeg'):
                o = os.path.join(od, stem + other)
                if other != out_ext and os.path.exists(o):
                    os.remove(o)   # gra bierze pierwszy z .png/.jpg/.jpeg - jeden plik na okno
            buf = io.BytesIO()
            if a.jpg:
                img.save(buf, 'JPEG', quality=92, optimize=True)
            else:
                img.save(buf, 'PNG', optimize=True)
            data = buf.getvalue()
            why = validate(data)
            if why is not None:
                print('BLAD   %-40s wynik nie przeszedl walidacji gry: %s' % (asub + '/' + n, why))
                bad += 1
                continue
            tmp = dst + '.tmp'
            with open(tmp, 'wb') as f:
                f.write(data)
            os.replace(tmp, dst)
            back = open(dst, 'rb').read()
            if back != data:
                print('BLAD   %-40s plik po zapisie rozni sie od zapisanego (dysk?)' % (sub + '/' + stem + out_ext))
                bad += 1
                continue
            print('OK     %-40s %4dx%-4d -> %dx%d %7d B  md5 %s' % (sub + '/' + stem + out_ext, src_size[0], src_size[1], W, H, len(data), md5(dst)))
            done += 1
    print('ZAPISANE %d, BLEDY %d, POMINIETE %d | wyjscie: %s' % (done, bad, skipped, a.out))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
