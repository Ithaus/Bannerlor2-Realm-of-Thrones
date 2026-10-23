# m109-muzyka (Jeff 22.10: "muzyka w czasie bitwy jest strasznie slaba, zastap ja naszymi 6 utworami, ktore leca losowo"): wersje bitewne tych samych
# 6 utworow wydarzen. Zrodlo: oryginaly ElevenLabs Music (tools/muzyka/zrodla/muz_*.mp3, bez EQ pod glos lektora - pliki wydarzen maja -28 LUFS i 2.5 kHz -3 dB).
# Dla kazdego poziomu suwaka (-17, -20, -23, -26 LUFS; klucz MCM BattleMusicLoudness 4..1) samo wzmocnienie volume=XdB (cel - I zrodla) - bez EQ,
# bez kompresji i bez limitera; libvorbis 44.1 kHz stereo w jakosci plikow wydarzen (-q:a 4). Gotowy ogg mierzony (ebur128 peak=true):
# I = cel +-0.5 LU i true peak <= -1.0 dBTP; za wysoki szczyt - wzmocnienie mniejsze o (TP + 1.1) dB, najwyzej 3 proby, potem blad (kod != 0).
# Wynik: Armoury/ModuleData/Lektor/Muzyka/Bitwa/lufs<N>/muz_<nastroj>.ogg (24 pliki) i raport tools/muzyka/bitwa_pomiar.tsv (poza Lektor - wgraj go nie kopiuje).
# Uzycie: python -I tools/muzyka/bitwa_glosnosc.py   (ffmpeg: zmienna FFMPEG albo sciezka ponizej)
import hashlib, os, re, subprocess, sys

FF = os.environ.get('FFMPEG') or r'C:\Users\GAME\tools\ffmpeg\ffmpeg-9.0.2-essentials_build\bin\ffmpeg.exe'
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SRC = os.path.join(HERE, 'zrodla')
OUT = os.path.join(ROOT, 'Armoury', 'ModuleData', 'Lektor', 'Muzyka', 'Bitwa')
TSV = os.path.join(HERE, 'bitwa_pomiar.tsv')
NAMES = ('zaloba', 'wojna', 'intryga', 'polnoc', 'korona', 'plotka')
LEVELS = (17, 20, 23, 26)          # -LUFS; suwak 4 = 17, 3 = 20, 2 = 23, 1 = 26 (EventMusic.Bitwa.cs LufsOf)
TOL_I = 0.5                        # LU
TP_MAX = -1.0                      # dBTP
TRIES = 3


def measure(p):
    """ebur128 peak=true -> (I LUFS, LRA LU, TP dBTP, dlugosc s)."""
    r = subprocess.run([FF, '-hide_banner', '-nostats', '-i', p, '-af', 'ebur128=peak=true', '-f', 'null', '-'],
                       capture_output=True, text=True, encoding='utf-8', errors='replace')
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


def encode(src, dst, gain):
    tmp = dst + '.tmp.ogg'
    r = subprocess.run([FF, '-hide_banner', '-nostats', '-loglevel', 'error', '-y', '-i', src, '-map', '0:a:0', '-map_metadata', '-1',
                        '-af', 'volume=%.2fdB' % gain, '-c:a', 'libvorbis', '-q:a', '4', '-ar', '44100', '-ac', '2', tmp],
                       capture_output=True, text=True, encoding='utf-8', errors='replace')
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
    if not os.path.isfile(FF):
        raise SystemExit('brak ffmpeg: ' + FF)
    src_m = {}
    for n in NAMES:
        p = os.path.join(SRC, 'muz_' + n + '.mp3')
        if not os.path.isfile(p):
            raise SystemExit('brak zrodla ' + p)
        src_m[n] = measure(p)
        print('zrodlo muz_%s.mp3: I %.1f LUFS, LRA %.1f, TP %.1f dBTP, %.1f s' % ((n,) + src_m[n]))
    rows = ['poziom\tplik\twzmocnienie_dB\tI_LUFS\tTP_dBTP\tLRA_LU\tdlugosc_s\tmd5']
    bad = 0
    for lv in LEVELS:
        d = os.path.join(OUT, 'lufs%d' % lv)
        os.makedirs(d, exist_ok=True)
        for n in NAMES:
            src = os.path.join(SRC, 'muz_' + n + '.mp3')
            dst = os.path.join(d, 'muz_' + n + '.ogg')
            gain = -lv - src_m[n][0]
            ok = False
            for t in range(TRIES):
                encode(src, dst, gain)
                i, lra, tp, dur = measure(dst)
                ok = abs(i + lv) <= TOL_I and tp <= TP_MAX
                print('  lufs%d muz_%s.ogg: wzmocnienie %+.2f dB -> I %.1f LUFS, TP %.1f dBTP, LRA %.1f%s' % (
                    lv, n, gain, i, tp, lra, '' if ok else ' - POZA (proba %d/%d)' % (t + 1, TRIES)))
                if ok or tp <= TP_MAX:
                    break
                gain -= tp + 1.1      # szczyt za wysoko: mniej o nadwyzke ponad -1.1 dBTP
            if not ok:
                bad += 1
            rows.append('%d\t%s\t%.2f\t%.1f\t%.1f\t%.1f\t%.2f\t%s' % (lv, 'lufs%d/muz_%s.ogg' % (lv, n), gain, i, tp, lra, dur, md5(dst)))
    with open(TSV, 'w', encoding='utf-8', newline='') as f:
        f.write('\r\n'.join(rows) + '\r\n')
    print('raport: ' + TSV)
    if bad:
        print('BLAD: plikow poza warunkami (I cel +-%.1f LU, TP <= %.1f dBTP): %d' % (TOL_I, TP_MAX, bad))
        return 1
    print('OK: %d plikow w %s' % (len(LEVELS) * len(NAMES), OUT))
    return 0


sys.exit(main())
