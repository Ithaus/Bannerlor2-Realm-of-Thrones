# Lektor wydarzen ROT (Jeff 21.10: "tylko ENG", "ciekawy glos, aby nie brzmial mlodziezowo").
# Klucz ElevenLabs czytany z pliku %USERPROFILE%\.elevenlabs\key.txt (zapisal go Jeff) - NIGDY nie wypisywany ani zapisywany gdzie indziej.
# Uzycie: python -I nagraj.py glosy | probka | wszystko   (wyniki w out/ obok skryptu: <klasa>_<n>.wav, 24 kHz mono 16 bit)
import json, os, sys, struct, urllib.request, urllib.error, time

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
KEYF = os.path.join(os.environ.get('USERPROFILE', ''), '.elevenlabs', 'key.txt')
API = 'https://api.elevenlabs.io/v1'
PREFER = ['George', 'Daniel', 'Bill', 'Brian']   # dojrzale glosy narratorow (brytyjski kronikarz najpierw)
MODEL = 'eleven_multilingual_v2'
FMT = 'pcm_24000'
SETTINGS = {'stability': 0.55, 'similarity_boost': 0.75, 'style': 0.25, 'use_speaker_boost': True, 'speed': 0.95}
# literowki ROT, zeby lektor ich nie czytal
FIX = {'Danaerys': 'Daenerys', 'assualt': 'assault', 'knick of time': 'nick of time', 'Crasters Keep': "Craster's Keep"}


def key():
    k = open(KEYF, encoding='utf-8').read().strip()
    if not k:
        raise SystemExit('pusty plik klucza')
    return k


def req(path, body=None, accept='application/json'):
    data = json.dumps(body).encode('utf-8') if body is not None else None
    r = urllib.request.Request(API + path, data=data, method='POST' if data else 'GET')
    r.add_header('xi-api-key', key())
    r.add_header('Accept', accept)
    if data:
        r.add_header('Content-Type', 'application/json')
    try:
        with urllib.request.urlopen(r, timeout=120) as resp:
            return resp.read()
    except urllib.error.HTTPError as e:
        raise SystemExit('HTTP %d: %s' % (e.code, e.read()[:300].decode('utf-8', 'replace')))


def voices():
    d = json.loads(req('/voices'))
    return [(v.get('name', ''), v.get('voice_id', ''), (v.get('labels') or {})) for v in d.get('voices', [])]


def pick():
    vs = voices()
    for want in PREFER:
        for n, i, lab in vs:
            if n.split(' ')[0].lower() == want.lower():
                return n, i, lab
    return vs[0] if vs else (None, None, None)


def text_of(e):
    parts = [e['title'].rstrip('.') + '.', e['upper'], e['lower']]
    t = '\n\n'.join(p.strip() for p in parts if p and p.strip())
    for a, b in FIX.items():
        t = t.replace(a, b)
    return t


def wav(path, pcm, rate=24000):
    with open(path, 'wb') as f:
        f.write(b'RIFF' + struct.pack('<I', 36 + len(pcm)) + b'WAVEfmt ' + struct.pack('<IHHIIHH', 16, 1, 1, rate, rate * 2, 2, 16) + b'data' + struct.pack('<I', len(pcm)) + pcm)


def tts(voice_id, text, path):
    pcm = req('/text-to-speech/%s?output_format=%s' % (voice_id, FMT), {'text': text, 'model_id': MODEL, 'voice_settings': SETTINGS}, accept='audio/*')
    wav(path, pcm)
    return len(pcm) / 48000.0


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else 'probka'
    os.makedirs(OUT, exist_ok=True)
    if mode == 'glosy':
        for n, i, lab in voices():
            print(n, '|', i, '|', json.dumps(lab, ensure_ascii=True))
        return
    if len(sys.argv) > 3:   # glos podany wprost (klucz bez prawa voices_read): nagraj.py probka <voice_id> <nazwa>
        name, vid, lab = sys.argv[3], sys.argv[2], {}
    else:
        name, vid, lab = pick()
    print('glos:', name, vid, json.dumps(lab, ensure_ascii=True))
    items = json.load(open(os.path.join(HERE, 'teksty.json'), encoding='utf-8'))
    if mode == 'probka':
        items = [e for e in items if e['cls'] == 'NedEvent']
    chars = 0
    for e in items:
        p = os.path.join(OUT, '%s_%d.wav' % (e['cls'], e['n']))
        if os.path.exists(p) and os.path.getsize(p) > 1000:
            continue
        t = text_of(e)
        sec = tts(vid, t, p)
        chars += len(t)
        print('%s_%d: %d znakow, %.1f s' % (e['cls'], e['n'], len(t), sec))
        time.sleep(0.5)
    json.dump({'voice': name, 'voice_id': vid, 'model': MODEL, 'format': FMT, 'settings': SETTINGS}, open(os.path.join(OUT, 'glos.json'), 'w', encoding='utf-8'), indent=1)
    print('razem znakow:', chars)


if __name__ == '__main__':
    main()
