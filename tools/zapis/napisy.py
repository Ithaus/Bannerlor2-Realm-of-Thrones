# Archiwum napisow zapisu: napisy dluzsze niz 32767 B (psuja wczytanie) - tylko odczyt.
import sys, struct, zlib
sys.stdout.reconfigure(encoding='utf-8', errors='backslashreplace')

def r3(b, i):
    if b[i] == 255 and b[i+1] == 255 and b[i+2] == 255:
        return -1
    return b[i] | (b[i+1] << 8) | (b[i+2] << 16)

def strings_of(path):
    raw = open(path, 'rb').read()
    n = struct.unpack_from('<i', raw, 0)[0]
    data = zlib.decompress(raw[4+n:], -15)
    p = 0
    hl = struct.unpack_from('<i', data, p)[0]; p += 4 + hl
    for _ in range(2):
        cnt = struct.unpack_from('<i', data, p)[0]; p += 4
        for k in range(cnt):
            l = struct.unpack_from('<i', data, p)[0]; p += 4 + l
    sl = struct.unpack_from('<i', data, p)[0]; p += 4
    return data[p:p+sl]

def main(path):
    b = strings_of(path)
    i = 0
    nf = struct.unpack_from('<i', b, i)[0]; i += 4 + 10 * nf
    ne = struct.unpack_from('<i', b, i)[0]; i += 4
    long_ = []
    total = 0
    for k in range(ne):
        eid = r3(b, i+3)
        ln = struct.unpack_from('<H', b, i+7)[0]   # bez znaku: tak naprawde zapisane
        i += 9
        # prawdziwa dlugosc: 4 + dlugosc tekstu z pierwszych 4 bajtow
        tl = struct.unpack_from('<i', b, i)[0]
        real = 4 + tl
        if real != ln and (real & 0xFFFF) == ln:
            long_.append((eid, real, b[i+4:i+4+min(tl, 400)].decode('utf-8', 'replace'), b[i+4+max(0, tl-200):i+4+tl].decode('utf-8', 'replace')))
        i += real
        total += 1
    print('==', path.split('/')[-1], '| napisow', ne, '| przeczytane', total, '| koniec', i, 'z', len(b), '| ZA DLUGIE (>32767 B):', len(long_))
    for eid, real, head, tail in long_:
        print('   id %d: %d B | poczatek: %s' % (eid, real, head[:300]))
        print('            koniec: %s' % tail[-150:])

for a in sys.argv[1:]:
    try:
        main(a)
    except Exception as e:
        print(a, 'BLAD', type(e).__name__, e)
