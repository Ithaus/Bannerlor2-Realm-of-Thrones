# Nagranie lektora EN glosem Davida (David - Deep and Engaging Storyteller), model eleven_multilingual_v2 - Jeff 21.10: "David v2 zostaje, tylko po angielsku".
# Klucz ElevenLabs z %USERPROFILE%\.elevenlabs\key.txt (nigdy nie wypisywany). Teksty: scenariusz_en.py (35 okien ROT) i ewentualnie scenariusz_prolog.py.
# Uzycie: python -I nagraj_david.py <katalog_wyjscia> [rot|prolog|wszystko] [--sprawdz]
#   --sprawdz: tylko liczy znaki i porownuje tytuly z teksty.json, bez nagrywania.
# Wynik: <klucz>.wav (24 kHz mono 16 bit) + arm_lektor.tsv (klucz, tytul okna z ekranu, plik).
import json, os, sys, struct, time, runpy, urllib.request, urllib.error

HERE = os.path.dirname(os.path.abspath(__file__))
KEYF = os.path.join(os.environ.get('USERPROFILE', ''), '.elevenlabs', 'key.txt')
VOICE = 'BNgbHR0DNeZixGQVzloa'
MODEL = 'eleven_multilingual_v2'
SETTINGS = {'stability': 0.5, 'similarity_boost': 0.78, 'style': 0.15, 'use_speaker_boost': True, 'speed': 0.95}


def key():
    k = open(KEYF, encoding='utf-8').read().strip()
    if not k:
        raise SystemExit('pusty plik klucza')
    return k


def wav(path, pcm, rate=24000):
    with open(path, 'wb') as f:
        f.write(b'RIFF' + struct.pack('<I', 36 + len(pcm)) + b'WAVEfmt ' + struct.pack('<IHHIIHH', 16, 1, 1, rate, rate * 2, 2, 16) + b'data' + struct.pack('<I', len(pcm)) + pcm)


def tts(text):
    body = json.dumps({'text': text, 'model_id': MODEL, 'voice_settings': SETTINGS}).encode('utf-8')
    r = urllib.request.Request('https://api.elevenlabs.io/v1/text-to-speech/%s?output_format=pcm_24000' % VOICE, data=body, method='POST')
    r.add_header('xi-api-key', key()); r.add_header('Content-Type', 'application/json'); r.add_header('Accept', 'audio/*')
    for proba in range(3):
        try:
            return urllib.request.urlopen(r, timeout=240).read()
        except urllib.error.HTTPError as e:
            msg = e.read()[:300].decode('utf-8', 'replace')
            if e.code in (429, 500, 502, 503) and proba < 2:
                time.sleep(5 * (proba + 1)); continue
            raise SystemExit('HTTP %d: %s' % (e.code, msg))


def scripts(which):
    out = {}
    if which in ('rot', 'wszystko'):
        out.update(runpy.run_path(os.path.join(HERE, 'scenariusz_en.py'))['S'])
    if which in ('prolog', 'wszystko') and os.path.exists(os.path.join(HERE, 'scenariusz_prolog.py')):
        out.update(runpy.run_path(os.path.join(HERE, 'scenariusz_prolog.py'))['S'])
    return out


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__ or 'podaj katalog wyjscia')
    outdir = sys.argv[1]
    which = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].startswith('--') else 'rot'
    check = '--sprawdz' in sys.argv
    S = scripts(which)
    titles = {'%s_%d' % (e['cls'], e['n']): e['title'] for e in json.load(open(os.path.join(HERE, 'teksty.json'), encoding='utf-8'))}
    bad = [k for k, (t, _) in S.items() if k in titles and titles[k] != t]
    missing = [k for k in titles if k not in S] if which in ('rot', 'wszystko') else []
    print('okien %d, znakow %d, zle tytuly %s, brak %s' % (len(S), sum(len(x) for _, x in S.values()), bad, missing))
    if check or bad:
        return
    os.makedirs(outdir, exist_ok=True)
    rows = []
    for k in sorted(S):
        title, text = S[k]
        p = os.path.join(outdir, k + '.wav')
        if not (os.path.exists(p) and os.path.getsize(p) > 1000):
            pcm = tts(text)
            wav(p, pcm)
            print('%s: %d znakow, %.1f s' % (k, len(text), len(pcm) / 48000.0))
            time.sleep(0.4)
        rows.append('%s\t%s\t%s.wav' % (k, title, k))
    with open(os.path.join(outdir, 'arm_lektor.tsv'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('# klucz\ttytul okna (EventPanel.EventTitle)\tplik\n' + '\n'.join(rows) + '\n')
    json.dump({'voice': 'David - Deep and Engaging Storyteller', 'voice_id': VOICE, 'model': MODEL, 'settings': SETTINGS},
              open(os.path.join(outdir, 'glos.json'), 'w', encoding='utf-8'), indent=1)
    print('gotowe:', len(rows))


if __name__ == '__main__':
    main()
