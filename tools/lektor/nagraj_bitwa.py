# Nagrania dzwieku bitwy (P2a, noc 25.10): glosy ElevenLabs TTS (i - przez sfx_bitwa.py - Sound Effects) wedlug scenariusza
# tools/lektor/bitwa/klipy_bitwa.csv (generator gen_klipy_bitwa.py) i obsady tools/lektor/bitwa/glosy_bitwa.json.
# Obrobka ffmpeg: obciecie ciszy (glosy i SFX bez petli), highpass 90 Hz (glosy), mono 44.1 kHz, loudnorm 2 przebiegi do celu z kolumny lufs,
# true peak <= -1 dBTP (sprawdzane ebur128 jak tools/muzyka/bitwa_glosnosc.py), libvorbis -q:a 5; kontrola pliku (OggS, bloki zer - wzor Lektor.cs).
# Wynik: <out>/<kategoria>/<id>.ogg + <out>/manifest.tsv (TAB: id, kategoria, kultura/rod, plik, glos, dlugosc_s, tekst, potem kolumny dodatkowe)
#        + <out>/nieudane.tsv + <out>/koszt.log (znaki, generacje SFX, kredyty przed/po).
# Klucz ElevenLabs z %USERPROFILE%\.elevenlabs\key.txt - nigdy nie wypisywany ani zapisywany.
# KOSZT: domyslnie tryb --sprawdz (zadnego platnego wywolania). Platne wywolania TYLKO z --nagraj - uruchamiac dopiero po zgodzie Jeffa.
# Uzycie:
#   python -I tools/lektor/nagraj_bitwa.py --sprawdz [--konto]                 policz znaki/SFX/koszt, sprawdz obsade i ffmpeg (--konto: odczyt subskrypcji, bez kosztu)
#   python -I tools/lektor/nagraj_bitwa.py --nagraj --out <katalog> [--prio 1|2|all] [--kategoria motto,rozkaz] [--rola L_STARK] [--id a,b]
#                                          [--probka] [--nadpisz] [--watki 3] [--limit-znakow 25000] [--rezerwa 30000]
#   python -I tools/lektor/sfx_bitwa.py   --nagraj --out <katalog> [...]       to samo dla wierszy SFX (limit --limit-sfx 120, kontrola kosztu 1. generacji)
import concurrent.futures, csv, hashlib, json, os, re, struct, subprocess, sys, tempfile, threading, time, urllib.error, urllib.request, zlib

HERE = os.path.dirname(os.path.abspath(__file__))
BITWA = os.path.join(HERE, 'bitwa')
CSV = os.path.join(BITWA, 'klipy_bitwa.csv')
OBSADA = os.path.join(BITWA, 'glosy_bitwa.json')
KEYF = os.path.join(os.environ.get('USERPROFILE', ''), '.elevenlabs', 'key.txt')
API = 'https://api.elevenlabs.io/v1'
FF = os.environ.get('FFMPEG') or r'C:\Users\GAME\tools\ffmpeg\ffmpeg-9.0.2-essentials_build\bin\ffmpeg.exe'
MODELS = {'v3': 'eleven_v3', 'v2': 'eleven_multilingual_v2'}
SET_V2 = {'stability': 0.4, 'similarity_boost': 0.8, 'style': 0.45, 'use_speaker_boost': True, 'speed': 1.0}
SET_V3 = {'stability': 0.5}          # v3: stability tylko 0.0 / 0.5 / 1.0 (Natural)
SFX_MODEL = 'eleven_text_to_sound_v2'
SFX_PROMPT_INFLUENCE = 0.5
SFX_KREDYT_NA_S = 40.0               # ZALOZENIE (cennik niepotwierdzony) - mierzone na 1. generacji, stop gdy > SFX_KREDYT_MAX_S
SFX_KREDYT_MAX_S = 60.0
TP_MAX = -1.0
MAX_GLOS_S = 8.5                     # przemowy i okrzyki do 8 s (kategoria mission_voice_shout); dluzsze - ostrzezenie
COLS_MAN = ['id', 'kategoria', 'kultura/rod', 'plik', 'glos', 'dlugosc_s', 'tekst', 'rola', 'voice_id', 'model', 'lufs_cel', 'lufs_I', 'tp_dBTP',
            'loop', 'prio', 'zakres', 'md5_pliku', 'md5_zrodla', 'pomiar']
_lock = threading.Lock()
_key = None


def key():
    global _key
    if _key is None:
        k = open(KEYF, encoding='utf-8').read().strip()
        if not k:
            raise SystemExit('pusty plik klucza')
        _key = k
    return _key


def http(path, body=None, accept='application/json', method=None, timeout=240):
    """Wywolanie API z ponawianiem 429/5xx (jak nagraj_kronika.py). Zwraca bajty; HTTPError -> RuntimeError('HTTP kod: tresc')."""
    data = json.dumps(body).encode('utf-8') if body is not None else None
    for proba in range(4):
        r = urllib.request.Request(API + path, data=data, method=method or ('POST' if data is not None else 'GET'))
        r.add_header('xi-api-key', key())
        r.add_header('Accept', accept)
        if data is not None:
            r.add_header('Content-Type', 'application/json')
        try:
            with urllib.request.urlopen(r, timeout=timeout) as resp:
                return resp.read()
        except urllib.error.HTTPError as e:
            msg = e.read()[:400].decode('utf-8', 'replace')
            if e.code in (429, 500, 502, 503, 504) and proba < 3:
                time.sleep(6 * (proba + 1))
                continue
            raise RuntimeError('HTTP %d: %s' % (e.code, msg))
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            if proba < 3:
                time.sleep(6 * (proba + 1))
                continue
            raise RuntimeError('siec: %s' % e)


def konto():
    d = json.loads(http('/user/subscription'))
    return {'uzyte': d.get('character_count'), 'limit': d.get('character_limit'), 'tier': d.get('tier'),
            'mozna_przekroczyc': d.get('can_extend_character_limit'), 'reset_unix': d.get('next_character_count_reset_unix')}


def spoken(t):
    return re.sub(r'\[[^\]]*\]\s*', '', t).strip()


def md5b(b):
    return hashlib.md5(b).hexdigest()


def md5f(p):
    h = hashlib.md5()
    with open(p, 'rb') as f:
        for b in iter(lambda: f.read(1 << 20), b''):
            h.update(b)
    return h.hexdigest()


def load_rows(path):
    with open(path, encoding='utf-8', newline='') as f:
        rd = csv.DictReader(f, delimiter=';')
        rows = [dict(r) for r in rd]
    for r in rows:
        r['prio'] = int(r['prio'])
        r['lufs'] = float(r['lufs'])
        r['loop'] = r['loop'] == '1'
        r['sek'] = float(r['sek']) if r['sek'] else 0.0
    return rows


def plik_ok(p):
    """Kontrola jak Lektor.cs: naglowek OggS, rozmiar, brak blokow 4 KB samych zer (dysk C Jeffa zeruje pliki)."""
    try:
        b = open(p, 'rb').read()
    except OSError:
        return False
    if len(b) < 1000 or b[:4] != b'OggS':
        return False
    z = bytes(4096)
    for i in range(0, len(b) - 4096, 4096):
        if b[i:i + 4096] == z:
            return False
    return True


def ff(args, what):
    r = subprocess.run([FF, '-hide_banner', '-nostats'] + args, capture_output=True, text=True, encoding='utf-8', errors='replace')
    if r.returncode != 0:
        raise RuntimeError('ffmpeg %s (kod %d): %s' % (what, r.returncode, r.stderr[-400:]))
    return r.stderr


def measure(p):
    e = ff(['-i', p, '-af', 'ebur128=peak=true', '-f', 'null', '-'], 'pomiar')
    summ = e[e.rfind('Summary:'):]
    i = re.search(r'I:\s+(-?[\d.]+|-inf) LUFS', summ)
    tp = re.search(r'Peak:\s+(-?[\d.]+|-inf) dBFS', summ)
    dur = re.findall(r'time=(\d+):(\d+):([\d.]+)', e) or re.findall(r'Duration: (\d+):(\d+):([\d.]+)', e)
    fv = lambda m: -99.0 if (not m or m.group(1) == '-inf') else float(m.group(1))
    d = 0.0
    if dur:
        h, m, s = dur[-1]
        d = int(h) * 3600 + int(m) * 60 + float(s)
    return fv(i), fv(tp), d


def duration(p):
    e = subprocess.run([FF, '-hide_banner', '-i', p, '-f', 'null', '-'], capture_output=True, text=True, encoding='utf-8', errors='replace').stderr
    t = re.findall(r'time=(\d+):(\d+):([\d.]+)', e)
    if not t:
        return 0.0
    h, m, s = t[-1]
    return int(h) * 3600 + int(m) * 60 + float(s)


def process(src, dst, row, voice):
    """src (wav/mp3) -> dst .ogg mono 44.1 kHz w celu LUFS; zwraca (I, TP, dlugosc, uwaga)."""
    pre = []
    if not row['loop']:
        trim = 'silenceremove=start_periods=1:start_threshold=-50dB:start_silence=0.05'
        pre += [trim, 'areverse', trim, 'areverse']
    if voice:
        pre.append('highpass=f=90')
    pre.append('aresample=44100')
    target = row['lufs']
    tmp_pre = dst + '.pre.wav'
    ff(['-y', '-i', src, '-map', '0:a:0', '-map_metadata', '-1', '-af', ','.join(pre), '-ac', '1', '-ar', '44100', '-c:a', 'pcm_s16le', tmp_pre], 'obrobka')
    try:
        dur = duration(tmp_pre)
        if dur < 0.3:
            raise RuntimeError('po obcieciu ciszy zostalo %.2f s' % dur)
        # wzmocnienie do celu LUFS + ogranicznik szczytow (loudnorm na klipach < 3 s nie trafia w cel - sprawdzone na probie 25.10)
        i0, tp0, _ = measure(tmp_pre)
        uw = ''
        if i0 < -60:
            gain = -3.0 - tp0          # zrodlo za ciche/krotkie dla bramki EBU - normalizacja szczytu do -3 dBFS
            uw = 'normalizacja szczytu (I zrodla %.1f)' % i0
        else:
            gain = target - i0
        lim_db = -2.0
        tol = 2.0 if dur < 3 else 1.0
        tmp = dst + '.tmp.ogg'
        for proba in range(5):
            chain = 'volume=%.2fdB,alimiter=limit=%.4f:attack=2:release=60:level=0:latency=1' % (gain, 10 ** (lim_db / 20.0))
            ff(['-y', '-i', tmp_pre, '-af', chain, '-ac', '1', '-ar', '44100', '-c:a', 'libvorbis', '-q:a', '5', tmp], 'kodowanie')
            i, tp, d = measure(tmp)
            if tp > TP_MAX:
                lim_db -= tp - TP_MAX + 0.3      # szczyt po kodowaniu Vorbis za wysoko - nizszy prog ogranicznika
                continue
            if i0 >= -60 and i < target - tol:
                gain += target - i                # ogranicznik zjadl glosnosc - dodaj roznice
                continue
            break
        os.replace(tmp, dst)
        stan = 'ok' if ((i0 < -60 or abs(i - target) <= tol) and tp <= TP_MAX) else 'poza'
        if voice and d > MAX_GLOS_S:
            uw = (uw + ' ' if uw else '') + 'dluzszy niz %.1f s' % MAX_GLOS_S
        return i, tp, d, stan + (' (' + uw + ')' if uw else '')
    finally:
        for p in (tmp_pre, dst + '.tmp.ogg'):
            if os.path.exists(p):
                os.remove(p)


def wav(path, pcm, rate=24000):
    with open(path, 'wb') as f:
        f.write(b'RIFF' + struct.pack('<I', 36 + len(pcm)) + b'WAVEfmt ' + struct.pack('<IHHIIHH', 16, 1, 1, rate, rate * 2, 2, 16) + b'data' + struct.pack('<I', len(pcm)) + pcm)


class Obsada:
    def __init__(self, path):
        d = json.load(open(path, encoding='utf-8'))
        self.role, self.glosy = d['role'], d['glosy']
        self.konto_ids = None
        self.zamiana = {}          # voice_id -> voice_id zapasu (gdy dodanie/uzycie nie wyszlo)
        self.log = []

    def voice_of(self, rola):
        vid = self.role[rola]['voice_id']
        return self.zamiana.get(vid, vid)

    def name(self, vid):
        return (self.glosy.get(vid) or {}).get('nazwa', vid)

    def ensure(self, roles):
        """Dodaje do konta glosy z biblioteki potrzebne dla rol (POST /v1/voices/add/<owner>/<id>); blad -> zapas roli z konta."""
        if self.konto_ids is None:
            d = json.loads(http('/voices'))
            self.konto_ids = {v.get('voice_id') for v in d.get('voices', [])}
        for rola in sorted(roles):
            vid = self.role[rola]['voice_id']
            if vid in self.konto_ids or vid in self.zamiana:
                continue
            g = self.glosy.get(vid) or {}
            owner = g.get('public_owner_id')
            try:
                if not owner:
                    raise RuntimeError('glos spoza konta bez public_owner_id')
                res = json.loads(http('/voices/add/%s/%s' % (owner, vid), {'new_name': ('Bitwa ' + g.get('nazwa', vid))[:40]}))
                nid = res.get('voice_id') or vid
                self.konto_ids.add(nid)
                if nid != vid:
                    self.zamiana[vid] = nid
                self.log.append('dodany glos %s (%s) dla %s' % (g.get('nazwa', vid), nid, rola))
            except RuntimeError as e:
                if 'already' in str(e).lower():
                    self.konto_ids.add(vid)
                    continue
                fb = self.role[rola]['zapas_voice_id']
                self.zamiana[vid] = fb
                self.log.append('NIE dodano %s dla %s (%s) - zapas %s' % (g.get('nazwa', vid), rola, str(e)[:120], self.name(fb)))


def tts(row, ob, model_key):
    vid = ob.voice_of(row['glos'])
    text = row['tekst'] if model_key == 'v3' else spoken(row['tekst'])
    body = {'text': text, 'model_id': MODELS[model_key], 'voice_settings': SET_V3 if model_key == 'v3' else SET_V2,
            'seed': zlib.crc32(row['id'].encode()) & 0x7fffffff}
    pcm = http('/text-to-speech/%s?output_format=pcm_24000' % vid, body, accept='audio/*')
    if len(pcm) < 4800:
        raise RuntimeError('za krotkie audio (%d B)' % len(pcm))
    return pcm, vid, len(text)


def sfx(row):
    body = {'text': row['tekst'], 'duration_seconds': row['sek'], 'prompt_influence': SFX_PROMPT_INFLUENCE, 'model_id': SFX_MODEL}
    if row['loop']:
        body['loop'] = True
    data = http('/sound-generation?output_format=mp3_44100_128', body, accept='audio/mpeg', timeout=300)
    if len(data) < 2000:
        raise RuntimeError('za krotkie audio (%d B)' % len(data))
    return data


def src_md5(row, vid, model_key):
    return md5b(('%s|%s|%s|%s|%s|%s' % (row['tekst'], vid, model_key, row['lufs'], row['sek'], row['loop'])).encode('utf-8'))


def read_manifest(p):
    old = {}
    if os.path.exists(p):
        for l in open(p, encoding='utf-8'):
            if l.startswith('#') or not l.strip():
                continue
            c = l.rstrip('\n').rstrip('\r').split('\t')
            if len(c) >= len(COLS_MAN):
                old[c[0]] = dict(zip(COLS_MAN, c))
    return old


def write_manifest(p, man):
    order = sorted(man.values(), key=lambda m: (m['kategoria'], m['id']))
    with open(p + '.tmp', 'w', encoding='utf-8', newline='\n') as f:
        f.write('# ' + '\t'.join(COLS_MAN) + '\n')
        for m in order:
            f.write('\t'.join(str(m[c]).replace('\t', ' ') for c in COLS_MAN) + '\n')
    os.replace(p + '.tmp', p)


def arg(name, default=None):
    if name in sys.argv:
        i = sys.argv.index(name)
        if i + 1 < len(sys.argv):
            return sys.argv[i + 1]
    return default


def select(rows, rodzaj):
    prio = arg('--prio', '1')
    sel = [r for r in rows if (r['glos'] == 'SFX') == (rodzaj == 'sfx')]
    if prio != 'all':
        sel = [r for r in sel if r['prio'] <= int(prio)]
    if arg('--kategoria'):
        ks = set(arg('--kategoria').split(','))
        sel = [r for r in sel if r['kategoria'] in ks]
    if arg('--rola'):
        rs = set(arg('--rola').split(','))
        sel = [r for r in sel if r['glos'] in rs]
    if arg('--id'):
        ids = set(arg('--id').split(','))
        sel = [r for r in sel if r['id'] in ids]
    if '--probka' in sys.argv and rodzaj == 'tts':
        # po jednym klipie na role: motto, potem rozkaz szarzy, potem cokolwiek - do odsluchu obsady
        best = {}
        rank = {'motto': 0, 'rozkaz': 1, 'przemowa': 2}
        for r in sel:
            k = r['glos']
            if k not in best or rank.get(r['kategoria'], 9) < rank.get(best[k]['kategoria'], 9):
                best[k] = r
        sel = sorted(best.values(), key=lambda r: r['glos'])
    return sel


def main(rodzaj='tts'):
    sys.stdout.reconfigure(encoding='utf-8')
    rows = load_rows(arg('--csv', CSV))
    ob = Obsada(arg('--obsada', OBSADA))
    nagraj = '--nagraj' in sys.argv
    sel = select(rows, rodzaj)
    limit_zn = int(arg('--limit-znakow', '25000'))
    limit_sfx = int(arg('--limit-sfx', '120'))
    rezerwa = int(arg('--rezerwa', '30000'))
    for r in sel:
        if r['glos'] != 'SFX' and r['glos'] not in ob.role:
            raise SystemExit('rola %s (klip %s) nie ma obsady w %s' % (r['glos'], r['id'], OBSADA))
    znaki = sum(len(r['tekst'] if r['model'] == 'v3' else spoken(r['tekst'])) for r in sel if r['glos'] != 'SFX')
    sfx_s = sum(r['sek'] for r in sel if r['glos'] == 'SFX')
    n_sfx = sum(1 for r in sel if r['glos'] == 'SFX')
    by = {}
    for r in sel:
        by[r['kategoria']] = by.get(r['kategoria'], 0) + 1
    print('wybrano %d klipow (%s): %s' % (len(sel), rodzaj, ', '.join('%s %d' % kv for kv in sorted(by.items()))))
    if rodzaj == 'tts':
        roles = sorted({r['glos'] for r in sel})
        print('znakow TTS (z tagami v3): %d (limit %d) | rol: %d | glosow: %d' % (znaki, limit_zn, len(roles), len({ob.role[x]['voice_id'] for x in roles})))
        brak = sorted({ob.name(ob.role[x]['voice_id']) for x in roles if not ob.glosy[ob.role[x]['voice_id']].get('na_koncie')})
        print('glosy z biblioteki do dodania przy --nagraj (%d): %s' % (len(brak), '; '.join(brak)))
        est = znaki
    else:
        print('generacji SFX: %d (limit %d), lacznie %.0f s; szacunek %.0f kredytow przy %.0f/s (ZALOZENIE, mierzone na 1. generacji)' % (
            n_sfx, limit_sfx, sfx_s, sfx_s * SFX_KREDYT_NA_S, SFX_KREDYT_NA_S))
        est = sfx_s * SFX_KREDYT_NA_S
    if not os.path.isfile(FF):
        raise SystemExit('brak ffmpeg: ' + FF)
    if not nagraj:
        if '--konto' in sys.argv:
            k = konto()
            print('konto: %s, uzyte %s z %s, zostalo %s, przekroczenie limitu: %s' % (k['tier'], k['uzyte'], k['limit'], k['limit'] - k['uzyte'], k['mozna_przekroczyc']))
        print('TRYB --sprawdz: zadnego platnego wywolania. Nagranie: --nagraj --out <katalog> (dopiero po zgodzie Jeffa).')
        return 0
    out = arg('--out')
    if not out:
        raise SystemExit('--nagraj wymaga --out <katalog>')
    if rodzaj == 'tts' and znaki > limit_zn:
        raise SystemExit('STOP: %d znakow > limit %d (zmniejsz wybor albo podnies --limit-znakow)' % (znaki, limit_zn))
    if rodzaj == 'sfx' and n_sfx > limit_sfx:
        raise SystemExit('STOP: %d generacji SFX > limit %d' % (n_sfx, limit_sfx))
    k0 = konto()
    zostalo = k0['limit'] - k0['uzyte']
    print('kredyty przed: uzyte %d z %d, zostalo %d (tier %s)' % (k0['uzyte'], k0['limit'], zostalo, k0['tier']))
    if zostalo - est < rezerwa:
        raise SystemExit('STOP: po nagraniu zostaloby ok. %d kredytow < rezerwa %d (--rezerwa)' % (zostalo - est, rezerwa))
    os.makedirs(out, exist_ok=True)
    manp = os.path.join(out, 'manifest.tsv')
    man = read_manifest(manp)
    if rodzaj == 'tts':
        ob.ensure({r['glos'] for r in sel})
        for l in ob.log:
            print('  ' + l)
    todo = []
    for r in sel:
        mk = 'sfx' if r['glos'] == 'SFX' else r['model']
        vid = 'SFX' if r['glos'] == 'SFX' else ob.voice_of(r['glos'])
        dst = os.path.join(out, r['kategoria'], r['id'] + '.ogg')
        m = man.get(r['id'])
        if ('--nadpisz' not in sys.argv and m and m['md5_zrodla'] == src_md5(r, vid, mk) and plik_ok(dst)):
            continue
        todo.append(r)
    print('do nagrania: %d (gotowych pominietych: %d)' % (len(todo), len(sel) - len(todo)))
    fails = []
    stat = {'znaki': 0, 'sfx': 0, 'sfx_s': 0.0, 'v2_zamiast_v3': 0}

    def job(r):
        if r['glos'] == 'SFX':
            data = sfx(r)
            with _lock:
                stat['sfx'] += 1
                stat['sfx_s'] += r['sek']
            fd, src = tempfile.mkstemp(suffix='.mp3')
            os.close(fd)
            open(src, 'wb').write(data)
            vid, mk, voice = 'SFX', 'sfx', False
        else:
            mk = r['model']
            try:
                pcm, vid, n = tts(r, ob, mk)
            except RuntimeError as e:
                s = str(e)
                if 'voice_not_found' in s or 'HTTP 404' in s:
                    rola = r['glos']
                    with _lock:
                        ob.zamiana[ob.role[rola]['voice_id']] = ob.role[rola]['zapas_voice_id']
                    pcm, vid, n = tts(r, ob, mk)
                elif mk == 'v3' and ('HTTP 400' in s or 'HTTP 422' in s or 'HTTP 403' in s):
                    mk = 'v2'
                    pcm, vid, n = tts(r, ob, mk)
                    with _lock:
                        stat['v2_zamiast_v3'] += 1
                else:
                    raise
            with _lock:
                stat['znaki'] += n
            fd, src = tempfile.mkstemp(suffix='.wav')
            os.close(fd)
            wav(src, pcm)
            voice = True
        try:
            dst = os.path.join(out, r['kategoria'], r['id'] + '.ogg')
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            i, tp, d, pom = process(src, dst, r, voice)
            if not plik_ok(dst):
                raise RuntimeError('plik nie przeszedl kontroli (OggS / bloki zer)')
        finally:
            os.remove(src)
        return {'id': r['id'], 'kategoria': r['kategoria'], 'kultura/rod': r['kultura/rod'], 'plik': '%s/%s.ogg' % (r['kategoria'], r['id']),
                'glos': 'SFX' if vid == 'SFX' else ob.name(vid), 'dlugosc_s': '%.2f' % d, 'tekst': spoken(r['tekst']) if voice else r['tekst'],
                'rola': r['glos'], 'voice_id': vid, 'model': mk, 'lufs_cel': '%.0f' % r['lufs'], 'lufs_I': '%.1f' % i, 'tp_dBTP': '%.1f' % tp,
                'loop': '1' if r['loop'] else '0', 'prio': str(r['prio']), 'zakres': r['zakres'], 'md5_pliku': md5f(dst),
                'md5_zrodla': src_md5(r, vid, r['model'] if voice else 'sfx'), 'pomiar': pom}

    def run(batch, threads):
        with concurrent.futures.ThreadPoolExecutor(max_workers=threads) as ex:
            futs = {ex.submit(job, r): r for r in batch}
            for i, fu in enumerate(concurrent.futures.as_completed(futs), 1):
                r = futs[fu]
                try:
                    m = fu.result()
                    with _lock:
                        man[m['id']] = m
                    print('%d/%d %s %s s I %s TP %s %s' % (i, len(batch), m['id'], m['dlugosc_s'], m['lufs_I'], m['tp_dBTP'], m['pomiar']), flush=True)
                except Exception as e:
                    fails.append((r['id'], str(e)[:300]))
                    print('%d/%d %s BLAD %s' % (i, len(batch), r['id'], str(e)[:200]), flush=True)
        write_manifest(manp, man)

    threads = int(arg('--watki', '3' if rodzaj == 'tts' else '2'))
    if rodzaj == 'sfx' and todo:
        # 1. generacja sama: pomiar kosztu z subskrypcji (bez rownoleglych wywolan)
        first, rest = todo[:1], todo[1:]
        run(first, 1)
        k1 = konto()
        kosz = k1['uzyte'] - k0['uzyte']
        na_s = kosz / max(first[0]['sek'], 0.5)
        print('koszt 1. generacji SFX: %d kredytow za %.1f s = %.1f/s' % (kosz, first[0]['sek'], na_s))
        if na_s > SFX_KREDYT_MAX_S and '--bez-kontroli-kosztu' not in sys.argv:
            print('STOP SFX: koszt %.1f/s > %.1f/s z planu - reszta nie nagrana (uruchom z --bez-kontroli-kosztu po decyzji)' % (na_s, SFX_KREDYT_MAX_S))
            for r in rest:
                fails.append((r['id'], 'pominiety - koszt SFX %.1f kredytow/s ponad plan' % na_s))
            rest = []
        elif rest:
            est_rest = sum(r['sek'] for r in rest) * na_s
            if (k1['limit'] - k1['uzyte']) - est_rest < rezerwa:
                print('STOP SFX: reszta ok. %d kredytow naruszylaby rezerwe %d' % (est_rest, rezerwa))
                for r in rest:
                    fails.append((r['id'], 'pominiety - rezerwa kredytow'))
                rest = []
        if rest:
            run(rest, threads)
    elif todo:
        run(todo, threads)
    k2 = konto()
    used = k2['uzyte'] - k0['uzyte']
    with open(os.path.join(out, 'nieudane.tsv'), 'a', encoding='utf-8', newline='\n') as f:
        for i, e in fails:
            f.write('%s\t%s\t%s\n' % (time.strftime('%Y-%m-%d %H:%M'), i, e))
    line = ('%s %s: klipow %d, nieudanych %d | znakow TTS %d, v2 zamiast v3 %d | SFX %d generacji, %.0f s | kredyty przed %d po %d, zuzyte %d, zostalo %d'
            % (time.strftime('%Y-%m-%d %H:%M'), rodzaj, len(todo) - len(fails), len(fails), stat['znaki'], stat['v2_zamiast_v3'], stat['sfx'], stat['sfx_s'],
               k0['uzyte'], k2['uzyte'], used, k2['limit'] - k2['uzyte']))
    if ob.log:
        line += ' | obsada: ' + '; '.join(ob.log)
    with open(os.path.join(out, 'koszt.log'), 'a', encoding='utf-8', newline='\n') as f:
        f.write(line + '\n')
    print(line)
    print('manifest: %s (%d wierszy)' % (manp, len(man)))
    return 1 if fails else 0


if __name__ == '__main__':
    sys.exit(main('tts'))
