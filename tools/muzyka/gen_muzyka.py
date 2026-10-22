# Muzyka wydarzen (Jeff 22.10): utwory instrumentalne z ElevenLabs Music. Klucz z %USERPROFILE%\.elevenlabs\key.txt - nigdy nie wypisywany.
# Uzycie: python -I gen_muzyka.py <katalog_wyjscia> <nazwa> [<nazwa> ...]   (nazwy z PROMPTS; istniejacy plik mp3 - pominiety)
import json, os, sys, time, urllib.error, urllib.request

KEYF = os.path.join(os.environ.get('USERPROFILE', ''), '.elevenlabs', 'key.txt')
BASE = ('Instrumental only, no vocals, no choir words. Original dark medieval fantasy film score, quiet and restrained, meant to sit under a spoken narration: '
        'soft dynamics, no sudden hits, no loud drums, steady mood from start to end, gentle fade in at the start. ')
PROMPTS = {
    'zaloba': BASE + 'Mourning and loss: slow solo cello over low sustained strings, a distant soft bell, minor key, grief and remembrance, 60 bpm.',
    'wojna': BASE + 'War is coming: low pulsing strings, muted distant war drums very soft, horn swells in the distance, tension and foreboding, minor key, 80 bpm.',
    'intryga': BASE + 'Court intrigue and schemes: plucked lute and pizzicato strings, a lonely hammered dulcimer, low drone, secretive and uneasy, minor key, 90 bpm.',
    'polnoc': BASE + 'The cold North and the Wall: icy wind texture, solo fiddle and low drone, sparse harp notes, vast frozen lands, loneliness, minor key, 70 bpm.',
    'korona': BASE + 'Kings, dragons and crowns: noble low brass and warm strings, slow rising motif, majestic but quiet, a hint of danger, 75 bpm.',
    'plotka': BASE + 'Tavern rumours: soft medieval lute and recorder, light frame drum brushed very softly, calm and warm, background for gossip, 95 bpm.',
}


def key():
    k = open(KEYF, encoding='utf-8').read().strip()
    if not k:
        raise SystemExit('pusty plik klucza')
    return k


def sub(k):
    r = urllib.request.Request('https://api.elevenlabs.io/v1/user/subscription', headers={'xi-api-key': k})
    d = json.load(urllib.request.urlopen(r, timeout=30))
    return d.get('character_count'), d.get('character_limit')


def gen(k, prompt, ms, out):
    body = json.dumps({'prompt': prompt, 'music_length_ms': ms, 'model_id': 'music_v1', 'force_instrumental': True}).encode('utf-8')
    r = urllib.request.Request('https://api.elevenlabs.io/v1/music?output_format=mp3_44100_128', data=body, method='POST',
                               headers={'xi-api-key': k, 'Content-Type': 'application/json', 'Accept': 'audio/mpeg'})
    t0 = time.time()
    try:
        data = urllib.request.urlopen(r, timeout=600).read()
    except urllib.error.HTTPError as e:
        msg = e.read().decode('utf-8', 'replace')[:400]
        print('BLAD HTTP', e.code, msg)
        return False
    open(out, 'wb').write(data)
    print(os.path.basename(out), len(data), 'B', round(time.time() - t0, 1), 's')
    return True


def main():
    outdir, names = sys.argv[1], sys.argv[2:]
    ms = int(os.environ.get('MUZ_MS', '90000'))
    os.makedirs(outdir, exist_ok=True)
    k = key()
    c0 = sub(k)
    print('kredyty przed:', c0)
    for n in names:
        out = os.path.join(outdir, 'muz_' + n + '.mp3')
        if os.path.exists(out):
            print('jest', out)
            continue
        if not gen(k, PROMPTS[n], ms, out):
            break
        c1 = sub(k)
        print('  zuzyte kredyty:', c1[0] - c0[0], '| zostalo', c1[1] - c1[0])
        c0 = c1


main()
