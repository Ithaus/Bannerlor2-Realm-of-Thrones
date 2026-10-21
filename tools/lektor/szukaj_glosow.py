# Szuka w bibliotece glosow ElevenLabs klimatycznych narratorow (Jeff: George "nie ma klimatu") i nagrywa krotkie probki.
# Klucz z %USERPROFILE%\.elevenlabs\key.txt - nie wypisywany.
# Uzycie: python -I szukaj_glosow.py szukaj   |   python -I szukaj_glosow.py probki <public_owner_id:voice_id:nazwa> ...
import json, os, sys, struct, urllib.request, urllib.error, urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'probki')
KEYF = os.path.join(os.environ.get('USERPROFILE', ''), '.elevenlabs', 'key.txt')
API = 'https://api.elevenlabs.io/v1'
LINE = ('Ned Stark... is dead. <break time="1.0s" /> Executed for treason, before the Great Sept of Baelor. <break time="1.4s" /> '
        "Now the lion and the wolf are at each other's throats... <break time=\"1.0s\" /> and the Seven Kingdoms bleed. <break time=\"1.6s\" /> "
        'Winter... is coming.')
SETTINGS = {'stability': 0.35, 'similarity_boost': 0.8, 'style': 0.55, 'use_speaker_boost': True, 'speed': 0.88}


def key():
    return open(KEYF, encoding='utf-8').read().strip()


def req(path, body=None, accept='application/json'):
    data = json.dumps(body).encode('utf-8') if body is not None else None
    r = urllib.request.Request(API + path, data=data, method='POST' if data is not None else 'GET')
    r.add_header('xi-api-key', key())
    r.add_header('Accept', accept)
    if data is not None:
        r.add_header('Content-Type', 'application/json')
    try:
        with urllib.request.urlopen(r, timeout=120) as resp:
            return resp.read()
    except urllib.error.HTTPError as e:
        raise RuntimeError('HTTP %d: %s' % (e.code, e.read()[:300].decode('utf-8', 'replace')))


def szukaj():
    seen = {}
    for q in ['epic fantasy narrator', 'medieval narrator', 'old storyteller', 'deep gravelly narrator', 'trailer narrator', 'dark fantasy', 'king', 'chronicle']:
        try:
            d = json.loads(req('/shared-voices?' + urllib.parse.urlencode({'search': q, 'gender': 'male', 'language': 'en', 'page_size': 12, 'sort': 'usage_character_count_1y'})))
        except RuntimeError as e:
            print('blad', q, e)
            continue
        for v in d.get('voices', []):
            vid = v.get('voice_id')
            if vid in seen:
                continue
            seen[vid] = 1
            print('%s:%s:%s | %s | %s | %s | %s | uzyc %s' % (v.get('public_owner_id'), vid, v.get('name', '').replace(':', ' '), v.get('age'), v.get('accent'),
                  v.get('use_case'), (v.get('description') or '')[:90].replace('\n', ' '), v.get('cloned_by_count')))


def wav(path, pcm, rate=24000):
    with open(path, 'wb') as f:
        f.write(b'RIFF' + struct.pack('<I', 36 + len(pcm)) + b'WAVEfmt ' + struct.pack('<IHHIIHH', 16, 1, 1, rate, rate * 2, 2, 16) + b'data' + struct.pack('<I', len(pcm)) + pcm)


def probki(specs):
    os.makedirs(OUT, exist_ok=True)
    for s in specs:
        owner, vid, name = s.split(':', 2)
        if owner and owner != '-':
            try:   # glos z biblioteki trzeba dodac do konta, zeby nim mowic
                req('/voices/add/%s/%s' % (owner, vid), {'new_name': name[:40]})
            except RuntimeError as e:
                if 'already' not in str(e).lower():
                    print('dodanie', name, e)
        try:
            pcm = req('/text-to-speech/%s?output_format=pcm_24000' % vid, {'text': LINE, 'model_id': 'eleven_multilingual_v2', 'voice_settings': SETTINGS}, accept='audio/*')
        except RuntimeError as e:
            print('tts', name, e)
            continue
        p = os.path.join(OUT, 'probka_%s.wav' % ''.join(c for c in name if c.isalnum())[:30])
        wav(p, pcm)
        print('ok', name, p, '%.1f s' % (len(pcm) / 48000.0))


if __name__ == '__main__':
    if sys.argv[1] == 'szukaj':
        szukaj()
    else:
        probki(sys.argv[2:])
