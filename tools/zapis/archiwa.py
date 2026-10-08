# Sprawdza WSZYSTKIE archiwa zapisu Bannerlorda (naglowek, napisy, obiekty, listy) - tylko odczyt.
import sys, struct, zlib, json, collections

def r3(b, i):
    if b[i] == 255 and b[i+1] == 255 and b[i+2] == 255:
        return -1
    return b[i] | (b[i+1] << 8) | (b[i+2] << 16)

def archive(b, keep=False):
    """Zwraca (ok, opis, wpisy[(folder,id,ext,dane)] jesli keep)."""
    i = 0
    nf = struct.unpack_from('<i', b, i)[0]; i += 4
    i += 10 * nf
    ne = struct.unpack_from('<i', b, i)[0]; i += 4
    out = []
    for k in range(ne):
        if i + 9 > len(b):
            return False, 'koniec danych przy wpisie %d/%d' % (k, ne), out
        fid = r3(b, i); eid = r3(b, i+3); ext = b[i+6]
        ln = struct.unpack_from('<h', b, i+7)[0]
        i += 9
        if ln < 0 or i + ln > len(b):
            return False, 'zly wpis %d/%d: folder %d id %d ext %d dlugosc %d (poz %d z %d)' % (k, ne, fid, eid, ext, ln, i-9, len(b)), out
        if keep:
            out.append((fid, eid, ext, b[i:i+ln]))
        i += ln
    if i != len(b):
        return False, 'po %d wpisach zostalo %d bajtow (poz %d z %d) - ktorys wpis byl dluzszy niz zapisana dlugosc' % (ne, len(b) - i, i, len(b)), out
    return True, 'ok (%d wpisow)' % ne, out

def main(path):
    raw = open(path, 'rb').read()
    n = struct.unpack_from('<i', raw, 0)[0]
    data = zlib.decompress(raw[4+n:], -15)
    p = 0
    hl = struct.unpack_from('<i', data, p)[0]; p += 4
    hdr = data[p:p+hl]; p += hl
    no = struct.unpack_from('<i', data, p)[0]; p += 4
    objs = []
    for k in range(no):
        l = struct.unpack_from('<i', data, p)[0]; p += 4
        objs.append(data[p:p+l]); p += l
    nc = struct.unpack_from('<i', data, p)[0]; p += 4
    cons = []
    for k in range(nc):
        l = struct.unpack_from('<i', data, p)[0]; p += 4
        cons.append(data[p:p+l]); p += l
    sl = struct.unpack_from('<i', data, p)[0]; p += 4
    strs = data[p:p+sl]; p += sl
    print('==', path.split('/')[-1], '| obiektow', no, '| list', nc, '| napisy', sl, 'B | koniec', p, 'z', len(data))
    ok, d, _ = archive(hdr); print('  naglowek:', d)
    ok, d, ent = archive(strs, keep=True); print('  napisy:', d)
    if ent:
        big = sorted(ent, key=lambda e: -len(e[3]))[:8]
        for fid, eid, ext, dat in big:
            txt = dat.decode('utf-8', 'replace')
            print('    najdluzszy wpis napisu: %d B | id %d | poczatek: %r' % (len(dat), eid, txt[:160]))
    bad = 0
    for k, b in enumerate(objs):
        ok, d, _ = archive(b)
        if not ok:
            bad += 1
            if bad <= 5: print('  obiekt %d: %s (rozmiar %d)' % (k, d, len(b)))
    print('  obiekty zle:', bad)
    bad = 0
    for k, b in enumerate(cons):
        ok, d, _ = archive(b)
        if not ok:
            bad += 1
            if bad <= 5: print('  lista %d: %s (rozmiar %d)' % (k, d, len(b)))
    print('  listy zle:', bad)

for a in sys.argv[1:]:
    try:
        main(a)
    except Exception as e:
        print(a, 'BLAD', type(e).__name__, e)
