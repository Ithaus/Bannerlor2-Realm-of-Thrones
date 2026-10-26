# m164-muzyka (Jeff 26.10 00:15: "dodaj jeszcze muzyke, aby jak jest bitwa leciala sciezka dzwiekowa wojenna nie tylko dzwieki z bitwy ale tez 10 utworow
# bitewnych"; po probce bit_04: "tak swietny klimat"): 10 utworow bitewnych z ElevenLabs Music (mp3 bit_NN_nazwa.mp3, NN 01..10) -> bit_NN_nazwa.ogg w 4 zestawach
# glosnosci lufs17/20/23/26 (suwak MCM BattleMusicLoudness 4..1, EventMusic.Bitwa.cs LufsOf). Na wzor bitwa_glosnosc.py (stare 6 utworow wydarzen):
#  - wejscie 2 s i wyjscie 4 s wypalane (afade) tylko, gdy plik ich nie ma: brzeg uznany za wyciszony, gdy szczyt pierwszych 0.25 s / ostatnich 0.5 s jest
#    co najmniej 30 dB pod szczytem calego pliku (utwor zaczyna sie z ciszy / wybrzmiewa do ciszy); decyzja w raporcie (kolumny wejscie / wyjscie);
#  - potem samo wzmocnienie volume=XdB do celu (cel - I pliku po wyciszeniach) - bez EQ, kompresji i limitera; libvorbis -q:a 4, 44.1 kHz stereo;
#  - gotowy ogg mierzony (ebur128 peak=true): I = cel +-0.5 LU i true peak <= -1.0 dBTP; za wysoki szczyt - wzmocnienie mniejsze o (TP + 1.1) dB,
#    najwyzej 3 proby, potem blad (kod 1).
# Wynik do katalogu POSREDNIEGO (argument): <wyjscie>/lufs<N>/bit_NN_nazwa.ogg i raport <wyjscie>/bitwa_v2_pomiar.tsv (z md5 zrodla i pliku). Nigdy prosto do gry:
# katalog wyjscia w instalacji Bannerlorda (sciezka z "Mount & Blade II Bannerlord") - odmowa. Do paczki kopiuje sie recznie do
# Armoury/ModuleData/Lektor/Muzyka/Bitwa/lufs<N>/ (kod gra tylko bit_*.ogg; stare muz_*.ogg w tych katalogach sa pomijane).
# Niepelny zestaw jest dozwolony (np. sama probka bit_04): przerabia, co jest, i wypisuje brakujace numery; kod 0, gdy wszystkie przerobione sa w warunkach.
# Raport TSV opisuje tylko pliki z tego przebiegu (niepelny zestaw do katalogu z poprzednim pelnym - raport nadpisany; wtedy osobny katalog wyjscia).
# Uzycie: python -I tools/muzyka/bitwa_v2.py <katalog_mp3> <katalog_wyjscia> [--prefiks bit|map]      (ffmpeg: zmienna FFMPEG albo sciezka ponizej)
# Paczka 165 (Jeff 26.10 ok. 18:30: "zrob mi jeszcze 10 utworow na mape kampanii w stylu gry o tron lub sredniowiecza minimum 2 minuty dlugosci a najlepiej taka
# muzyka ktora nie jest zbyt intensywna i nie rozprasza ale jako dobry podklad"): --prefiks map przerabia map_NN_nazwa.mp3 tymi samymi regulami na
# <wyjscie>/lufs<N>/map_NN_nazwa.ogg (do paczki: Armoury/ModuleData/Lektor/Muzyka/Mapa/lufs<N>/, EventMusic.MapaTlo.cs); wejscie 4 s i wyjscie 6 s wypalane
# tylko, gdy ich brak (zrodla ElevenLabs maja je z promptu); oczekiwana dlugosc 120-180 s; raport <wyjscie>/mapa_v2_pomiar.tsv. Bez --prefiks - bit jak dotad.
import hashlib, os, re, shutil, subprocess, sys

FF = os.environ.get('FFMPEG') or r'C:\Users\GAME\tools\ffmpeg\ffmpeg-9.0.2-essentials_build\bin\ffmpeg.exe'
LEVELS = (17, 20, 23, 26)          # -LUFS; suwak 4 = 17, 3 = 20, 2 = 23, 1 = 26 (EventMusic.Bitwa.cs LufsOf)
TOL_I = 0.5                        # LU
TP_MAX = -1.0                      # dBTP
TRIES = 3
FADE_IN, FADE_OUT = 2.0, 4.0       # s
EDGE_IN, EDGE_OUT = 0.25, 0.5      # s - okno brzegu do oceny, czy wyciszenie juz jest
EDGE_DB = 30.0                     # brzeg co najmniej tyle dB pod szczytem pliku = wyciszenie jest
DUR_OK = (120.0, 160.0)            # s - oczekiwana dlugosc (tools/sprawdz_logi.py m109muz MUZ-0); poza - UWAGA, bez bledu
PREFIX = 'bit'
TSV_NAME = 'bitwa_v2_pomiar.tsv'
# paczka 165: profile prefiksow - (wejscie s, wyjscie s, dlugosc min s, dlugosc max s, raport)
PROFILES = {
    'bit': (2.0, 4.0, 120.0, 160.0, 'bitwa_v2_pomiar.tsv'),
    'map': (4.0, 6.0, 120.0, 180.0, 'mapa_v2_pomiar.tsv'),   # tools/sprawdz_logi.py m165mapa MAP-0: dlugosci 120-180 s
}
NAME_RX = re.compile(r'^bit_(\d\d)_([a-z0-9_]+)\.mp3$')
EXPECTED = ('01', '02', '03', '04', '05', '06', '07', '08', '09', '10')


def use_profile(name):
    """Paczka 165: ustawia globalne wejscie / wyjscie / dlugosc / wzor nazwy / raport wedlug prefiksu (bit albo map)."""
    global PREFIX, TSV_NAME, FADE_IN, FADE_OUT, DUR_OK, NAME_RX
    if name not in PROFILES:
        raise SystemExit('nieznany prefiks %s (dozwolone: %s)' % (name, ', '.join(sorted(PROFILES))))
    fi, fo, dmin, dmax, tsv = PROFILES[name]
    PREFIX, TSV_NAME, FADE_IN, FADE_OUT, DUR_OK = name, tsv, fi, fo, (dmin, dmax)
    NAME_RX = re.compile(r'^' + name + r'_(\d\d)_([a-z0-9_]+)\.mp3$')


def run(args):
    return subprocess.run([FF, '-hide_banner', '-nostats'] + args, capture_output=True, text=True, encoding='utf-8', errors='replace')


def measure(p):
    """ebur128 peak=true -> (I LUFS, LRA LU, TP dBTP, dlugosc s)."""
    r = run(['-i', p, '-af', 'ebur128=peak=true', '-f', 'null', '-'])
    e = r.stderr
    if r.returncode != 0 or 'Summary:' not in e:
        raise SystemExit('pomiar nieudany: %s (kod %d)\n%s' % (p, r.returncode, e[-800:]))
    summ = e[e.rfind('Summary:'):]
    i = re.search(r'I:\s+(-?[\d.]+) LUFS', summ)
    lra = re.search(r'LRA:\s+(-?[\d.]+) LU', summ)
    tp = re.search(r'Peak:\s+(-?[\d.]+|-inf) dBFS', summ)
    dur = re.search(r'Duration: (\d+):(\d+):([\d.]+)', e)
    if not (i and lra and tp and dur):
        raise SystemExit('pomiar bez liczb: %s' % p)
    d = int(dur.group(1)) * 3600 + int(dur.group(2)) * 60 + float(dur.group(3))
    return float(i.group(1)), float(lra.group(1)), (-99.0 if tp.group(1) == '-inf' else float(tp.group(1))), d


def peak(p, start=None, length=None):
    """Szczyt probek (volumedetect max_volume, dBFS) calego pliku albo okna [start, start + length]."""
    af = 'volumedetect'
    if start is not None:
        af = 'atrim=start=%.3f:duration=%.3f,%s' % (max(0.0, start), length, af)
    r = run(['-i', p, '-af', af, '-f', 'null', '-'])
    m = re.search(r'max_volume:\s+(-?[\d.]+|-inf) dB', r.stderr)
    if r.returncode != 0 or not m:
        raise SystemExit('volumedetect nieudany: %s (kod %d)\n%s' % (p, r.returncode, r.stderr[-800:]))
    return -120.0 if m.group(1) == '-inf' else float(m.group(1))


def prepare(src, wav, dur):
    """Zrodlo -> wav float 44.1 kHz stereo z wypalonym wejsciem / wyjsciem, jesli ich brak. -> (opis wejscia, opis wyjscia)."""
    top = peak(src)
    head = peak(src, 0.0, EDGE_IN)
    tail = peak(src, dur - EDGE_OUT, EDGE_OUT)
    fin = head > top - EDGE_DB
    fout = tail > top - EDGE_DB
    af = []
    if fin:
        af.append('afade=t=in:st=0:d=%.1f' % FADE_IN)
    if fout:
        af.append('afade=t=out:st=%.3f:d=%.1f' % (max(0.0, dur - FADE_OUT), FADE_OUT))
    args = ['-loglevel', 'error', '-y', '-i', src, '-map', '0:a:0', '-map_metadata', '-1']
    if af:
        args += ['-af', ','.join(af)]
    args += ['-c:a', 'pcm_f32le', '-ar', '44100', '-ac', '2', wav]
    r = run(args)
    if r.returncode != 0:
        raise SystemExit('przygotowanie nieudane: %s (kod %d)\n%s' % (src, r.returncode, r.stderr[-800:]))
    din = ('wypalone %.0f s' % FADE_IN) if fin else 'jest'
    dout = ('wypalone %.0f s' % FADE_OUT) if fout else 'jest'
    return '%s (brzeg %.1f / szczyt %.1f dBFS)' % (din, head, top), '%s (brzeg %.1f / szczyt %.1f dBFS)' % (dout, tail, top)


def encode(src, dst, gain):
    tmp = dst + '.tmp.ogg'
    r = run(['-loglevel', 'error', '-y', '-i', src, '-map', '0:a:0', '-map_metadata', '-1',
             '-af', 'volume=%.2fdB' % gain, '-c:a', 'libvorbis', '-q:a', '4', '-ar', '44100', '-ac', '2', tmp])
    if r.returncode != 0:
        raise SystemExit('kodowanie nieudane: %s (kod %d)\n%s' % (dst, r.returncode, r.stderr[-800:]))
    os.replace(tmp, dst)


def md5(p):
    h = hashlib.md5()
    with open(p, 'rb') as f:
        for b in iter(lambda: f.read(1 << 20), b''):
            h.update(b)
    return h.hexdigest()


def main():
    args = sys.argv[1:]
    prefix = 'bit'
    if '--prefiks' in args:   # paczka 165: --prefiks map (utwory mapy kampanii)
        k = args.index('--prefiks')
        if k + 1 >= len(args):
            raise SystemExit('--prefiks bez wartosci (bit albo map)')
        prefix = args[k + 1]
        args = args[:k] + args[k + 2:]
    if len(args) != 2:
        raise SystemExit('uzycie: python -I bitwa_v2.py <katalog_mp3> <katalog_wyjscia> [--prefiks bit|map]')
    use_profile(prefix)
    src_dir, out = os.path.abspath(args[0]), os.path.abspath(args[1])
    if 'mount & blade ii bannerlord' in out.replace('/', '\\').lower():
        raise SystemExit('odmowa: katalog wyjscia w instalacji gry (%s) - tylko katalog posredni, do paczki kopiuje sie recznie' % out)
    if not os.path.isfile(FF):
        raise SystemExit('brak ffmpeg: ' + FF)
    if not os.path.isdir(src_dir):
        raise SystemExit('brak katalogu zrodel: ' + src_dir)
    srcs = {}
    for f in sorted(os.listdir(src_dir)):
        m = NAME_RX.match(f)
        if not m:
            continue
        if m.group(1) in srcs:
            raise SystemExit('dwa zrodla z numerem %s: %s i %s' % (m.group(1), srcs[m.group(1)], f))
        srcs[m.group(1)] = f
    if not srcs:
        raise SystemExit('brak plikow %s_NN_nazwa.mp3 w %s' % (PREFIX, src_dir))
    missing = [n for n in EXPECTED if n not in srcs]
    tmpd = os.path.join(out, '_tmp')
    os.makedirs(tmpd, exist_ok=True)
    rows = ['poziom\tplik\twzmocnienie_dB\tI_LUFS\tTP_dBTP\tLRA_LU\tdlugosc_s\twejscie\twyjscie\tzrodlo\tzrodlo_md5\tmd5']
    bad = 0
    warn = []
    try:
        for num in sorted(srcs):
            f = srcs[num]
            src = os.path.join(src_dir, f)
            base = f[:-4]
            i0, lra0, tp0, dur0 = measure(src)
            wav = os.path.join(tmpd, base + '.wav')
            fin, fout = prepare(src, wav, dur0)
            i1, lra1, tp1, dur1 = measure(wav)
            print('zrodlo %s: I %.1f LUFS, LRA %.1f, TP %.1f dBTP, %.1f s | wejscie %s | wyjscie %s | po wyciszeniach I %.1f LUFS' % (
                f, i0, lra0, tp0, dur0, fin, fout, i1))
            if not (DUR_OK[0] <= dur1 <= DUR_OK[1]):
                warn.append('%s %.1f s (poza %.0f-%.0f s)' % (f, dur1, DUR_OK[0], DUR_OK[1]))
            smd5 = md5(src)
            for lv in LEVELS:
                d = os.path.join(out, 'lufs%d' % lv)
                os.makedirs(d, exist_ok=True)
                dst = os.path.join(d, base + '.ogg')
                gain = -lv - i1
                ok = False
                for t in range(TRIES):
                    encode(wav, dst, gain)
                    i, lra, tp, dur = measure(dst)
                    ok = abs(i + lv) <= TOL_I and tp <= TP_MAX
                    print('  lufs%d %s.ogg: wzmocnienie %+.2f dB -> I %.1f LUFS, TP %.1f dBTP, LRA %.1f, %.2f s%s' % (
                        lv, base, gain, i, tp, lra, dur, '' if ok else ' - POZA (proba %d/%d)' % (t + 1, TRIES)))
                    if ok or tp <= TP_MAX or t == TRIES - 1:
                        break             # ostatnia proba: wzmocnienie w raporcie = to, z ktorym zakodowano plik
                    gain -= tp + 1.1      # szczyt za wysoko: mniej o nadwyzke ponad -1.1 dBTP
                if not ok:
                    bad += 1
                rows.append('%d\tlufs%d/%s.ogg\t%.2f\t%.1f\t%.1f\t%.1f\t%.2f\t%s\t%s\t%s\t%s\t%s' % (
                    lv, lv, base, gain, i, tp, lra, dur, fin.split(' (')[0], fout.split(' (')[0], f, smd5, md5(dst)))
    finally:
        shutil.rmtree(tmpd, ignore_errors=True)
    tsv = os.path.join(out, TSV_NAME)
    with open(tsv, 'w', encoding='utf-8', newline='') as fh:
        fh.write('\r\n'.join(rows) + '\r\n')
    print('raport: ' + tsv)
    for w in warn:
        print('UWAGA dlugosc: ' + w)
    if missing:
        print('ZESTAW NIEPELNY: brak numerow %s (przerobione %d z %d)' % (', '.join(missing), len(srcs), len(EXPECTED)))
    if bad:
        print('BLAD: plikow poza warunkami (I cel +-%.1f LU, TP <= %.1f dBTP): %d' % (TOL_I, TP_MAX, bad))
        return 1
    print('OK: %d plikow (%d utworow x %d poziomy) w %s' % (len(srcs) * len(LEVELS), len(srcs), len(LEVELS), out))
    return 0


sys.exit(main())
