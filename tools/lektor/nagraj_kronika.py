# Nagrania lektora (David v2) dla okien Kroniki Westeros: czyta Armoury/ModuleData/arm_kronika_kNN.tsv (sekcja #okno), bierze okna
# lektor=1, forma=okno, wersja != pelna; klucz nagrania kr_NNN_w[war]; tekst = tytul + gorny + dolny akapit (bez {=...}).
# Wynik: <katalog>/kr_NNN_w.ogg (Vorbis przez ffmpeg) + arm_lektor_kronika.tsv (klucz, tytul, plik, md5 tekstu, md5 pliku).
# Ponowne uruchomienie nagrywa tylko okna nowe albo ze zmienionym tekstem (md5 tekstu w manifescie).
# Klucz ElevenLabs z %USERPROFILE%\.elevenlabs\key.txt - nigdy nie wypisywany.
# Uzycie: python -I nagraj_kronika.py <katalog_ModuleData> <katalog_wyjscia> [--sprawdz] [--watki 4]
import concurrent.futures, glob, hashlib, json, os, re, struct, subprocess, sys, tempfile, time, urllib.error, urllib.request

KEYF = os.path.join(os.environ.get('USERPROFILE', ''), '.elevenlabs', 'key.txt')
VOICE = 'BNgbHR0DNeZixGQVzloa'
MODEL = 'eleven_multilingual_v2'
SETTINGS = {'stability': 0.5, 'similarity_boost': 0.78, 'style': 0.15, 'use_speaker_boost': True, 'speed': 0.95}
FFMPEG = r'C:\Users\GAME\tools\ffmpeg\ffmpeg-9.0.2-essentials_build\bin\ffmpeg.exe'
SAY = {"R'hllor": 'the Red God'}   # wymowa: lektor zle czyta te nazwy


def key():
    k = open(KEYF, encoding='utf-8').read().strip()
    if not k:
        raise SystemExit('pusty plik klucza')
    return k


def strip_id(s):
    return re.sub(r'^\{=[^}]*\}', '', s.strip())


def windows(mod):
    out = []
    for f in sorted(glob.glob(os.path.join(mod, 'arm_kronika_k[0-9][0-9].tsv'))):
        if f.endswith('k00.tsv'):
            continue
        sec, cols = None, None
        for line in open(f, encoding='utf-8'):
            line = line.rstrip('\n').rstrip('\r')
            if line.startswith('#okno'):
                sec, cols = 'okno', line.split('\t')[1:]
                continue
            if line.startswith('#'):
                if line[1:].split('\t')[0] in ('wybor', 'banda', 'para'):
                    sec = 'x'
                continue
            if not line or sec != 'okno':
                continue
            c = line.split('\t')
            row = dict(zip(cols, c))
            if row.get('lektor', '').strip() != '1' or row.get('forma', '').strip() != 'okno' or row.get('wersja', '').strip() == 'pelna':
                continue
            title, up, low = strip_id(row.get('tytul', '')), strip_id(row.get('gora', '')), strip_id(row.get('dol', ''))
            if '{' in title + up + low:
                continue   # zmienne - bez nagrania
            wid = row.get('id', '').strip()
            k = 'kr_' + wid.replace('.', '_') + row.get('war', '').strip()
            text = title.rstrip('.!?') + '.\n\n' + up + '\n\n' + low
            for a, b in SAY.items():
                text = text.replace(a, b)
            out.append((k, title, text, os.path.basename(f)))
    return out


def tts_pcm(text):
    body = json.dumps({'text': text, 'model_id': MODEL, 'voice_settings': SETTINGS}).encode('utf-8')
    for proba in range(4):
        r = urllib.request.Request('https://api.elevenlabs.io/v1/text-to-speech/%s?output_format=pcm_24000' % VOICE, data=body, method='POST')
        r.add_header('xi-api-key', key()); r.add_header('Content-Type', 'application/json'); r.add_header('Accept', 'audio/*')
        try:
            return urllib.request.urlopen(r, timeout=240).read()
        except urllib.error.HTTPError as e:
            msg = e.read()[:300].decode('utf-8', 'replace')
            if e.code in (429, 500, 502, 503) and proba < 3:
                time.sleep(6 * (proba + 1)); continue
            raise RuntimeError('HTTP %d: %s' % (e.code, msg))
        except Exception as e:
            if proba < 3:
                time.sleep(6 * (proba + 1)); continue
            raise


def to_ogg(pcm, path):
    fd, wavp = tempfile.mkstemp(suffix='.wav'); os.close(fd)
    with open(wavp, 'wb') as f:
        f.write(b'RIFF' + struct.pack('<I', 36 + len(pcm)) + b'WAVEfmt ' + struct.pack('<IHHIIHH', 16, 1, 1, 24000, 48000, 2, 16) + b'data' + struct.pack('<I', len(pcm)) + pcm)
    tmp = path + '.tmp.ogg'
    subprocess.run([FFMPEG, '-v', 'error', '-y', '-i', wavp, '-c:a', 'libvorbis', '-q:a', '5', tmp], check=True)
    os.replace(tmp, path)
    os.remove(wavp)


def md5s(b):
    return hashlib.md5(b).hexdigest()


def main():
    mod, outdir = sys.argv[1], sys.argv[2]
    check = '--sprawdz' in sys.argv
    threads = int(sys.argv[sys.argv.index('--watki') + 1]) if '--watki' in sys.argv else 4
    ws = windows(mod)
    print('okien do nagrania: %d, znakow %d' % (len(ws), sum(len(t) for _, _, t, _ in ws)))
    os.makedirs(outdir, exist_ok=True)
    man = os.path.join(outdir, 'arm_lektor_kronika.tsv')
    old = {}
    if os.path.exists(man):
        for l in open(man, encoding='utf-8'):
            c = l.rstrip('\n').split('\t')
            if len(c) >= 5 and not l.startswith('#'):
                old[c[0]] = c
    todo = [w for w in ws if not (w[0] in old and old[w[0]][3] == md5s(w[2].encode('utf-8')) and os.path.exists(os.path.join(outdir, w[0] + '.ogg')))]
    print('nowych albo zmienionych: %d, znakow %d' % (len(todo), sum(len(t) for _, _, t, _ in todo)))
    if check:
        return
    done = {}

    def job(w):
        k, title, text, src = w
        pcm = tts_pcm(text)
        p = os.path.join(outdir, k + '.ogg')
        to_ogg(pcm, p)
        return k, len(pcm) / 48000.0

    errs = 0
    with concurrent.futures.ThreadPoolExecutor(max_workers=threads) as ex:
        futs = {ex.submit(job, w): w for w in todo}
        for i, fu in enumerate(concurrent.futures.as_completed(futs), 1):
            w = futs[fu]
            try:
                k, sec = fu.result()
                print('%d/%d %s %.1f s' % (i, len(todo), k, sec), flush=True)
            except Exception as e:
                errs += 1
                print('%d/%d %s BLAD %s' % (i, len(todo), w[0], str(e)[:200]), flush=True)
    rows = []
    for k, title, text, src in ws:
        p = os.path.join(outdir, k + '.ogg')
        if os.path.exists(p):
            rows.append('%s\t%s\t%s.ogg\t%s\t%s\t%s' % (k, title, k, md5s(text.encode('utf-8')), md5s(open(p, 'rb').read()), src))
    with open(man, 'w', encoding='utf-8', newline='\n') as f:
        f.write('# klucz\ttytul okna\tplik\tmd5 tekstu\tmd5 pliku\tpaczka\n' + '\n'.join(rows) + '\n')
    print('gotowe: %d nagran w manifescie, bledow %d' % (len(rows), errs))


if __name__ == '__main__':
    main()
